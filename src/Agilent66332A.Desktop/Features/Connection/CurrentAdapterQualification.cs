using System.IO;
using System.Security;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Desktop.Features.Connection;

internal sealed class CurrentAdapterQualification(IHardwareQualification evidence) : IHardwareQualification
{
    public HardwareQualification? Match(InstrumentIdentity identity, ConnectionProfile connection)
    {
        var qualification = evidence.Match(identity, connection);
        if (qualification is null || connection.Adapter is not { } openedAdapter) return null;

        // Check again after *IDN? in case the COM assignment changed between discovery and opening.
        try
        {
            var current = WindowsSerialPortDiscovery.GetPorts().FirstOrDefault(port =>
                StringComparer.OrdinalIgnoreCase.Equals(port.PortName, connection.PortName));
            return openedAdapter.Matches(current?.Adapter) ? qualification : null;
        }
        catch (Exception error) when (error is IOException or SecurityException or UnauthorizedAccessException) { return null; }
    }
}
