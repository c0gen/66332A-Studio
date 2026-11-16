using System.Globalization;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Simulation;

public sealed partial class SimulatedTransport
{
    public bool HasRelayOption { get; init; } = true;
    public bool? DigitalPinInput { get; set; }
    public bool FastOutputMode { get; set; }
    private string simulatedPortFunction = "RIDF";
    private string simulatedInhibitMode = "OFF";
    private string simulatedFaultSource = "OFF";
    private bool simulatedFaultEnabled;
    private int simulatedDigitalOutputs;
    private bool simulatedRelayClosed;
    private string simulatedRelayPolarity = "NORM";
    private bool simulatedDisplayEnabled = true;
    private string simulatedDisplayMode = "NORM";
    private string simulatedDisplayText = "";

    private bool TrySimulateSetup(string command, out string? reply)
    {
        if (TrySimulateService(command, out reply)) return true;
        if (TrySimulateDiagnostics(command, out reply)) return true;
        var split = command.Split(' ', 2);
        string header = split[0];
        string value = split.Length == 2 ? split[1] : "";
        reply = header switch
        {
            "*OPT?" => HasRelayOption ? "760" : "0",
            "SYST:VERS?" => "1995.0",
            "VOLT:ALC:BAND?" => FastOutputMode ? "60000" : "15000",
            "DIG:FUNC?" => simulatedPortFunction,
            "DIG:DATA?" when simulatedPortFunction == "DIG" => (simulatedDigitalOutputs & 3 |
                (DigitalPinInput switch { true => 4, false => 0, _ => simulatedDigitalOutputs & 4 })).ToString(CultureInfo.InvariantCulture),
            "OUTP:RI:MODE?" => simulatedInhibitMode,
            "OUTP:DFI:SOUR?" => simulatedFaultSource,
            "OUTP:DFI?" or "OUTP:DFI:STAT?" => simulatedFaultEnabled ? "1" : "0",
            "OUTP:REL?" when HasRelayOption => simulatedRelayClosed ? "1" : "0",
            "OUTP:REL:POL?" when HasRelayOption => simulatedRelayPolarity,
            "DISP?" => simulatedDisplayEnabled ? "1" : "0",
            "DISP:MODE?" => simulatedDisplayMode,
            "DISP:TEXT?" => "\"" + simulatedDisplayText.Replace("\"", "\"\"") + "\"",
            _ => null
        };
        if (reply is not null) return true;
        switch (header)
        {
            case "DIG:FUNC": simulatedPortFunction = value; return true;
            case "DIG:DATA" when simulatedPortFunction == "DIG": simulatedDigitalOutputs = ScpiParser.Register(value); return true;
            case "OUTP:RI:MODE": simulatedInhibitMode = value; return true;
            case "OUTP:DFI:SOUR": simulatedFaultSource = value; return true;
            case "OUTP:DFI" or "OUTP:DFI:STAT": simulatedFaultEnabled = value == "1"; return true;
            case "OUTP:REL" when HasRelayOption: simulatedRelayClosed = value == "1"; return true;
            case "OUTP:REL:POL" when HasRelayOption: simulatedRelayPolarity = value; return true;
            case "OUTP" when value.EndsWith(",NOR", StringComparison.Ordinal):
                if (HasRelayOption) output = value.StartsWith('1');
                else SimulatedError(-224, "Illegal parameter value");
                return true;
            case "OUTP":
                if (HasRelayOption) simulatedRelayClosed = value == "1";
                return false; // The shared output simulator applies the standard output state.
            case "DISP": simulatedDisplayEnabled = value == "1"; return true;
            case "DISP:MODE": simulatedDisplayMode = value; return true;
            case "DISP:TEXT":
                simulatedDisplayText = value.Length >= 2 && value[0] == '"' && value[^1] == '"'
                    ? value[1..^1].Replace("\"\"", "\"") : value;
                return true;
            case "SYST:RWL": return true;
            default: return false;
        }
    }
}
