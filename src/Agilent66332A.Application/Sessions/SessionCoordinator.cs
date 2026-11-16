using Agilent66332A.Application.Monitoring;
using Agilent66332A.Application.Runs;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Core.Sequences;

namespace Agilent66332A.Application.Sessions;

public sealed partial class SessionCoordinator(IMonotonicClock clock, IRecordingFactory recordings) : IAsyncDisposable
{
    private IInstrumentClient? instrument;
    private CancellationTokenSource? monitoring;
    private Task monitorTask = Task.CompletedTask;
    private CancellationTokenSource? runCancellation;
    private Task<RunResult>? runTask;
    private readonly SemaphoreSlim ownership = new(1, 1);
    private readonly object recordingGate = new();
    private readonly object controlGate = new();
    private IRecordingSession? manualRecording;
    private double recordingStarted;
    private long operationGeneration;
    public ConnectionState State { get; private set; }
    public ConnectionProfile Profile { get; private set; } = new();
    public DeviceSnapshot? Snapshot => instrument?.Snapshot;
    public string SourceKind => instrument?.SourceKind ?? "Disconnected";
    public bool CanConfigure => State == ConnectionState.Ready && instrument is { CanConfigure: true, CanMonitor: true } && !IsRunning && !IsFeatureActive;
    public bool CanMeasureDc => instrument is { CanMonitor: true, CanMeasureDc: true };
    public bool IsCompatibilityMode => instrument?.CanMonitor == false;
    public bool IsRunning => runTask is { IsCompleted: false };
    public bool IsShuttingDown => Volatile.Read(ref shutdownInProgress) != 0;
    public bool IsRecording { get { lock (recordingGate) return manualRecording is not null; } }
    public double PollSeconds { get; set; } = 1;
    public ShutdownResult? LastShutdown { get; private set; }
    public RunResult? LastRun { get; private set; }
    public MeasurementHistory History { get; } = new();
    public event Action? Changed;
    public event Action<MeasurementPair>? Sample;
    public event Action<StudioEvent>? Event;
    public event Action<RunProgress>? Progress;
    public event Action<string>? Trace;
    private void Notify() => Changed?.Invoke();
    private void Emit(string type, string message)
    {
        var entry = new StudioEvent(clock.UtcNow, clock.Seconds, type, message);
        Event?.Invoke(entry);
        lock (recordingGate) manualRecording?.Event(entry);
    }
    public async Task ConnectAsync(IInstrumentClient client, ConnectionProfile profile)
    {
        await ownership.WaitAsync().ConfigureAwait(false);
        try
        {
            if (State is not (ConnectionState.Disconnected or ConnectionState.Lost)) throw new InvalidOperationException("Disconnect before connecting.");
            await StopMonitoringAsync().ConfigureAwait(false);
            if (instrument is not null) await instrument.DisposeAsync().ConfigureAwait(false);
            instrument = client; instrument.Trace += message => Trace?.Invoke(message);
            Profile = profile; State = ConnectionState.Connecting; Notify();
            try
            {
                await instrument.ConnectAsync(profile).ConfigureAwait(false);
                State = ConnectionState.Ready; History.Clear(); LastShutdown = null;
                Emit("connected", $"{SourceKind}: {instrument.Snapshot!.Identity.Raw}");
                StartMonitoring();
            }
            catch { State = ConnectionState.Lost; throw; }
            finally { Notify(); }
        }
        finally { ownership.Release(); }
    }
    private void Publish(MeasurementPair sample)
    {
        History.Add(sample); Sample?.Invoke(sample);
        lock (recordingGate) manualRecording?.Append(sample, clock.Seconds - recordingStarted, null);
        Notify();
    }
    private void CheckRecordingHealth()
    {
        lock (recordingGate) if (manualRecording?.Failure is { } e) throw new IOException("Recording failed.", e);
    }
    private void StartMonitoring()
    {
        lock (controlGate)
        {
            if (instrument is not { CanMonitor: true } client || State != ConnectionState.Ready ||
                monitoring is not null || IsFeatureActive || IsRunning || shutdownInProgress != 0) return;
            monitoring = new();
            var token = monitoring.Token;
            monitorTask = Task.Run(async () =>
            {
                try { await MonitorLoop.RunAsync(client, clock, () => PollSeconds, Publish, CheckRecordingHealth, token, Notify).ConfigureAwait(false); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { }
                catch (Exception e)
                {
                    State = ConnectionState.Lost;
                    LastShutdown = await client.RequestOutputOffAsync("Monitoring failure: " + e.Message).ConfigureAwait(false);
                    try { Emit("failure", e.Message + " / " + LastShutdown.Outcome); } catch { /* UI remains available if recording has failed. */ }
                    await FinishManualRecordingAsync(ExecutionOutcome.Failed, LastShutdown).ConfigureAwait(false);
                    Notify();
                }
            });
        }
    }
    private async Task StopMonitoringAsync()
    {
        CancellationTokenSource? stopped;
        Task ending;
        lock (controlGate)
        {
            stopped = monitoring;
            stopped?.Cancel();
            ending = monitorTask;
        }
        await ending.ConfigureAwait(false);
        lock (controlGate)
        {
            if (ReferenceEquals(monitoring, stopped)) { stopped?.Dispose(); monitoring = null; }
        }
    }
    private async Task ManualAsync(Func<IInstrumentClient, Task> action, Func<IInstrumentClient, Task>? afterVerification = null)
    {
        if (IsRunning) throw new InvalidOperationException("Manual writes are locked during a run.");
        long expected = Interlocked.Read(ref operationGeneration);
        await ownership.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsRunning || !CanConfigure || IsShuttingDown || expected != Interlocked.Read(ref operationGeneration)) throw new InvalidOperationException("The session cannot accept this change.");
            await StopMonitoringAsync().ConfigureAwait(false);
            if (expected != Interlocked.Read(ref operationGeneration)) throw new OperationCanceledException();
            Task applying;
            lock (controlGate)
            {
                if (expected != operationGeneration) throw new OperationCanceledException();
                applying = action(instrument!);
            }
            await applying.ConfigureAwait(false);
            if (afterVerification is not null)
            {
                lock (controlGate)
                {
                    if (expected != operationGeneration) throw new OperationCanceledException();
                    applying = afterVerification(instrument!);
                }
                await applying.ConfigureAwait(false);
            }
            Emit("verified", "Command verified by readback.");
        }
        catch
        {
            if (instrument is not null)
            {
                LastShutdown = await instrument.RequestOutputOffAsync("Manual operation failed").ConfigureAwait(false);
                if (instrument.Snapshot?.Output == OutputState.Unknown) State = ConnectionState.Lost;
            }
            throw;
        }
        finally { StartMonitoring(); Notify(); ownership.Release(); }
    }
    public Task ApplyAsync(RequestedSettings target) => ManualAsync(c => c.ApplyAsync(target));
    public Task ApplyAndEnableAsync(RequestedSettings target) =>
        ManualAsync(c => c.ApplyAsync(target), c => c.SetOutputAsync(true));
    public Task OutputOnAsync() => ManualAsync(async c => { await c.SetOutputAsync(true).ConfigureAwait(false); });
    public Task ProtectionsAsync(ProtectionSettings target) => ManualAsync(async c => { await c.ApplyProtectionsAsync(target).ConfigureAwait(false); });
    public Task ClearFaultAsync() => ManualAsync(async c => { await c.ClearProtectionAsync().ConfigureAwait(false); });
    public async Task<ShutdownResult> OutputOffAsync(string reason)
    {
        Task<ShutdownResult> pending;
        lock (controlGate)
        {
            Interlocked.Increment(ref operationGeneration);
            runCancellation?.Cancel();
            featureCancellation?.Cancel();
            monitoring?.Cancel();
            if (instrument is null) return LastShutdown = new(reason, clock.UtcNow, null, ShutdownOutcome.Unverified, "No instrument is connected.");
            pending = instrument.RequestOutputOffAsync(reason); // latch before first await
            shutdownInProgress++;
        }
        Notify();
        try
        {
            var result = await pending.ConfigureAwait(false);
            LastShutdown = result;
            if (instrument?.IsSessionUsable != true || result.Outcome == ShutdownOutcome.Unverified && !IsCompatibilityMode)
                State = ConnectionState.Lost;
            try { Emit("shutdown", $"{reason}: {result.Outcome}; {result.Error}"); } catch { }
            await StopMonitoringAsync().ConfigureAwait(false);
            return result;
        }
        finally
        {
            lock (controlGate) shutdownInProgress--;
            StartMonitoring(); Notify();
        }
    }
    public async Task StartManualRecordingAsync(string name)
    {
        await ownership.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsRunning || IsRecording || State != ConnectionState.Ready || Snapshot is null || !CanMeasureDc)
                throw new InvalidOperationException("Connect with live DC measurements available and finish any existing recording or run first.");
            var recording = await recordings.StartAsync(new(name, SourceKind, Profile, Snapshot, PollSeconds)).ConfigureAwait(false);
            lock (recordingGate) { recordingStarted = clock.Seconds; manualRecording = recording; }
            Emit("recordingStarted", recording.DirectoryPath); Notify();
        }
        finally { ownership.Release(); }
    }
    public Task StopManualRecordingAsync() => FinishManualRecordingAsync(ExecutionOutcome.Completed, null);
    private async Task FinishManualRecordingAsync(ExecutionOutcome outcome, ShutdownResult? shutdown)
    {
        IRecordingSession? current;
        lock (recordingGate) { current = manualRecording; manualRecording = null; }
        if (current is null) return;
        RecordingOutcome result;
        try { result = await current.CompleteAsync(outcome, shutdown, Snapshot).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false); }
        catch { result = RecordingOutcome.Failed; }
        if (result != RecordingOutcome.Complete && instrument is not null)
        {
            LastShutdown = await instrument.RequestOutputOffAsync("Recording finalization failed").ConfigureAwait(false);
            Event?.Invoke(new(clock.UtcNow, clock.Seconds, "recordingFailed", "Recording incomplete; " + LastShutdown.Outcome));
        }
        await current.DisposeAsync().ConfigureAwait(false);
        Notify();
    }
    public async Task<RunResult> RunAsync(SequenceDocument document)
    {
        long expected = Interlocked.Read(ref operationGeneration);
        await ownership.WaitAsync().ConfigureAwait(false);
        try
        {
            if (IsRunning || IsRecording || !CanConfigure || !CanMeasureDc)
                throw new InvalidOperationException("Finish manual recording and connect a configurable session with live DC measurements before running.");
            await StopMonitoringAsync().ConfigureAwait(false);
            var runner = new SequenceRunner(instrument!, clock, recordings);
            runner.Sample += Publish; runner.Event += entry => Event?.Invoke(entry); runner.Progress += p => Progress?.Invoke(p);
            lock (controlGate)
            {
                if (expected != operationGeneration) throw new OperationCanceledException("Run preparation was cancelled by Output Off.");
                runCancellation = new();
                var runToken = runCancellation.Token;
                runTask = Task.Run(() => runner.RunAsync(document, Profile, PollSeconds, runToken));
            }
            Notify();
        }
        finally { ownership.Release(); }
        try
        {
            LastRun = await runTask.ConfigureAwait(false);
            LastShutdown = LastRun.Shutdown;
            if (LastShutdown.Outcome == ShutdownOutcome.Unverified || LastRun.ExecutionOutcome == ExecutionOutcome.Failed) State = ConnectionState.Lost;
            return LastRun;
        }
        finally
        {
            lock (controlGate) { runCancellation?.Dispose(); runCancellation = null; }
            StartMonitoring(); Notify();
        }
    }
    public async Task DisconnectAsync()
    {
        await OutputOffAsync("Disconnect / exit").ConfigureAwait(false);
        if (runTask is { } active) await active.ConfigureAwait(false);
        await ownership.WaitAsync().ConfigureAwait(false);
        try
        {
            await StopMonitoringAsync().ConfigureAwait(false);
            await FinishManualRecordingAsync(ExecutionOutcome.Cancelled, LastShutdown).ConfigureAwait(false);
            if (instrument is not null) await instrument.DisconnectAsync().ConfigureAwait(false);
            State = ConnectionState.Disconnected; Notify();
        }
        finally { ownership.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        if (instrument is not null) await instrument.DisposeAsync().ConfigureAwait(false);
        ownership.Dispose();
    }
}
