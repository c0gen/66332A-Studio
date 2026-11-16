using Agilent66332A.Core.Models;

namespace Agilent66332A.Core.Abstractions;

public interface IInstrumentDiagnostics
{
    Task<StatusRegisterSnapshot> ReadDiagnosticsAsync(CancellationToken token = default);
    Task<StatusRegisterSnapshot> ConfigureStatusAsync(InstrumentStatusSettings settings, CancellationToken token = default);
    Task<StatusRegisterSnapshot> PresetStatusAsync(CancellationToken token = default);
    Task<StatusRegisterSnapshot> ClearStatusAsync(CancellationToken token = default);
    Task<StatusEventReading> ReadAndAcknowledgeStatusEventAsync(StatusEventRegister register, CancellationToken token = default);
    Task<DeviceErrorBatch> DrainDeviceErrorsAsync(CancellationToken token = default);
}
