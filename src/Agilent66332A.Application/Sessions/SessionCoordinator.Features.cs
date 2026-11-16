namespace Agilent66332A.Application.Sessions;

public sealed partial class SessionCoordinator
{
    private CancellationTokenSource? featureCancellation;
    private int shutdownInProgress;

    public bool IsFeatureActive => Volatile.Read(ref featureCancellation) is not null;

    public bool CanUseFeature<TFeature>(bool requiresConfiguration) where TFeature : class =>
        State == Core.Models.ConnectionState.Ready && !IsRunning && !IsFeatureActive && instrument is TFeature &&
        (!requiresConfiguration || instrument.CanConfigure);

    public async Task<TResult> UseFeatureAsync<TFeature, TResult>(Func<TFeature, CancellationToken, Task<TResult>> action,
        bool requiresConfiguration, CancellationToken token = default) where TFeature : class
    {
        long generation = Interlocked.Read(ref operationGeneration);
        if (IsFeatureActive || IsRunning) throw new InvalidOperationException("Finish the active instrument operation first.");
        await ownership.WaitAsync(token).ConfigureAwait(false);
        CancellationTokenSource? operation = null;
        try
        {
            lock (controlGate)
            {
                if (generation != operationGeneration || shutdownInProgress != 0) throw new OperationCanceledException();
                if (!CanUseFeature<TFeature>(requiresConfiguration) || instrument is not TFeature feature)
                    throw new InvalidOperationException("The connected instrument cannot accept this operation.");
                operation = CancellationTokenSource.CreateLinkedTokenSource(token);
                featureCancellation = operation;
            }
            Notify();
            await StopMonitoringAsync().ConfigureAwait(false);
            operation.Token.ThrowIfCancellationRequested();
            if (State != Core.Models.ConnectionState.Ready || instrument is not TFeature client || !instrument.IsSessionUsable)
                throw new IOException("The instrument session changed. Reconnect before retrying.");
            return await action(client, operation.Token).ConfigureAwait(false);
        }
        finally
        {
            if (operation is not null)
            {
                lock (controlGate)
                {
                    featureCancellation = null;
                    operation.Dispose();
                    if (instrument?.IsSessionUsable != true) State = Core.Models.ConnectionState.Lost;
                }
                StartMonitoring();
                Notify();
            }
            ownership.Release();
        }
    }
}
