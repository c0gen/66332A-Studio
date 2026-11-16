using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Core.Sequences;

namespace Agilent66332A.Application.Runs;

public sealed record RunProgress(string Phase, int Row, int Repeat, double HoldDeadline, double StartedSeconds, string Message);

public sealed class SequenceRunner(IInstrumentClient instrument, IMonotonicClock clock, IRecordingFactory recordings)
{
    public event Action<RunProgress>? Progress;
    public event Action<MeasurementPair>? Sample;
    public event Action<StudioEvent>? Event;
    public string? RecordingDirectory { get; private set; }

    public async Task<RunResult> RunAsync(SequenceDocument input, ConnectionProfile profile, double pollSeconds, CancellationToken token)
    {
        var document = input;
        var execution = ExecutionOutcome.Failed;
        IRecordingSession? recording = null;
        string? error = null;
        double started = clock.Seconds;
        void Emit(string type, string message, string? stepId = null)
        {
            var entry = new StudioEvent(clock.UtcNow, clock.Seconds - started, type, message, stepId);
            Event?.Invoke(entry); recording?.Event(entry);
        }
        try
        {
            var structureErrors = SequenceValidator.Validate(input);
            if (structureErrors.Count > 0) throw new ArgumentException(string.Join("\n", structureErrors));
            document = input.Freeze();
            var state = await instrument.ReadSnapshotAsync(token).ConfigureAwait(false);
            var errors = SequenceValidator.Validate(document, state);
            if (errors.Count > 0) throw new ArgumentException(string.Join("\n", errors));
            if (!instrument.CanConfigure) throw new InvalidOperationException("Configuration is unavailable for this session.");
            Progress?.Invoke(new("Preparing", 0, 0, 0, started, "Opening automatic recording"));
            recording = await recordings.StartAsync(new(document.Name, instrument.SourceKind, profile, state, pollSeconds, document), token).ConfigureAwait(false);
            RecordingDirectory = recording.DirectoryPath;
            token.ThrowIfCancellationRequested();
            await instrument.SetOutputAsync(false, token).ConfigureAwait(false);
            Emit("prepared", "Output off verified; recording preflight passed.");
            for (int repeat = 1; repeat <= document.RepeatCount; repeat++)
            for (int row = 0; row < document.Steps.Length; row++)
            {
                token.ThrowIfCancellationRequested();
                var step = document.Steps[row];
                Progress?.Invoke(new("Transitioning", row + 1, repeat, 0, started, step.Label));
                Emit("transition", $"Row {row + 1}, repeat {repeat}", step.Id);
                if (!step.OutputEnabled) await instrument.SetOutputAsync(false, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                var target = new RequestedSettings(step.VoltageV, step.CurrentLimitA);
                await instrument.ApplyAsync(target, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                state = await instrument.SetOutputAsync(step.OutputEnabled, token).ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                var expected = state;
                double holdStart = clock.Seconds;
                double deadline = holdStart + step.HoldSeconds;
                double nextPoll = holdStart;
                double lastWake = holdStart;
                Emit("holdStarted", $"Confirmed state; hold {step.HoldSeconds:G} s.", step.Id);
                Progress?.Invoke(new("Holding", row + 1, repeat, deadline, started, step.Label));
                var position = new RunPosition(step.Id, row + 1, repeat, "Holding", target, step.OutputEnabled);
                while (true)
                {
                    token.ThrowIfCancellationRequested();
                    if (recording.Failure is { } failure) throw new IOException("Automatic recording failed.", failure);
                    if (clock.Seconds - lastWake > Math.Max(5, pollSeconds * 3)) throw new IOException("Scheduling interruption detected; reconnect before running again.");
                    lastWake = clock.Seconds;
                    if (clock.Seconds >= deadline) break;
                    if (clock.Seconds >= nextPoll)
                    {
                        var sample = await instrument.MeasureAsync(token).ConfigureAwait(false);
                        Sample?.Invoke(sample);
                        recording.Append(sample, clock.Seconds - started, position);
                        var actual = sample.Snapshot;
                        if (sample.Quality != SampleQuality.Valid || actual.Status.HasFault || actual.Output != expected.Output ||
                            actual.Settings.VoltageV != expected.Settings.VoltageV || actual.Settings.CurrentLimitA != expected.Settings.CurrentLimitA || actual.Protections != expected.Protections)
                            throw new IOException("Instrument state changed unexpectedly during the run.");
                        nextPoll = clock.Seconds + pollSeconds;
                        continue;
                    }
                    await clock.DelayAsync(TimeSpan.FromSeconds(Math.Max(.001, Math.Min(.2, Math.Min(nextPoll, deadline) - clock.Seconds))), token).ConfigureAwait(false);
                }
                Emit("holdEnded", $"Actual hold {clock.Seconds - holdStart:G17} s.", step.Id);
            }
            execution = ExecutionOutcome.Completed;
        }
        catch (OperationCanceledException) { execution = ExecutionOutcome.Cancelled; error = "Run cancelled."; }
        catch (Exception e) { execution = ExecutionOutcome.Failed; error = e.Message; }

        Progress?.Invoke(new("Stopping", 0, 0, 0, started, error ?? "All rows finished; verifying output off"));
        // Shutdown is independent of recording I/O and never uses the cancelled run token.
        var shutdown = await instrument.RequestOutputOffAsync(error ?? "Sequence complete").ConfigureAwait(false);
        RecordingOutcome recordingOutcome = RecordingOutcome.Incomplete;
        if (recording is not null)
        {
            try { Emit("shutdown", $"{execution}; {shutdown.Outcome}; {shutdown.Error}"); }
            catch (Exception e) { error ??= e.Message; }
            Progress?.Invoke(new("Finalizing", 0, 0, 0, started, "Finalizing recording"));
            try { recordingOutcome = await recording.CompleteAsync(execution, shutdown, instrument.Snapshot).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
            catch (Exception e) { recordingOutcome = RecordingOutcome.Failed; error ??= e.Message; }
            await recording.DisposeAsync().ConfigureAwait(false);
        }
        Progress?.Invoke(new("Finished", 0, 0, 0, started, $"{execution} / {shutdown.Outcome} / recording {recordingOutcome}"));
        return new(execution, shutdown, recordingOutcome, error);
    }
}
