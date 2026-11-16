using System.ComponentModel;
using System.Runtime.InteropServices;
using Agilent66332A.Core.Models;
using Microsoft.Win32.SafeHandles;

namespace Agilent66332A.Instruments.Serial;

internal sealed class WindowsDtrDsrTransport : IByteTransport
{
    private const uint GenericReadWrite = 0xC0000000;
    private const uint OpenExisting = 3;
    private const uint Binary = 1, CheckParity = 1 << 1, DsrOutputFlow = 1 << 3;
    private const uint DtrHandshake = 2 << 4, RtsEnable = 1 << 12;
    private const uint PurgeTx = 0x0005, PurgeRx = 0x000A;

    private SafeFileHandle? port;
    private SafeFileHandle OpenPort => port ?? throw new IOException("Port is closed.");
    public bool IsOpen => port is { IsClosed: false, IsInvalid: false };

    public void Open(ConnectionProfile profile)
    {
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("DTR/DSR flow control requires Windows.");
        if (profile.FlowControl != SerialFlowControl.DtrDsr) throw new ArgumentException("Select DTR/DSR flow control.");
        string name = profile.PortName;
        if (name.Length < 4 || !name.StartsWith("COM", StringComparison.OrdinalIgnoreCase) ||
            name.AsSpan(3).IndexOfAnyExceptInRange('0', '9') >= 0)
            throw new ArgumentException("DTR/DSR requires a Windows COM port name.");

        port = CreateFileW($@"\\.\{name}", GenericReadWrite, 0, 0, OpenExisting, 0, 0);
        Check(!port.IsInvalid, $"Could not open {name}");

        var state = new DeviceControlBlock { Length = (uint)Marshal.SizeOf<DeviceControlBlock>() };
        Check(GetCommState(port, ref state), "Could not read serial configuration");
        state.BaudRate = (uint)profile.BaudRate;
        state.Flags = Binary | DsrOutputFlow | DtrHandshake |
            (profile.Parity == SerialParity.None ? 0 : CheckParity) | (profile.RtsEnabled ? RtsEnable : 0);
        state.ByteSize = (byte)profile.DataBits;
        state.Parity = profile.Parity switch
        {
            SerialParity.Odd => 1,
            SerialParity.Even => 2,
            SerialParity.Mark => 3,
            SerialParity.Space => 4,
            _ => 0
        };
        state.StopBits = 0; // ONESTOPBIT in the Windows DCB.
        state.XonChar = 17;
        state.XoffChar = 19;
        Check(SetCommState(port, ref state), "Could not set DTR/DSR serial configuration");

        // These read values return available data immediately or wait at most one slice for the first byte.
        var timeouts = new CommTimeouts
        {
            ReadInterval = uint.MaxValue,
            ReadTotalMultiplier = uint.MaxValue,
            ReadTotalConstant = SerialPortTransport.ReadSliceMilliseconds,
            WriteTotalConstant = (uint)SerialPortTransport.WriteTimeoutMilliseconds(profile)
        };
        Check(SetCommTimeouts(port, in timeouts), "Could not set serial timeouts");
    }

    public void Write(ReadOnlySpan<byte> bytes)
    {
        Check(WriteFile(OpenPort, bytes.ToArray(), (uint)bytes.Length, out uint written, 0), "Serial write failed");
        if (written != bytes.Length) throw new TimeoutException("Serial write did not finish before its deadline.");
    }

    public int Read(Span<byte> buffer)
    {
        var scratch = new byte[buffer.Length];
        Check(ReadFile(OpenPort, scratch, (uint)scratch.Length, out uint count, 0), "Serial read failed");
        if (count == 0) throw new TimeoutException("No serial data arrived in the read interval.");
        scratch.AsSpan(0, (int)count).CopyTo(buffer);
        return (int)count;
    }

    public void SendBreak(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        var current = OpenPort;
        Check(PurgeComm(current, PurgeTx), "Could not clear pending serial writes");
        Check(SetCommBreak(current), "Could not assert serial Break");
        try { if (token.WaitHandle.WaitOne(SerialPortTransport.BreakMilliseconds)) token.ThrowIfCancellationRequested(); }
        finally { Check(ClearCommBreak(current), "Could not clear serial Break"); }
    }

    public void DiscardInput() => Check(PurgeComm(OpenPort, PurgeRx), "Could not discard serial input");

    public void Close()
    {
        var current = port;
        port = null;
        current?.Dispose();
    }

    public void Dispose() => Close();

    private static void Check(bool success, string operation)
    {
        if (success) return;
        var cause = new Win32Exception(Marshal.GetLastPInvokeError());
        throw new IOException($"{operation}. {cause.Message}", cause);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DeviceControlBlock
    {
        public uint Length, BaudRate, Flags;
        public ushort Reserved, XonLimit, XoffLimit;
        public byte ByteSize, Parity, StopBits, XonChar, XoffChar, ErrorChar, EofChar, EventChar;
        public ushort Reserved1;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct CommTimeouts
    {
        public uint ReadInterval, ReadTotalMultiplier, ReadTotalConstant, WriteTotalMultiplier, WriteTotalConstant;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, nint security, uint mode, uint flags, nint template);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool GetCommState(SafeFileHandle file, ref DeviceControlBlock state);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool SetCommState(SafeFileHandle file, ref DeviceControlBlock state);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool SetCommTimeouts(SafeFileHandle file, in CommTimeouts timeouts);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool ReadFile(SafeFileHandle file, [Out] byte[] buffer, uint size, out uint count, nint overlapped);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool WriteFile(SafeFileHandle file, byte[] buffer, uint size, out uint count, nint overlapped);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool SetCommBreak(SafeFileHandle file);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool ClearCommBreak(SafeFileHandle file);

    [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
    private static extern bool PurgeComm(SafeFileHandle file, uint flags);
}
