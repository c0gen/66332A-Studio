using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Studio.Tests;

namespace Agilent66332A.Application.Tests;
public sealed class FakeInstrument(FakeClock clock) : IInstrumentClient
{
    public string SourceKind => "Simulator";
    public bool CanConfigure => true;
    public DeviceSnapshot? Snapshot { get; private set; } = TestModels.State;
    public event Action<string>? Trace { add { } remove { } }
    public List<(string Command, double Time)> Calls { get; } = [];
    public Action<string>? AfterCall { get; set; }
    public bool FailShutdown { get; set; }
    public bool ExternalChange { get; set; }
    public bool QueryAborted { get; private set; }
    private void Call(string text, CancellationToken token = default) { token.ThrowIfCancellationRequested(); Calls.Add((text, clock.Seconds)); clock.Seconds += .15; AfterCall?.Invoke(text); token.ThrowIfCancellationRequested(); }
    public Task<DeviceSnapshot> ConnectAsync(ConnectionProfile profile, CancellationToken token = default) => ReadSnapshotAsync(token);
    public Task<DeviceSnapshot> ReadSnapshotAsync(CancellationToken token = default) { Call("snapshot", token); return Task.FromResult(Snapshot!); }
    public Task<MeasurementPair> MeasureAsync(CancellationToken token = default)
    {
        try { Call("measure", token); } catch (OperationCanceledException) { QueryAborted = true; throw; }
        var state = ExternalChange ? Snapshot! with { Settings = Snapshot!.Settings with { VoltageV = 9 } } : Snapshot!;
        return Task.FromResult(TestModels.Pair(clock.Seconds) with { Snapshot = state });
    }
    public Task<ApplyResult> ApplyAsync(RequestedSettings target, CancellationToken token = default)
    {
        Call($"apply {target.VoltageV}", token); Snapshot = Snapshot! with { Settings = new(target.VoltageV, target.CurrentLimitA, clock.UtcNow) };
        return Task.FromResult(new ApplyResult(target, Snapshot.Settings));
    }
    public Task<DeviceSnapshot> SetOutputAsync(bool enabled, CancellationToken token = default)
    { Call(enabled ? "on" : "off", token); Snapshot = Snapshot! with { Output = enabled ? OutputState.On : OutputState.Off }; return Task.FromResult(Snapshot); }
    public Task<ShutdownResult> RequestOutputOffAsync(string reason)
    {
        Call("shutdown"); Snapshot = Snapshot! with { Output = FailShutdown ? OutputState.Unknown : OutputState.Off };
        return Task.FromResult(new ShutdownResult(reason, clock.UtcNow, FailShutdown ? null : clock.UtcNow, FailShutdown ? ShutdownOutcome.Unverified : ShutdownOutcome.VerifiedOff));
    }
    public Task<DeviceSnapshot> ApplyProtectionsAsync(ProtectionSettings settings, CancellationToken token = default) => ReadSnapshotAsync(token);
    public Task<DeviceSnapshot> ClearProtectionAsync(CancellationToken token = default) => ReadSnapshotAsync(token);
    public Task DisconnectAsync() => Task.CompletedTask;
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
public sealed class FakeRecordings : IRecordingFactory
{
    public FakeRecording Session { get; } = new();
    public bool FailPreflight { get; set; }
    public RecordingContext? Context { get; private set; }
    public Task<IRecordingSession> StartAsync(RecordingContext context, CancellationToken token = default)
    {
        Context = context;
        return FailPreflight ? Task.FromException<IRecordingSession>(new IOException("Disk unavailable")) : Task.FromResult<IRecordingSession>(Session);
    }
}
public sealed class FakeRecording : IRecordingSession
{
    public string DirectoryPath => "fake";
    public Exception? Failure { get; set; }
    public int Samples { get; private set; }
    public bool FailAppend { get; set; }
    public bool FailFinalization { get; set; }
    public List<StudioEvent> Events { get; } = [];
    public void Append(MeasurementPair sample, double elapsedSeconds, RunPosition? position) { Samples++; if (FailAppend) throw new IOException("Disk full"); }
    public void Event(StudioEvent entry) => Events.Add(entry);
    public Task<RecordingOutcome> CompleteAsync(ExecutionOutcome execution, ShutdownResult? shutdown, DeviceSnapshot? final, CancellationToken token = default) => Task.FromResult(FailFinalization ? RecordingOutcome.Failed : RecordingOutcome.Complete);
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
