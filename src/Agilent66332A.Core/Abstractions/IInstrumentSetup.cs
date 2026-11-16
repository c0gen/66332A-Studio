using Agilent66332A.Core.Models;

namespace Agilent66332A.Core.Abstractions;

public interface IInstrumentSetup
{
    Task<InstrumentSetupSnapshot> ReadInstrumentSetupAsync(CancellationToken token = default);
    Task<InstrumentSetupSnapshot> ConfigureDigitalPortAsync(DigitalPortSettings settings, CancellationToken token = default);
    Task<DigitalPortReading> WriteDigitalPortAsync(int outputMask, CancellationToken token = default);
    Task<InstrumentSetupSnapshot> ConfigureRelayAsync(RelaySettings settings, CancellationToken token = default);
    Task<DeviceSnapshot> SetOutputWithoutRelayAsync(bool enabled, CancellationToken token = default);
    Task<InstrumentSetupSnapshot> ConfigurePanelDisplayAsync(PanelDisplaySettings settings, CancellationToken token = default);
    Task SetPanelControlAsync(PanelControlMode mode, CancellationToken token = default);
}
