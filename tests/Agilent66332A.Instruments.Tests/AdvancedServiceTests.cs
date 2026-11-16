using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Instruments.Devices;
using Agilent66332A.Instruments.Simulation;

namespace Agilent66332A.Instruments.Tests;

public sealed class AdvancedServiceTests
{
    [Fact]
    public async Task SetupUsesTheInstalledRelayOptionAndKeepsDigitalPinReadbackIndependent()
    {
        var transport = new SimulatedTransport { HasRelayOption = false, DigitalPinInput = true };
        await using var client = Create(transport);
        await client.ConnectAsync(new());
        var setup = await client.ReadInstrumentSetupAsync();
        Assert.Null(setup.Relay);
        Assert.DoesNotContain(transport.Commands, command => command.StartsWith("OUTP:REL", StringComparison.Ordinal));
        await Assert.ThrowsAsync<NotSupportedException>(() => client.ConfigureRelayAsync(new(true, RelayPolarity.Reverse)));

        var configured = await client.ConfigureDigitalPortAsync(new(DigitalPortFunction.DigitalIo, InhibitMode.Latching,
            true, FaultIndicatorSource.Questionable));
        Assert.Equal(DigitalPortFunction.DigitalIo, configured.Port.Function);
        var reading = await client.WriteDigitalPortAsync(1);
        Assert.True(reading.Output0);
        Assert.False(reading.Output1);
        Assert.True(reading.Pin3High);
    }

    [Fact]
    public async Task IndependentOutputSwitchPreservesTheVerifiedRelayAndQuotedPanelTextRoundTrips()
    {
        var transport = new SimulatedTransport();
        await using var client = Create(transport);
        await client.ConnectAsync(new());
        var configured = await client.ConfigureRelayAsync(new(false, RelayPolarity.Reverse));
        Assert.Equal(new RelaySettings(false, RelayPolarity.Reverse), configured.Relay);
        Assert.Equal(OutputState.On, (await client.SetOutputWithoutRelayAsync(true)).Output);
        Assert.Equal(configured.Relay, (await client.ReadInstrumentSetupAsync()).Relay);
        Assert.Equal(OutputState.Off, (await client.SetOutputWithoutRelayAsync(false)).Output);
        var display = new PanelDisplaySettings(true, PanelDisplayMode.Text, "Bench \"A\"; 1");
        Assert.Equal(display, (await client.ConfigurePanelDisplayAsync(display)).Display);
        await Assert.ThrowsAsync<ArgumentException>(() => client.ConfigurePanelDisplayAsync(display with { Text = "A\r\nOUTP 1" }));
    }

    [Fact]
    public async Task StatusReadLeavesLatchedEventsUntilExplicitAcknowledgment()
    {
        var transport = new SimulatedTransport();
        await using var client = Create(transport);
        await client.ConnectAsync(new());
        var original = await client.ReadDiagnosticsAsync();
        var requested = original.Settings with
        {
            Questionable = original.Settings.Questionable with { Enable = 2, PositiveTransitions = 2 },
            ServiceRequestEnable = 8
        };
        Assert.Equal(requested, (await client.ConfigureStatusAsync(requested)).Settings);
        transport.Scenario = SimulatorScenario.ProtectionTrip;
        transport.Commands.Clear();
        StatusRegisterSnapshot fault = await client.ReadDiagnosticsAsync();
        Assert.Equal(2, fault.QuestionableCondition);
        Assert.Equal(8, fault.StatusByte & 8);
        Assert.DoesNotContain("STAT:QUES:EVEN?", transport.Commands);
        Assert.DoesNotContain("*ESR?", transport.Commands);

        var eventReading = await client.ReadAndAcknowledgeStatusEventAsync(StatusEventRegister.Questionable);
        Assert.Equal(2, eventReading.Value);
        var acknowledged = await client.ReadDiagnosticsAsync();
        Assert.Equal(2, acknowledged.QuestionableCondition);
        Assert.Equal(0, acknowledged.StatusByte & 8);
    }

    [Fact]
    public async Task SavedStateRequiresIsolatedRecallAndOffIsTheFirstCommandAfterRecall()
    {
        var transport = new SimulatedTransport();
        await using var client = Create(transport);
        await client.ConnectAsync(new());
        await client.ApplyAsync(new(3, .12));
        await client.SaveDeviceStateAsync(2);
        await client.ApplyAsync(new(8, .14));
        transport.Commands.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.RecallDeviceStateAsync(2, false));
        Assert.Empty(transport.Commands);

        var restored = await client.RecallDeviceStateAsync(2, true);
        Assert.Equal(3, restored.Settings.VoltageV);
        Assert.Equal(OutputState.Off, restored.Output);
        int recall = transport.Commands.IndexOf("*RCL 2");
        Assert.True(recall >= 0);
        Assert.Equal("OUTP 0", transport.Commands[recall + 1]);
        Assert.Contains("INIT:CONT:SEQ1 OFF", transport.Commands);
    }

    [Fact]
    public async Task StartupPolicyRejectsAStaleReviewAndWritesASafeSlotBeforeEnablingRecall()
    {
        var transport = new SimulatedTransport();
        await using var client = Create(transport);
        var stale = await client.ConnectAsync(new());
        await client.ApplyAsync(new(8, .14));
        transport.Commands.Clear();
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.ConfigurePowerOnAsync(PowerOnMode.RecallSlotZero, stale));
        Assert.DoesNotContain("*SAV 0", transport.Commands);

        var actual = await client.ConfigurePowerOnAsync(PowerOnMode.RecallSlotZero, client.Snapshot!);
        Assert.Equal(PowerOnMode.RecallSlotZero, actual.PowerOn);
        int save = transport.Commands.IndexOf("*SAV 0");
        Assert.True(save > 0);
        Assert.True(transport.Commands.IndexOf("OUTP 0") < save);
        Assert.True(save < transport.Commands.IndexOf("OUTP:PON:STAT RCL0"));
    }

    [Fact]
    public async Task CompatibilityNeverClaimsVerifiedOffAndCanReturnToKnownScpiSession()
    {
        var transport = new SimulatedTransport();
        await using var client = Create(transport);
        var original = await client.ConnectAsync(new());
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.SetProgrammingLanguageAsync(ProgrammingLanguage.Compatibility, false));
        var legacy = await client.SetProgrammingLanguageAsync(ProgrammingLanguage.Compatibility, true);
        Assert.Equal(ProgrammingLanguage.Compatibility, legacy.Language);
        Assert.Equal(OutputState.Unknown, client.Snapshot!.Output);
        Assert.False(client.CanMonitor);
        var reading = await client.ReadCompatibilityAsync();
        Assert.Equal(original.Identity, reading.Identity);
        Assert.Equal(0, reading.VoltageV);
        var stop = await client.RequestOutputOffAsync("Legacy off test");
        Assert.Equal(ShutdownOutcome.Unverified, stop.Outcome);
        Assert.True(client.IsSessionUsable);

        Assert.Equal(ProgrammingLanguage.Scpi, (await client.SetProgrammingLanguageAsync(ProgrammingLanguage.Scpi, true)).Language);
        Assert.Equal(OutputState.Off, client.Snapshot!.Output);
        Assert.Equal(original.Identity, client.Snapshot.Identity);
    }

    [Fact]
    public async Task CompatibilityStartupAndFaultControlsCheckErrorsAndGateTheRelay()
    {
        var transport = new SimulatedTransport { HasRelayOption = false };
        await using var client = Create(transport);
        await client.ConnectAsync(new());
        var service = await client.SetProgrammingLanguageAsync(ProgrammingLanguage.Compatibility, true);
        Assert.False(service.CompatibilityRelayPresent);
        await Assert.ThrowsAsync<NotSupportedException>(() => client.SetCompatibilityRelayAtPowerOnAsync(true, true));
        Assert.DoesNotContain(transport.Commands, command => command.StartsWith("RLYPON", StringComparison.Ordinal));
        Assert.True((await client.SetCompatibilityPowerOnRequestAsync(true)).Accepted);
        var duplicate = await client.SetCompatibilityPowerOnRequestAsync(true);
        Assert.False(duplicate.Accepted);
        Assert.Equal(2, duplicate.ErrorCode);
        Assert.True((await client.SetCompatibilityFaultMaskAsync(64)).Accepted);
        Assert.True((await client.SetCompatibilityServiceRequestsAsync(true)).Accepted);
        transport.Scenario = SimulatorScenario.ProtectionTrip;
        await client.ReadCompatibilityAsync();
        Assert.True(transport.CompatibilityServiceRequestAsserted);
        Assert.Equal(64, (await client.AcknowledgeCompatibilityEventsAsync()).Fault);
        Assert.Equal(0, await client.ReadCompatibilityErrorAsync());
        Assert.True((await client.RunCompatibilitySelfTestAsync(true)).Passed);
        var clear = await client.ClearCompatibilityAsync(true);
        Assert.True(clear.Accepted);
        Assert.Equal("OUT 0", transport.Commands[transport.Commands.IndexOf("CLR") + 1]);
        Assert.Equal(OutputState.Unknown, client.Snapshot!.Output);
    }

    [Fact]
    public async Task CalibrationDiscardDoesNotSaveAndPasswordsStayOutOfBothTranscripts()
    {
        const string secret = "849265";
        var transport = new SimulatedTransport();
        await using var client = Create(transport);
        var trace = new List<string>();
        client.Trace += trace.Add;
        await client.ConnectAsync(new());
        await client.ChangeCalibrationPasscodeAsync(null, 849265);
        var prompts = new List<CalibrationPromptKind>();
        var result = await client.RunCalibrationAsync(CalibrationProcedure.Voltage, 849265, (prompt, _) =>
        {
            prompts.Add(prompt.Kind);
            return Task.FromResult(new CalibrationAnswer(prompt.Kind != CalibrationPromptKind.Save, prompts.Count == 2 ? .01 : 19.5));
        });

        Assert.False(result.Saved);
        Assert.Equal(new[] { CalibrationPromptKind.Fixture, CalibrationPromptKind.ReferenceReading,
            CalibrationPromptKind.ReferenceReading, CalibrationPromptKind.Save }, prompts);
        Assert.DoesNotContain("CAL:SAVE", transport.Commands);
        Assert.DoesNotContain(trace.Concat(transport.Commands), line => line.Contains(secret, StringComparison.Ordinal));
        Assert.False((await client.ReadServiceAsync()).CalibrationEnabled);
        Assert.Equal(OutputState.Off, client.Snapshot!.Output);
    }

    [Fact]
    public async Task ShutdownDuringCalibrationStopsTheOutputAndRejectsAnAlreadyOpenReferencePrompt()
    {
        var transport = new SimulatedTransport();
        await using var client = Create(transport);
        await client.ConnectAsync(new());
        var waiting = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var reference = new TaskCompletionSource<CalibrationAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calibration = client.RunCalibrationAsync(CalibrationProcedure.PositiveCurrent, null, (prompt, _) =>
        {
            if (prompt.Kind == CalibrationPromptKind.Fixture) return Task.FromResult(new CalibrationAnswer(true));
            waiting.TrySetResult();
            return reference.Task;
        });
        await waiting.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var stop = await client.RequestOutputOffAsync("Calibration stop");
        Assert.Equal(ShutdownOutcome.VerifiedOff, stop.Outcome);
        reference.SetResult(new(true, 1));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => calibration);
        Assert.DoesNotContain(transport.Commands, command => command.StartsWith("CAL:DATA", StringComparison.Ordinal));
        Assert.False((await client.ReadServiceAsync()).CalibrationEnabled);
    }

    [Fact]
    public async Task NegativeCalibrationWaitsForExternalSourceRemovalBeforeSaving()
    {
        var transport = new SimulatedTransport();
        await using var client = Create(transport);
        await client.ConnectAsync(new());
        var prompts = new List<CalibrationPromptKind>();
        var calibration = await client.RunCalibrationAsync(CalibrationProcedure.NegativeCurrent, null, (prompt, _) =>
        {
            prompts.Add(prompt.Kind);
            return Task.FromResult(new CalibrationAnswer(true));
        });

        Assert.True(calibration.Saved);
        Assert.Equal(new[] { CalibrationPromptKind.Fixture, CalibrationPromptKind.Fixture, CalibrationPromptKind.Save }, prompts);
        int procedure = transport.Commands.IndexOf("CAL:CURR:NEG");
        int store = transport.Commands.IndexOf("CAL:SAVE");
        Assert.True(procedure >= 0 && store > procedure);
        Assert.Contains("OUTP 0", transport.Commands.Skip(procedure + 1).Take(store - procedure - 1));
        Assert.False((await client.ReadServiceAsync()).CalibrationEnabled);
    }

    private static AgilentClient Create(SimulatedTransport transport) => new(transport, new SystemClock(), true, "Simulator");
}
