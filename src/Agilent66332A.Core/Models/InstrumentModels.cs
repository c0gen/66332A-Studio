namespace Agilent66332A.Core.Models;

public enum OutputState { Unknown, Off, On }
public enum ConnectionState { Disconnected, Connecting, Ready, Lost }
public enum SampleQuality { Valid, Missing, Interrupted }
public enum ShutdownOutcome { VerifiedOff, Unverified }
public enum ExecutionOutcome { Completed, Cancelled, Failed }
public enum RecordingOutcome { Complete, Incomplete, Failed }

public sealed record InstrumentIdentity(string Manufacturer, string Model, string SerialNumber, string Firmware, string Raw);
public sealed record NumericRange(double Minimum, double Maximum, double Resolution)
{
    public bool Contains(double value) => double.IsFinite(value) && value >= Minimum && value <= Maximum;
    public bool Accepts(double requested, double actual) => Contains(actual) && Math.Abs(requested - actual) <= Resolution / 2 + 1e-10;
}
public sealed record DeviceCapabilities(NumericRange Voltage, NumericRange Current, NumericRange Ovp, NumericRange OcpDelay, bool HardwareValidated)
{
    public QualifiedFeature QualifiedFeatures { get; init; }
}
public sealed record RequestedSettings(double VoltageV, double CurrentLimitA);
public sealed record AcknowledgedSettings(double VoltageV, double CurrentLimitA, DateTimeOffset VerifiedUtc);
public sealed record ProtectionSettings(double OvpV, bool OcpEnabled, double OcpDelaySeconds);
public sealed record InstrumentStatus(string RegulationMode, int OperationCondition, int QuestionableCondition, string FaultFlags, DateTimeOffset VerifiedUtc)
{
    public bool HasFault => FaultFlags.Length > 0;
    public bool WaitingForTrigger => (OperationCondition & 32) != 0;
    public bool CalibrationInProgress => (OperationCondition & 1) != 0;
    public bool MeasurementOverload => (QuestionableCondition & 16384) != 0;
}
public sealed record DeviceSnapshot(long Generation, InstrumentIdentity Identity, DeviceCapabilities Capabilities,
    AcknowledgedSettings Settings, ProtectionSettings Protections, OutputState Output, InstrumentStatus Status, DateTimeOffset ObservedUtc);
public sealed record MeasurementSample(double? Value, DateTimeOffset StartedUtc, DateTimeOffset CompletedUtc, double MonotonicSeconds);
public sealed record MeasurementPair(long Generation, long Cycle, MeasurementSample Voltage, MeasurementSample Current,
    SampleQuality Quality, DeviceSnapshot Snapshot)
{
    public double? EstimatedPowerW => Quality == SampleQuality.Valid && Voltage.Value is { } v && Current.Value is { } i ? v * i : null;
    public double PairSkewSeconds => (Current.CompletedUtc - Voltage.CompletedUtc).TotalSeconds;
    public double OldestMonotonicSeconds => Math.Min(Voltage.MonotonicSeconds, Current.MonotonicSeconds);
}
public sealed record ApplyResult(RequestedSettings Requested, AcknowledgedSettings Acknowledged);
public sealed record ShutdownResult(string Reason, DateTimeOffset RequestedUtc, DateTimeOffset? VerifiedUtc, ShutdownOutcome Outcome, string? Error = null);
public sealed record StudioEvent(DateTimeOffset Utc, double ElapsedSeconds, string Type, string Message, string? StepId = null);
