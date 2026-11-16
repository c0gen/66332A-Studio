using Agilent66332A.Core.Models;

namespace Agilent66332A.Core.Abstractions;

public interface IAcquisitionClient
{
    Task<AcquisitionSettings> ReadAcquisitionAsync(CancellationToken token = default);
    Task<AcquisitionSettings> ApplyAcquisitionAsync(AcquisitionSettings settings, CancellationToken token = default);
    Task<WaveformCapture> CaptureAsync(CaptureRequest request, IProgress<CaptureProgress>? progress = null,
        Func<bool>? takeFireRequest = null, CancellationToken token = default);
}

public interface ITransientClient
{
    Task<TransientSettings> ReadTransientAsync(CancellationToken token = default);
    Task<DeviceSnapshot> RunTransientAsync(TransientRequest request, Func<bool> takeFireRequest,
        IProgress<TransientProgress>? progress = null, CancellationToken token = default);
    Task<TransientSettings> AbortTriggersAsync(CancellationToken token = default);
}
