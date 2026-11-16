using Agilent66332A.Core.Models;

namespace Agilent66332A.Core.Abstractions;

public interface IInstrumentService
{
    ProgrammingLanguage Language { get; }
    Task<InstrumentServiceSnapshot> ReadServiceAsync(CancellationToken token = default);
    Task<SelfTestResult> RunSelfTestAsync(CancellationToken token = default);
    Task<DeviceSnapshot> ResetInstrumentAsync(CancellationToken token = default);
    Task SaveDeviceStateAsync(int slot, CancellationToken token = default);
    Task<DeviceSnapshot> RecallDeviceStateAsync(int slot, bool fixtureIsolated, CancellationToken token = default);
    Task<InstrumentServiceSnapshot> ConfigurePowerOnAsync(PowerOnMode mode, DeviceSnapshot? reviewedState = null, CancellationToken token = default);
    Task<InstrumentServiceSnapshot> SetProgrammingLanguageAsync(ProgrammingLanguage language, bool fixtureIsolated, CancellationToken token = default);
    Task<CompatibilityReading> ReadCompatibilityAsync(CancellationToken token = default);
    Task<CompatibilityEvents> AcknowledgeCompatibilityEventsAsync(CancellationToken token = default);
    Task<int> ReadCompatibilityErrorAsync(CancellationToken token = default);
    Task<CompatibilityCommandResult> SetCompatibilityFaultMaskAsync(int mask, CancellationToken token = default);
    Task<CompatibilityCommandResult> SetCompatibilityServiceRequestsAsync(bool enabled, CancellationToken token = default);
    Task<CompatibilityCommandResult> SetCompatibilityPowerOnRequestAsync(bool enabled, CancellationToken token = default);
    Task<CompatibilityCommandResult> SetCompatibilityRelayAtPowerOnAsync(bool closeRelay, bool fixtureIsolated, CancellationToken token = default);
    Task<CompatibilitySelfTestResult> RunCompatibilitySelfTestAsync(bool fixtureIsolated, CancellationToken token = default);
    Task<CompatibilityCommandResult> ClearCompatibilityAsync(bool fixtureIsolated, CancellationToken token = default);
    Task<CalibrationResult> RunCalibrationAsync(CalibrationProcedure procedure, decimal? passcode,
        Func<CalibrationPrompt, CancellationToken, Task<CalibrationAnswer>> prompt, CancellationToken token = default);
    Task ChangeCalibrationPasscodeAsync(decimal? currentPasscode, decimal newPasscode, CancellationToken token = default);
}
