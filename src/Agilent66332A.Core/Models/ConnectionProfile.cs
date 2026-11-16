using System.Text.Json.Serialization;

namespace Agilent66332A.Core.Models;

public enum SerialParity { None, Even, Odd, Mark, Space }
public enum SerialFlowControl { None, XonXoff, RtsCts, DtrDsr }

public sealed record SerialAdapterIdentity(string InstanceId, string DriverKey, string DriverInf, string DriverVersion)
{
    [JsonIgnore]
    public bool IsComplete => !string.IsNullOrWhiteSpace(InstanceId) && !string.IsNullOrWhiteSpace(DriverKey) &&
        !string.IsNullOrWhiteSpace(DriverInf) && !string.IsNullOrWhiteSpace(DriverVersion);

    public bool Matches(SerialAdapterIdentity? other)
    {
        if (!IsComplete || other?.IsComplete != true) return false;
        var comparer = StringComparer.OrdinalIgnoreCase;
        return comparer.Equals(InstanceId, other.InstanceId) && comparer.Equals(DriverKey, other.DriverKey) &&
            comparer.Equals(DriverInf, other.DriverInf) && comparer.Equals(DriverVersion, other.DriverVersion);
    }
}

public sealed record ConnectionProfile(
    string PortName = "COM7",
    int BaudRate = 9600,
    bool DtrEnabled = true,
    bool RtsEnabled = true,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] SerialParity Parity = SerialParity.None,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] SerialFlowControl FlowControl = SerialFlowControl.None,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? CableId = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] SerialAdapterIdentity? Adapter = null,
    [property: JsonIgnore] string? ChassisLabel = null)
{
    public static IReadOnlyList<int> SupportedBaudRates { get; } = Array.AsReadOnly(new[] { 9600, 4800, 2400, 1200, 600, 300 });

    [JsonIgnore] public int DataBits => Parity == SerialParity.None ? 8 : 7;
    [JsonIgnore] public bool DtrControlledByHandshake => FlowControl == SerialFlowControl.DtrDsr;
    [JsonIgnore] public bool RtsControlledByHandshake => FlowControl == SerialFlowControl.RtsCts;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(PortName) || !SupportedBaudRates.Contains(BaudRate))
            throw new ArgumentException("Select a serial port and a baud rate from 300 to 9600.");
        if (!Enum.IsDefined(Parity) || !Enum.IsDefined(FlowControl))
            throw new ArgumentException("Select a supported parity and flow control mode.");
    }

    public override string ToString()
    {
        string flow = FlowControl switch
        {
            SerialFlowControl.XonXoff => "XON/XOFF",
            SerialFlowControl.RtsCts => "RTS/CTS",
            SerialFlowControl.DtrDsr => "DTR/DSR",
            _ => "no flow"
        };
        string dtr = DtrControlledByHandshake ? "auto" : DtrEnabled ? "on" : "off";
        string rts = RtsControlledByHandshake ? "auto" : RtsEnabled ? "on" : "off";
        return $"{PortName} / {BaudRate} / {DataBits}{Parity.ToString()[0]}1 / {flow} / CRLF / DTR {dtr}, RTS {rts}";
    }
}
