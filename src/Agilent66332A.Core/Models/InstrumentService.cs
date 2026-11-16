namespace Agilent66332A.Core.Models;

public enum ProgrammingLanguage { Unknown, Scpi, Compatibility }
public enum PowerOnMode { Reset, RecallSlotZero }
public enum CalibrationProcedure { Voltage, Overvoltage, PositiveCurrent, NegativeCurrent, LowRangeCurrent, AcCurrent }
public enum CalibrationPromptKind { Fixture, ReferenceReading, Save }

public sealed record InstrumentServiceSnapshot(ProgrammingLanguage Language, PowerOnMode? PowerOn,
    bool? CalibrationEnabled, DateTimeOffset ObservedUtc, bool? CompatibilityRelayPresent = null);
public sealed record CompatibilityReading(InstrumentIdentity Identity, double VoltageV, double CurrentA,
    int StatusRegister, DateTimeOffset ObservedUtc);
public sealed record CompatibilityEvents(int AccumulatedStatus, int Fault, int ErrorCode, DateTimeOffset ObservedUtc);
public sealed record CompatibilityCommandResult(int ErrorCode, DateTimeOffset CompletedUtc, bool CommandSent = true)
{
    public bool Accepted => ErrorCode == 0 && CommandSent;
}
public sealed record CompatibilitySelfTestResult(int TestCode, int ErrorCode, DateTimeOffset CompletedUtc)
{
    public bool Passed => TestCode == 0 && ErrorCode == 0;
}

public sealed record CalibrationPrompt(CalibrationPromptKind Kind, string Instruction, string? Unit = null);
public sealed record CalibrationAnswer(bool Confirmed, double? Reading = null);
public sealed record CalibrationResult(CalibrationProcedure Procedure, bool Saved, DateTimeOffset CompletedUtc);
