using System.IO.Ports;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Instruments.Serial;

internal sealed class ManagedSerialPortTransport : IByteTransport
{
    private SerialPort? port;
    private SerialPort OpenPort => port ?? throw new IOException("Port is closed.");
    public bool IsOpen => port?.IsOpen == true;

    public void Open(ConnectionProfile profile)
    {
        var parity = profile.Parity switch
        {
            SerialParity.Even => Parity.Even,
            SerialParity.Odd => Parity.Odd,
            SerialParity.Mark => Parity.Mark,
            SerialParity.Space => Parity.Space,
            _ => Parity.None
        };
        var handshake = profile.FlowControl switch
        {
            SerialFlowControl.None => Handshake.None,
            SerialFlowControl.XonXoff => Handshake.XOnXOff,
            SerialFlowControl.RtsCts => Handshake.RequestToSend,
            _ => throw new ArgumentException("The selected flow control needs a native serial connection.")
        };

        port = new(profile.PortName, profile.BaudRate, parity, profile.DataBits, StopBits.One);
        port.Handshake = handshake;
        port.DtrEnable = profile.DtrEnabled;
        if (!profile.RtsControlledByHandshake) port.RtsEnable = profile.RtsEnabled;
        port.ReadTimeout = SerialPortTransport.ReadSliceMilliseconds;
        port.WriteTimeout = SerialPortTransport.WriteTimeoutMilliseconds(profile);
        port.ParityReplace = 0;
        port.NewLine = "\r\n";
        port.Open();
    }

    public void Write(ReadOnlySpan<byte> bytes) => OpenPort.Write(bytes.ToArray(), 0, bytes.Length);

    public int Read(Span<byte> buffer)
    {
        var scratch = new byte[buffer.Length];
        int count = OpenPort.Read(scratch, 0, scratch.Length);
        scratch.AsSpan(0, count).CopyTo(buffer);
        return count;
    }

    public void SendBreak(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var current = OpenPort;
        current.DiscardOutBuffer();
        current.BreakState = true;
        try { if (token.WaitHandle.WaitOne(SerialPortTransport.BreakMilliseconds)) token.ThrowIfCancellationRequested(); }
        finally { current.BreakState = false; }
    }

    public void DiscardInput() => OpenPort.DiscardInBuffer();

    public void Close()
    {
        var current = port;
        port = null;
        current?.Dispose();
    }

    public void Dispose() => Close();
}
