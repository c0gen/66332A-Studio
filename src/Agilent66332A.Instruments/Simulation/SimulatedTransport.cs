using System.Globalization;
using System.Text;
using Agilent66332A.Core.Models;
using Agilent66332A.Instruments.Scpi;
using Agilent66332A.Instruments.Serial;

namespace Agilent66332A.Instruments.Simulation;

public enum SimulatorScenario { Normal, DelayedReplies, ProtectionTrip, CableLoss, NegativeCurrent }

// Deterministic resistive-load model. The same worker and typed client are used in both modes.
public sealed partial class SimulatedTransport : IByteTransport
{
    private readonly Queue<byte> replies = new();
    private double voltage = 5, current = .1, ovp = 22, delay = .08;
    private bool output, ocp;
    private long sample;
    private DateTime readyAt;
    private int questionable;
    public SimulatorScenario Scenario { get; set; }
    public bool IsOpen { get; private set; }
    public List<string> Commands { get; } = [];
    public void Open(ConnectionProfile profile) { IsOpen = true; replies.Clear(); }
    public void Write(ReadOnlySpan<byte> bytes)
    {
        if (!IsOpen) throw new IOException("Simulator cable is disconnected.");
        if (Scenario == SimulatorScenario.CableLoss) { IsOpen = false; throw new IOException("Simulated cable loss. Output state cannot be verified."); }
        string command = Encoding.ASCII.GetString(bytes).Trim();
        Commands.Add(command.StartsWith("CAL:PASS ", StringComparison.Ordinal) || command.StartsWith("CAL:STAT 1,", StringComparison.Ordinal)
            ? command.Split(' ', 2)[0] + " [redacted]" : command);
        if (Commands.Count > 2048) Commands.RemoveRange(0, 1024);
        if (Scenario == SimulatorScenario.ProtectionTrip) questionable = 2;
        AdvanceSimulatedAcquisition();
        if (TrySimulateDigitizer(command, out string? advanced) || TrySimulateSetup(command, out advanced))
        {
            if (advanced is not null) EnqueueReply(advanced);
            return;
        }
        string? reply = command switch
        {
            "*IDN?" => "AGILENT TECHNOLOGIES,66332A,SIMULATOR,1.0",
            "VOLT? MIN" => "0", "VOLT? MAX" => "20.475", "CURR? MIN" => "0", "CURR? MAX" => "5.11875",
            "VOLT:PROT? MIN" => "0", "VOLT:PROT? MAX" => "22.5",
            "VOLT?" => F(voltage), "CURR?" => F(current), "VOLT:PROT?" => F(ovp),
            "CURR:PROT:STAT?" => ocp ? "1" : "0", "OUTP:PROT:DEL?" => F(delay),
            "OUTP?" => output ? "1" : "0",
            "STAT:OPER:COND?" => SimulatedOperationCondition.ToString(CultureInfo.InvariantCulture),
            "STAT:QUES:COND?" => questionable.ToString(CultureInfo.InvariantCulture),
            "SYST:ERR?" => "0,\"No error\"",
            "MEAS:VOLT?" => F(output && questionable == 0 ? Math.Min(voltage, current * 100) + Math.Sin(++sample * .17) * .0002 : 0),
            "MEAS:CURR?" => F(output && questionable == 0 ? Scenario == SimulatorScenario.NegativeCurrent ? -.002 : Math.Min(voltage / 100, current) : 0),
            _ => null
        };
        if (reply is not null)
        {
            if (command.StartsWith("MEAS:", StringComparison.Ordinal)) simulatedWaveform = null;
            EnqueueReply(reply);
            return;
        }
        var split = command.Split(' ', 2);
        switch (split[0])
        {
            case "VOLT": voltage = Quantize(ScpiParser.Number(split[1]), .005); break;
            case "CURR": current = Quantize(ScpiParser.Number(split[1]), .001); break;
            case "VOLT:PROT": ovp = Quantize(ScpiParser.Number(split[1]), .005); break;
            case "OUTP:PROT:DEL": delay = Quantize(ScpiParser.Number(split[1]), .001); break;
            case "CURR:PROT:STAT": ocp = split[1] == "1"; break;
            case "OUTP": output = split[1] == "1"; break;
            case "OUTP:PROT:CLE": if (Scenario != SimulatorScenario.ProtectionTrip) questionable = 0; break;
            case "SYST:REM": case "SYST:LOC": break;
            default: throw new IOException($"Simulator does not support {command}.");
        }
    }
    private void EnqueueReply(string reply)
    {
        foreach (byte value in Encoding.ASCII.GetBytes(reply + "\r\n")) replies.Enqueue(value);
        readyAt = DateTime.UtcNow.AddMilliseconds(Scenario == SimulatorScenario.DelayedReplies ? 300 : 0);
    }
    private static double Quantize(double v, double resolution) => Math.Round(v / resolution, MidpointRounding.AwayFromZero) * resolution;
    private static string F(double value) => value.ToString("G17", CultureInfo.InvariantCulture);
    public int Read(Span<byte> buffer)
    {
        if (!IsOpen) throw new IOException("Simulator disconnected.");
        if (DateTime.UtcNow < readyAt || replies.Count == 0) { Thread.Sleep(25); throw new TimeoutException(); }
        int count = Math.Min(buffer.Length, Math.Min(replies.Count, 7)); // Exercise fragmented replies in everyday use.
        for (int i = 0; i < count; i++) buffer[i] = replies.Dequeue();
        return count;
    }
    public void SendBreak(CancellationToken token) { token.ThrowIfCancellationRequested(); replies.Clear(); }
    public void DiscardInput() => replies.Clear();
    public void Close() { IsOpen = false; replies.Clear(); }
    public void Dispose() => Close();
}
