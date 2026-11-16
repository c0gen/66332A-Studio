using Agilent66332A.Core.Abstractions;

namespace Agilent66332A.Storage.Recordings;

public sealed class RecordingFactory(Func<string> root) : IRecordingFactory
{
    public async Task<IRecordingSession> StartAsync(RecordingContext context, CancellationToken token = default)
    {
        using var preflight = CancellationTokenSource.CreateLinkedTokenSource(token);
        preflight.CancelAfter(TimeSpan.FromSeconds(3));
        var start = Task.Run<IRecordingSession>(async () => await FileRecordingSession.CreateAsync(root(), context, preflight.Token).ConfigureAwait(false), preflight.Token);
        return await start.WaitAsync(TimeSpan.FromSeconds(4), token).ConfigureAwait(false);
    }
}
