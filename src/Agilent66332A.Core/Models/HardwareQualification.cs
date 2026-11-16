namespace Agilent66332A.Core.Models;

public sealed record HardwareQualification(
    NumericRange Voltage,
    NumericRange Current,
    NumericRange Ovp,
    NumericRange OcpDelay,
    bool BreakValidated,
    QualifiedFeature Features = QualifiedFeature.None);
