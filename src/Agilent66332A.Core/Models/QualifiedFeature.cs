namespace Agilent66332A.Core.Models;

[Flags]
public enum QualifiedFeature
{
    None = 0,
    Acquisition = 1 << 0,
    AcquisitionImmediate = 1 << 1,
    AcquisitionBus = 1 << 2,
    TransientImmediate = 1 << 3,
    TransientBus = 1 << 4,
    Diagnostics = 1 << 5,
    DigitalIo = 1 << 6,
    Relay = 1 << 7,
    Service = 1 << 8,
    Compatibility = 1 << 9,
    Calibration = 1 << 10,
    All = Acquisition | AcquisitionImmediate | AcquisitionBus | TransientImmediate | TransientBus |
          Diagnostics | DigitalIo | Relay | Service | Compatibility | Calibration
}
