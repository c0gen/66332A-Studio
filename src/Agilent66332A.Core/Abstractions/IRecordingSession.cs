using Agilent66332A.Core.Models;
using Agilent66332A.Core.Sequences;

namespace Agilent66332A.Core.Abstractions;

public sealed record RunPosition(string StepId, int RowIndex, int RepeatIndex, string Phase, RequestedSettings Requested, bool OutputEnabled);
public sealed record RunResult(ExecutionOutcome ExecutionOutcome, ShutdownResult Shutdown, RecordingOutcome RecordingOutcome, string? Error);
public sealed record RecordingContext(string Name, string SourceKind, ConnectionProfile Profile, DeviceSnapshot InitialSnapshot,
    double PollSeconds, SequenceDocument? Sequence = null);
public interface IRecordingSession : IAsyncDisposable
{
    string DirectoryPath { get; }
    Exception? Failure { get; }
    void Append(MeasurementPair sample, double elapsedSeconds, RunPosition? position);
    void Event(StudioEvent entry);
    Task<RecordingOutcome> CompleteAsync(ExecutionOutcome execution, ShutdownResult? shutdown, DeviceSnapshot? final, CancellationToken token = default);
}
public interface IRecordingFactory
{
    Task<IRecordingSession> StartAsync(RecordingContext context, CancellationToken token = default);
}
