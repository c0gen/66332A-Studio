using System.Text;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Instruments.Devices;
using Agilent66332A.Instruments.Scpi;
using Agilent66332A.Instruments.Simulation;

namespace Agilent66332A.Instruments.Tests;
public class ProtocolTests
{
    [Theory] [InlineData(" -2.34E-3 ", -.00234)] [InlineData("+5.0", 5)]
    public void ParsesSignedScientificNotation(string text, double value) => Assert.Equal(value, ScpiParser.Number(text));
    [Theory] [InlineData("NaN")] [InlineData("Infinity")] [InlineData("1,23")] [InlineData("text")]
    public void RejectsMalformedNumbers(string text) => Assert.Throws<FormatException>(() => ScpiParser.Number(text));
    [Theory] [InlineData("HEWLETT-PACKARD")] [InlineData("AGILENT TECHNOLOGIES")] [InlineData("Keysight Technologies")]
    public void IdentifiesSupportedManufacturerVariants(string manufacturer) => Assert.Equal("66332A", ScpiParser.Identity(manufacturer + ",66332A,S,1").Model);
    [Fact] public void ParsesQuotedErrorAndRejectsImpossibleStatus()
    { Assert.Equal((-100, "Bad, parameter"), ScpiParser.Error("-100,\"Bad, parameter\"")); Assert.Throws<FormatException>(() => ScpiParser.Register("1.2")); Assert.Throws<FormatException>(() => ScpiParser.Register("65536")); }
    [Fact] public void FramingAcceptsFragmentedCrlfAndBoundsLines()
    {
        var f = new LineFramer(); foreach (byte b in Encoding.ASCII.GetBytes("12\r")) Assert.Null(f.Push(b)); Assert.Equal("12", f.Push(10));
        for (int i = 0; i < 4096; i++) f.Push(65); Assert.Throws<FormatException>(() => f.Push(65));
    }
    [Fact] public void ChannelRefusesLateNumericRepliesWithoutValidatedBreak()
    {
        using var t = new ScriptedTransport(); t.Open(new()); t.Script.Enqueue(("VOLT?", []));
        var channel = new ScpiChannel(t, _ => { });
        Assert.Throws<TimeoutException>(() => channel.Query("VOLT?", default, .005));
        Assert.False(channel.Synchronized);
        Assert.Throws<IOException>(() => channel.Recover(false, default));
        Assert.Throws<IOException>(() => channel.Query("CURR?", default)); Assert.Single(t.Writes);
    }
    [Fact] public void ChannelAssemblesChunksAndRejectsUnsolicitedExtraLine()
    {
        using var t = new ScriptedTransport(); t.Open(new());
        t.Script.Enqueue(("VOLT?", [Encoding.ASCII.GetBytes("1.0E"), Encoding.ASCII.GetBytes("+0\r\n")]));
        var channel = new ScpiChannel(t, _ => { }); Assert.Equal(1, ScpiParser.Number(channel.Query("VOLT?", default)));
        t.Script.Enqueue(("CURR?", [Encoding.ASCII.GetBytes("1\n2\n")])); Assert.Throws<IOException>(() => channel.Query("CURR?", default));
    }

    [Fact]
    public void BreakRecoveryRejectsWaveformTailBeforeAcceptingCleanLanguageAndIdentity()
    {
        using var transport = new ScriptedTransport();
        transport.Open(new());
        transport.Script.Enqueue(("SYST:LANG?", []));
        transport.Script.Enqueue(("SYST:LANG?", ["5.18516E-3,3.89SCPI\r\n"u8.ToArray()]));
        transport.Script.Enqueue(("SYST:LANG?", ["SCPI\r\n"u8.ToArray()]));
        transport.Script.Enqueue(("*IDN?", ["HEWLETT-PACKARD,66332A,0,A.01.03\r\n"u8.ToArray()]));
        transport.Script.Enqueue(("OUTP?", ["0\r\n"u8.ToArray()]));
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(4));
        var channel = new ScpiChannel(transport, _ => { });

        channel.Recover(true, budget.Token);

        Assert.Equal("0", channel.Query("OUTP?", budget.Token));
        Assert.Equal(5, transport.Writes.Count);
    }
    [Fact] public async Task UnsupportedIdentityNeverWritesConfiguration()
    {
        var t = new ScriptedTransport();
        t.Script.Enqueue(("SYST:LANG?", [Encoding.ASCII.GetBytes("SCPI\n")]));
        t.Script.Enqueue(("*IDN?", [Encoding.ASCII.GetBytes("AGILENT,66312A,S,1\n")]));
        await using var c = new AgilentClient(t, new SystemClock(), true, "Simulator");
        await Assert.ThrowsAsync<NotSupportedException>(() => c.ConnectAsync(new()));
        Assert.Equal(new[] { "SYST:LANG?\r\n", "*IDN?\r\n" }, t.Writes);
    }
    [Fact] public async Task SimulatorVerifiesQuantizedTargetsAndProtectionOffOrdering()
    {
        var t = new SimulatedTransport(); await using var c = new AgilentClient(t, new SystemClock(), true, "Simulator");
        await c.ConnectAsync(new()); await c.SetOutputAsync(true);
        t.Commands.Clear(); var result = await c.ApplyAsync(new(4.001, .2001));
        Assert.Equal(4, result.Acknowledged.VoltageV); Assert.Equal(.2, result.Acknowledged.CurrentLimitA, 10);
        int voltageWrite = t.Commands.FindIndex(x => x.StartsWith("VOLT "));
        Assert.True(voltageWrite >= 0 && voltageWrite < t.Commands.FindIndex(x => x.StartsWith("CURR ")));
        t.Commands.Clear(); await c.ApplyProtectionsAsync(new(21, true, .1));
        Assert.True(t.Commands.IndexOf("OUTP 0") < t.Commands.FindIndex(x => x.StartsWith("VOLT:PROT ")));
        await c.ClearProtectionAsync(); Assert.Equal(OutputState.Off, c.Snapshot!.Output);
    }
    [Fact] public async Task PartialApplyMismatchStopsFurtherWritesAndRequestsOff()
    {
        var t = new InterceptTransport(); await using var c = new AgilentClient(t, new SystemClock(), true, "Simulator");
        await c.ConnectAsync(new()); bool changed = false;
        t.BeforeWrite = command => { if (command.StartsWith("VOLT ")) changed = true; };
        t.ReplaceReply = command => changed && command == "VOLT?" ? "9" : null;
        await Assert.ThrowsAsync<IOException>(() => c.ApplyAsync(new(4, .2)));
        Assert.Contains("OUTP 0", t.Inner.Commands); Assert.DoesNotContain(t.Inner.Commands, s => s.StartsWith("CURR "));
    }
    [Fact] public async Task OffDuringReadInvalidatesAlreadyQueuedEnableAtActualWorker()
    {
        var t = new InterceptTransport(); await using var c = new AgilentClient(t, new SystemClock(), true, "Simulator");
        await c.ConnectAsync(new()); t.Inner.Commands.Clear(); t.BlockVoltageMeasurement = true;
        var measurement = c.MeasureAsync(); Assert.True(t.MeasurementEntered.Wait(TimeSpan.FromSeconds(2)));
        var on = c.SetOutputAsync(true);
        var off = c.RequestOutputOffAsync("Race test");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => measurement);
        Assert.Equal(ShutdownOutcome.VerifiedOff, (await off).Outcome);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => on);
        Assert.DoesNotContain("OUTP 1", t.Inner.Commands); Assert.Equal(OutputState.Off, c.Snapshot!.Output);
    }
    [Fact] public async Task UnvalidatedPhysicalProfileCannotConfigureAndCableLossIsUnknown()
    {
        var t = new SimulatedTransport(); await using var c = new AgilentClient(t, new SystemClock());
        await c.ConnectAsync(new()); await Assert.ThrowsAsync<InvalidOperationException>(() => c.SetOutputAsync(true));
        t.Scenario = SimulatorScenario.CableLoss;
        await Assert.ThrowsAsync<IOException>(() => c.MeasureAsync()); Assert.Equal(OutputState.Unknown, c.Snapshot!.Output);
        Assert.Equal(ShutdownOutcome.Unverified, (await c.RequestOutputOffAsync("Loss")).Outcome);
    }

    [Fact]
    public async Task LiveVerificationEnablesBasicControlOnlyAfterBreakAndOffReadback()
    {
        var transport = new SimulatedTransport();
        await using var client = new AgilentClient(transport, new SystemClock(), verifyBasicControlOnConnect: true);

        var state = await client.ConnectAsync(new());

        Assert.True(state.Capabilities.HardwareValidated);
        Assert.True(client.CanConfigure);
        Assert.Equal(QualifiedFeature.None, state.Capabilities.QualifiedFeatures);
        Assert.Equal(OutputState.Off, state.Output);
        Assert.Contains("MEAS:ARR:VOLT?", transport.Commands);
        Assert.Contains("OUTP 0", transport.Commands);
        Assert.Equal(.2, (await client.ApplyAsync(new(.2, .01))).Acknowledged.VoltageV, 8);
        Assert.Equal(OutputState.Off, client.Snapshot!.Output);
        Assert.DoesNotContain("OUTP 1", transport.Commands);
    }

    [Fact]
    public async Task LiveVerificationLeavesAnEnabledOutputAloneAndStaysReadOnly()
    {
        var transport = new SimulatedTransport();
        transport.Open(new());
        transport.Write("OUTP 1\r\n"u8);
        transport.Close();
        transport.Commands.Clear();
        await using var client = new AgilentClient(transport, new SystemClock(), verifyBasicControlOnConnect: true);

        var state = await client.ConnectAsync(new());

        Assert.False(client.CanConfigure);
        Assert.Equal(OutputState.On, state.Output);
        Assert.DoesNotContain("MEAS:ARR:VOLT?", transport.Commands);
        Assert.DoesNotContain("OUTP 0", transport.Commands);
    }

    [Fact] public async Task SignedCurrentIsPreserved()
    {
        var t = new SimulatedTransport { Scenario = SimulatorScenario.NegativeCurrent };
        await using var c = new AgilentClient(t, new SystemClock(), true, "Simulator"); await c.ConnectAsync(new()); await c.SetOutputAsync(true);
        var pair = await c.MeasureAsync(); Assert.True(pair.Current.Value < 0); Assert.True(pair.EstimatedPowerW < 0);
    }
    [Fact] public async Task NewClientsNeverReuseSessionGenerations()
    {
        await using var first = new AgilentClient(new SimulatedTransport(), new SystemClock(), true, "Simulator");
        await using var second = new AgilentClient(new SimulatedTransport(), new SystemClock(), true, "Simulator");
        var a = await first.ConnectAsync(new()); var b = await second.ConnectAsync(new());
        Assert.NotEqual(a.Generation, b.Generation);
    }
}
