using System.Globalization;
using Agilent66332A.Application.Monitoring;
using Agilent66332A.Application.Runs;
using Agilent66332A.Core.Models;
using Agilent66332A.Core.Sequences;
using Studio.Tests;

namespace Agilent66332A.Application.Tests;
public class RunnerTests
{
    [Fact] public async Task HoldsStartAfterVerificationRepeatLazilyAndFinishOff()
    {
        var clock = new FakeClock(); var instrument = new FakeInstrument(clock); var factory = new FakeRecordings();
        var runner = new SequenceRunner(instrument, clock, factory);
        var d = SequenceDocument.Example with { RepeatCount = 2 };
        var result = await runner.RunAsync(d, new(), 1, default);
        Assert.Equal(ExecutionOutcome.Completed, result.ExecutionOutcome); Assert.Equal(ShutdownOutcome.VerifiedOff, result.Shutdown.Outcome);
        Assert.Equal("off", instrument.Calls.First(c => c.Command is "off" or "on").Command);
        Assert.Equal(6, factory.Session.Events.Count(e => e.Type == "holdEnded"));
        var holds = factory.Session.Events.Where(e => e.Type is "holdStarted" or "holdEnded").ToArray();
        for (int i = 0; i < holds.Length; i += 2)
        {
            var step = d.Steps.First(s => s.Id == holds[i].StepId);
            Assert.True(holds[i + 1].ElapsedSeconds - holds[i].ElapsedSeconds >= step.HoldSeconds - 1e-9);
        }
        Assert.Equal("shutdown", instrument.Calls[^1].Command); Assert.True(factory.Session.Samples > 0);
    }
    [Fact] public async Task PreflightFailureCannotEnableOutput()
    {
        var clock = new FakeClock(); var instrument = new FakeInstrument(clock);
        var runner = new SequenceRunner(instrument, clock, new FakeRecordings { FailPreflight = true });
        var result = await runner.RunAsync(SequenceDocument.Example, new(), 1, default);
        Assert.Equal(ExecutionOutcome.Failed, result.ExecutionOutcome); Assert.DoesNotContain(instrument.Calls, c => c.Command == "on"); Assert.Equal("shutdown", instrument.Calls[^1].Command);
    }
    [Fact] public async Task CancellationBetweenApplyAndEnableCannotEnable()
    {
        var clock = new FakeClock(); using var cancellation = new CancellationTokenSource();
        var instrument = new FakeInstrument(clock); instrument.AfterCall = cmd => { if (cmd.StartsWith("apply ")) cancellation.Cancel(); };
        var runner = new SequenceRunner(instrument, clock, new FakeRecordings());
        var result = await runner.RunAsync(SequenceDocument.Example, new(), 1, cancellation.Token);
        Assert.Equal(ExecutionOutcome.Cancelled, result.ExecutionOutcome); Assert.DoesNotContain(instrument.Calls, c => c.Command == "on"); Assert.Equal("shutdown", instrument.Calls[^1].Command);
    }
    [Fact] public async Task RecordingFailureStopsAtFirstRowAndDoesNotHideCause()
    {
        var clock = new FakeClock(); var instrument = new FakeInstrument(clock); var factory = new FakeRecordings(); factory.Session.FailAppend = true;
        var runner = new SequenceRunner(instrument, clock, factory);
        var result = await runner.RunAsync(SequenceDocument.Example, new(), 1, default);
        Assert.Equal(ExecutionOutcome.Failed, result.ExecutionOutcome); Assert.Contains("Disk full", result.Error);
        Assert.DoesNotContain(instrument.Calls, c => c.Command == "apply 2"); Assert.Equal("shutdown", instrument.Calls[^1].Command);
    }
    [Fact] public async Task ExecutionShutdownAndRecordingOutcomesAreIndependent()
    {
        var clock = new FakeClock(); var instrument = new FakeInstrument(clock) { FailShutdown = true }; var factory = new FakeRecordings(); factory.Session.FailFinalization = true;
        var result = await new SequenceRunner(instrument, clock, factory).RunAsync(SequenceDocument.Example, new(), 1, default);
        Assert.Equal(ExecutionOutcome.Completed, result.ExecutionOutcome); Assert.Equal(ShutdownOutcome.Unverified, result.Shutdown.Outcome); Assert.Equal(RecordingOutcome.Failed, result.RecordingOutcome);
    }
    [Fact] public async Task FrontPanelChangeAbortsRun()
    {
        var clock = new FakeClock(); var instrument = new FakeInstrument(clock) { ExternalChange = true };
        var result = await new SequenceRunner(instrument, clock, new FakeRecordings()).RunAsync(SequenceDocument.Example, new(), 1, default);
        Assert.Equal(ExecutionOutcome.Failed, result.ExecutionOutcome); Assert.Contains("unexpectedly", result.Error);
    }
    [Fact] public async Task SchedulingInterruptionAbortsInsteadOfCatchingUp()
    {
        var clock = new FakeClock(); clock.OnDelay = () => { clock.Seconds += 30; };
        var instrument = new FakeInstrument(clock);
        var d = SequenceDocument.Example with { Steps = [SequenceDocument.Example.Steps[0] with { HoldSeconds = 100 }] };
        var result = await new SequenceRunner(instrument, clock, new FakeRecordings()).RunAsync(d, new(), 1, default);
        Assert.Equal(ExecutionOutcome.Failed, result.ExecutionOutcome); Assert.Contains("interruption", result.Error);
    }
    [Fact] public void HistoryIsBoundedAcrossLongSessions()
    {
        var history = new MeasurementHistory(); for (int i = 0; i < 7200; i++) history.Add(TestModels.Pair(i));
        Assert.InRange(history.Snapshot().Length, 3600, 3602); Assert.True(history.Snapshot()[0].OldestMonotonicSeconds >= 3599);
    }
    [Fact] public async Task OrdinaryMonitoringPauseFinishesBoundedReadWithoutAbortingWireTransaction()
    {
        var clock = new FakeClock(); using var cancel = new CancellationTokenSource(); var instrument = new FakeInstrument(clock);
        instrument.AfterCall = cmd => { if (cmd == "measure") cancel.Cancel(); };
        bool published = false;
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => MonitorLoop.RunAsync(instrument, clock, () => 1, _ => published = true, () => { }, cancel.Token));
        Assert.False(instrument.QueryAborted); Assert.False(published);
    }
}
