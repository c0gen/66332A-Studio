using System.IO.Ports;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Instruments.Serial;

public sealed class SerialPortTransport : IByteTransport
{
    internal const int ReadSliceMilliseconds = 75;
    internal const int BreakMilliseconds = 100;

    private IByteTransport? active;
    private IByteTransport OpenTransport => active ?? throw new IOException("Port is closed.");
    public bool IsOpen => active?.IsOpen == true;
    public static string[] GetPortNames() => SerialPort.GetPortNames().Order(StringComparer.OrdinalIgnoreCase).ToArray();

    public void Open(ConnectionProfile profile)
    {
        if (active is not null) throw new InvalidOperationException("Port is already open.");
        profile.Validate();
        IByteTransport transport = profile.FlowControl == SerialFlowControl.DtrDsr
            ? new WindowsDtrDsrTransport()
            : new ManagedSerialPortTransport();
        try
        {
            transport.Open(profile);
            active = transport;
        }
        catch
        {
            transport.Dispose();
            throw;
        }
    }

    public void Write(ReadOnlySpan<byte> bytes) => OpenTransport.Write(bytes);
    public int Read(Span<byte> buffer) => OpenTransport.Read(buffer);
    public void SendBreak(CancellationToken token) => OpenTransport.SendBreak(token);
    public void DiscardInput() => OpenTransport.DiscardInput();

    public void Close()
    {
        var transport = active;
        active = null;
        transport?.Dispose();
    }

    public void Dispose() => Close();

    // Keep failed, flow-controlled writes bounded so Output Off can regain the worker promptly.
    internal static int WriteTimeoutMilliseconds(ConnectionProfile profile) => profile.BaudRate >= 2400 ? 500 : 1500;
}
