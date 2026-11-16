namespace Agilent66332A.Instruments.Serial;

// All transport access, including open/close, occurs on this single dedicated worker.
public sealed class CommandWorker : IAsyncDisposable
{
    private sealed record Work(Action Execute, Action<Exception> Reject, int Priority);
    private readonly object gate = new();
    private readonly PriorityQueue<Work, (int Priority, long Order)> queue = new();
    private readonly AutoResetEvent available = new(false);
    private readonly Task thread;
    private long order;
    private bool stopping;
    public CommandWorker() => thread = Task.Factory.StartNew(Loop, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
    public Task<T> Enqueue<T>(Func<T> action, int priority = 1)
    {
        var completion = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (gate)
        {
            if (stopping) return Task.FromException<T>(new ObjectDisposedException(nameof(CommandWorker)));
            if (queue.Count >= 128 && priority != 0) return Task.FromException<T>(new IOException("Instrument queue is full."));
            var work = new Work(() => { try { completion.TrySetResult(action()); } catch (Exception e) { completion.TrySetException(e); } },
                e => completion.TrySetException(e), priority);
            queue.Enqueue(work, (priority, order++));
        }
        available.Set();
        return completion.Task;
    }
    private void Loop()
    {
        while (true)
        {
            Work? next;
            lock (gate)
            {
                if (stopping && queue.Count == 0) return;
                next = queue.TryDequeue(out var work, out _) ? work : null;
            }
            if (next is null) available.WaitOne(); else next.Execute();
        }
    }
    public async ValueTask DisposeAsync()
    {
        lock (gate)
        {
            stopping = true;
            while (queue.TryDequeue(out var work, out _)) work.Reject(new ObjectDisposedException(nameof(CommandWorker)));
        }
        available.Set();
        await thread.ConfigureAwait(false);
        available.Dispose();
    }
}
