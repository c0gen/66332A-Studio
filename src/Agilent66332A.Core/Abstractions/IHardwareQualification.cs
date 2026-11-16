using Agilent66332A.Core.Models;

namespace Agilent66332A.Core.Abstractions;

public interface IHardwareQualification
{
    HardwareQualification? Match(InstrumentIdentity identity, ConnectionProfile connection);
}
