using System.Diagnostics;
using System.Text;
using Agilent66332A.Instruments.Serial;

namespace Agilent66332A.Instruments.Scpi;

public sealed class ScpiChannel(IByteTransport transport, Action<string> trace, object? dispatchGate = null)
{
    private readonly object writeGate = dispatchGate ?? new();
    private int baudRate = 9600;
    public bool Synchronized { get; private set; } = true;

    internal void Opened(int baud)
    {
        baudRate = baud;
        Synchronized = true;
    }

    public void Write(string command, CancellationToken token, bool sensitive = false)
    {
        lock (writeGate)
        {
            token.ThrowIfCancellationRequested();
            if (!Synchronized) throw new IOException("Protocol synchronization is lost; reconnect is required.");
            trace(sensitive ? $"> {command.Split(' ', 2)[0]} [redacted]" : $"> {command}");
            try { transport.Write(Encoding.ASCII.GetBytes(command + "\r\n")); }
            catch { Synchronized = false; throw; }
        }
    }
    public string Query(string command, CancellationToken token, double timeoutSeconds = 2)
    {
        string reply = QueryLine(command, token, timeoutSeconds + (baudRate < 4800 ? 2560d / baudRate : 0));
        trace($"< {reply}");
        return reply;
    }

    private string QueryLine(string command, CancellationToken token, double timeoutSeconds)
    {
        var framer = new LineFramer();
        string? reply = null;
        ReadResponse(command, token, timeoutSeconds, b => (reply = framer.Push(b)) is not null);
        return reply!;
    }

    public IReadOnlyList<double> QueryArray(string command, int expected, double acquisitionSeconds,
        CancellationToken token, Action<int>? report = null)
    {
        var response = new ArrayResponse(expected);
        // Both permitted serial word formats use ten wire bits per ASCII character.
        double wireSeconds = 10d * response.MaximumBytes / baudRate;
        ReadResponse(command, token, acquisitionSeconds + wireSeconds + Math.Max(3, wireSeconds / 5), b =>
        {
            bool completed = response.Push(b);
            if (completed || b == ',' && response.Count % 64 == 0) report?.Invoke(response.Count);
            return completed;
        });
        trace($"< [{response.Count} waveform samples]");
        return response.Samples;
    }

    internal void InterruptVoltageWaveform(int points, CancellationToken token)
    {
        var waveform = new ArrayResponse(points);
        ReadResponse("MEAS:ARR:VOLT?", token, 5, value =>
        {
            waveform.Push(value);
            return waveform.Count > 0;
        }, interrupt: true);
    }

    private void ReadResponse(string command, CancellationToken token, double timeoutSeconds, Func<byte, bool> push, bool interrupt = false)
    {
        Write(command, token);
        var watch = Stopwatch.StartNew();
        var buffer = new byte[256];
        try
        {
            while (watch.Elapsed.TotalSeconds < timeoutSeconds)
            {
                token.ThrowIfCancellationRequested();
                int count;
                try { count = transport.Read(buffer); } catch (TimeoutException) { continue; }
                bool completed = false;
                for (int i = 0; i < count; i++)
                {
                    if (completed) throw new IOException("Unsolicited bytes after SCPI reply; synchronization lost.");
                    completed = push(buffer[i]);
                    if (completed && interrupt)
                    {
                        Synchronized = false;
                        return;
                    }
                }
                if (completed) return;
            }
            throw new TimeoutException($"Timed out waiting for {command}.");
        }
        catch { Synchronized = false; throw; }
    }
    public void Recover(bool breakValidated, CancellationToken token, Action<CancellationToken>? barrier = null)
    {
        Synchronized = false;
        if (!breakValidated) throw new IOException("Break recovery is not validated for this adapter. Output state is unknown; reconnect explicitly.");
        transport.SendBreak(token);
        transport.DiscardInput();
        try
        {
            WaitForScpiMarker(token);
            if (barrier is null) ScpiParser.Identity(Query("*IDN?", token));
            else barrier(token);
        }
        catch { Synchronized = false; throw; }
    }

    private void WaitForScpiMarker(CancellationToken token)
    {
        // On A.01.03, the serial link can pause after Break and append its first reply to a waveform tail.
        // Only the complete language reply advances to identity verification. Never retry actuator writes here.
        for (int attempt = 0; attempt < 6; attempt++)
        {
            if (!DrainUntilQuiet(token)) continue;
            Synchronized = true;
            string? reply = null;
            try { reply = QueryLine("SYST:LANG?", token, Math.Max(.75, .35 + 200d / baudRate)); }
            catch (Exception error) when (error is TimeoutException or FormatException || error is IOException && transport.IsOpen)
            {
                trace("Waiting for a clean SCPI reply after serial Break.");
            }
            if (string.Equals(reply?.Trim(), "SCPI", StringComparison.OrdinalIgnoreCase))
            {
                trace("< SCPI");
                return;
            }
            if (string.Equals(reply?.Trim(), "COMP", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(reply?.Trim(), "COMPATIBILITY", StringComparison.OrdinalIgnoreCase))
                throw new IOException("The instrument entered Compatibility mode during recovery.");
            if (reply is not null) trace("Discarded an unrecognized reply after serial Break.");
            Synchronized = false;
        }
        throw new IOException("Serial Break did not establish a clean SCPI reply.");
    }

    private bool DrainUntilQuiet(CancellationToken token)
    {
        const int quietMs = 150, limitMs = 450;
        var watch = Stopwatch.StartNew();
        long lastData = 0;
        Span<byte> buffer = stackalloc byte[256];
        while (watch.ElapsedMilliseconds < limitMs)
        {
            token.ThrowIfCancellationRequested();
            try
            {
                if (transport.Read(buffer) > 0) lastData = watch.ElapsedMilliseconds;
            }
            catch (TimeoutException) { }
            if (watch.ElapsedMilliseconds - lastData >= quietMs) return true;
        }
        return false;
    }
}
