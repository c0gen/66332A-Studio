using Agilent66332A.Core.Models;

namespace Agilent66332A.Core.Abstractions;

public interface IInstrumentClient : IAsyncDisposable
{
    string SourceKind { get; }
    bool CanConfigure { get; }
    bool CanMonitor => true;
    bool CanMeasureDc => true;
    bool IsSessionUsable => true;
    DeviceSnapshot? Snapshot { get; }
    event Action<string>? Trace;
    Task<DeviceSnapshot> ConnectAsync(ConnectionProfile profile, CancellationToken token = default);
    Task<DeviceSnapshot> ReadSnapshotAsync(CancellationToken token = default);
    Task<MeasurementPair> MeasureAsync(CancellationToken token = default);
    Task<ApplyResult> ApplyAsync(RequestedSettings settings, CancellationToken token = default);
    Task<DeviceSnapshot> SetOutputAsync(bool enabled, CancellationToken token = default);
    Task<DeviceSnapshot> ApplyProtectionsAsync(ProtectionSettings settings, CancellationToken token = default);
    Task<DeviceSnapshot> ClearProtectionAsync(CancellationToken token = default);
    // This path must invalidate queued control work synchronously, before returning its Task.
    Task<ShutdownResult> RequestOutputOffAsync(string reason);
    Task DisconnectAsync();
}
