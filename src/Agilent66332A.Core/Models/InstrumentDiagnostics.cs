namespace Agilent66332A.Core.Models;

public enum StatusEventRegister { Operation, Questionable, Standard }

public sealed record StatusGroupMasks(int Enable, int PositiveTransitions, int NegativeTransitions);
public sealed record InstrumentStatusSettings(StatusGroupMasks Operation, StatusGroupMasks Questionable,
    int StandardEventEnable, int ServiceRequestEnable, bool ClearEnablesAtPowerOn);
public sealed record StatusRegisterSnapshot(int OperationCondition, int QuestionableCondition, int StatusByte,
    InstrumentStatusSettings Settings, DateTimeOffset ObservedUtc);
public sealed record StatusEventReading(StatusEventRegister Register, int Value, DateTimeOffset ObservedUtc);
public sealed record DeviceErrorReading(int Code, string Message, DateTimeOffset ObservedUtc);
public sealed record DeviceErrorBatch(IReadOnlyList<DeviceErrorReading> Errors, bool LimitReached);
public sealed record SelfTestResult(int Code, DeviceErrorBatch Errors, DateTimeOffset CompletedUtc)
{
    public bool Passed => Code == 0;
}
