using System.Text;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Instruments.Devices;
using Agilent66332A.Instruments.Serial;
using Agilent66332A.Instruments.Simulation;

namespace Agilent66332A.Instruments.Tests;

public class AcquisitionTests
{
    [Fact]
    public async Task ReadsMaximumFragmentedWaveformAndSameBufferStatistics()
    {
        var transport = new SimulatedTransport { Scenario = SimulatorScenario.NegativeCurrent };
        await using var client = Simulated(transport);
        var trace = new List<string>();
        client.Trace += message => trace.Add(message);
        await client.ConnectAsync(new());
        await client.SetOutputAsync(true);
        var settings = AcquisitionSettings.Default with { Signal = MeasurementSignal.Current, Sweep = new(4096) };
        var capture = await client.CaptureAsync(new(settings, CaptureStart.MeasureNow, TimeSpan.FromSeconds(5)));

        Assert.Equal(4096, capture.Samples.Count);
        Assert.All(capture.Samples, value => Assert.True(value < 0));
        Assert.Equal(capture.Samples.Min(), capture.Statistics.Minimum);
        Assert.Equal(capture.Samples.Max(), capture.Statistics.Maximum);
        Assert.Equal(capture.Samples.Average(), capture.Statistics.Dc);
        Assert.Contains("< [4096 waveform samples]", trace);
        Assert.DoesNotContain(trace, message => message.Length > 4096);
        Assert.Contains("FETC:CURR:ACDC?", transport.Commands);
    }

    [Fact]
    public async Task RepeatedSerialTriggersReturnTheAcknowledgedSweepLengthAndOffset()
    {
        var transport = new SimulatedTransport();
        await using var client = Simulated(transport);
        await client.ConnectAsync(new());
        var settings = AcquisitionSettings.Default with { Sweep = new(100, .0000312, -20), VoltageTrigger = new(Count: 3) };
        var capture = await client.CaptureAsync(new(settings, CaptureStart.SerialTrigger, TimeSpan.FromSeconds(5)), takeFireRequest: () => true);

        Assert.Equal(3, transport.Commands.Count(command => command == "*TRG"));
        Assert.Equal(100, capture.Samples.Count);
        Assert.Equal(-20 * settings.Sweep.IntervalSeconds, capture.SecondsAt(0), 10);
        Assert.Equal(0, capture.SecondsAt(20));
        Assert.Contains("FETC:ARR:VOLT?", transport.Commands);
    }

    [Fact]
    public async Task RejectsOversizedMultipleSweepsBeforeArming()
    {
        var transport = new SimulatedTransport();
        await using var client = Simulated(transport);
        await client.ConnectAsync(new());
        var settings = AcquisitionSettings.Default with { VoltageTrigger = new(Count: 3) };
        await Assert.ThrowsAsync<ArgumentException>(() => client.CaptureAsync(new(settings, CaptureStart.Internal, TimeSpan.FromSeconds(5))));
        Assert.DoesNotContain("INIT:SEQ2", transport.Commands);
        Assert.DoesNotContain("OUTP 0", transport.Commands);
    }

    [Fact]
    public async Task WaitingCaptureOwnsTheBufferAndStopsWhenNoTriggerArrives()
    {
        var transport = new SimulatedTransport();
        await using var client = Simulated(transport);
        await client.ConnectAsync(new());
        var armed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new OnProgress<CaptureProgress>(value => { if (value.Phase == CapturePhase.Waiting) armed.TrySetResult(); });
        var capture = client.CaptureAsync(new(AcquisitionSettings.Default, CaptureStart.Internal, TimeSpan.FromMilliseconds(400)), progress);

        await armed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await Assert.ThrowsAsync<InvalidOperationException>(() => client.MeasureAsync());
        await Assert.ThrowsAsync<TimeoutException>(() => capture);
        Assert.DoesNotContain("MEAS:VOLT?", transport.Commands);
        Assert.DoesNotContain("FETC:ARR:VOLT?", transport.Commands);
        Assert.Contains("ABOR", transport.Commands);
        Assert.Equal(OutputState.Off, client.Snapshot!.Output);
    }

    [Fact]
    public async Task OutputOffCancelsAnArmedContinuousTransientAndPreventsLaterFire()
    {
        var transport = new SimulatedTransport();
        await using var client = Simulated(transport);
        await client.ConnectAsync(new());
        await client.SetOutputAsync(true);
        var armed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var progress = new OnProgress<TransientProgress>(value => { if (value.CanFire) armed.TrySetResult(); });
        var transient = client.RunTransientAsync(new(new(3, .1), TransientTrigger.Serial, true, TimeSpan.FromSeconds(5)), () => false, progress);

        await armed.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var off = await client.RequestOutputOffAsync("test");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => transient);
        Assert.Equal(ShutdownOutcome.VerifiedOff, off.Outcome);
        Assert.DoesNotContain("*TRG", transport.Commands);
        Assert.True(transport.Commands.IndexOf("INIT:CONT:SEQ1 OFF") < transport.Commands.IndexOf("ABOR"));
        Assert.False((await client.ReadTransientAsync()).WaitingForTrigger);
    }

    [Fact]
    public async Task OutputOffInterruptsArrayTransferAndRequiresReconnectAfterBreak()
    {
        var transport = new InterruptibleWaveformTransport();
        await using var client = Simulated(transport);
        await client.ConnectAsync(new());
        await client.SetOutputAsync(true);
        var capture = client.CaptureAsync(new(AcquisitionSettings.Default, CaptureStart.MeasureNow, TimeSpan.FromSeconds(5)));

        await transport.Downloading.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var off = await client.RequestOutputOffAsync("cancel waveform transfer");
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => capture);
        Assert.Equal(ShutdownOutcome.VerifiedOff, off.Outcome);
        Assert.True(transport.SentBreak);
        Assert.False(client.IsSessionUsable);
        Assert.Equal(OutputState.Off, client.Snapshot!.Output);
    }

    private static AgilentClient Simulated(IByteTransport transport) => new(transport, new SystemClock(), true, "Simulator");

    private sealed class OnProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class InterruptibleWaveformTransport : IByteTransport
    {
        private readonly SimulatedTransport inner = new();
        private bool blocked;
        public TaskCompletionSource Downloading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool SentBreak { get; private set; }
        public bool IsOpen => inner.IsOpen;
        public void Open(ConnectionProfile profile) => inner.Open(profile);
        public void Write(ReadOnlySpan<byte> bytes)
        {
            inner.Write(bytes);
            if (!Encoding.ASCII.GetString(bytes).StartsWith("MEAS:ARR:", StringComparison.Ordinal)) return;
            blocked = true;
            Downloading.TrySetResult();
        }
        public int Read(Span<byte> buffer)
        {
            if (!blocked) return inner.Read(buffer);
            Thread.Sleep(10);
            throw new TimeoutException();
        }
        public void SendBreak(CancellationToken token) { SentBreak = true; blocked = false; inner.SendBreak(token); }
        public void DiscardInput() => inner.DiscardInput();
        public void Close() => inner.Close();
        public void Dispose() => inner.Dispose();
    }
}
