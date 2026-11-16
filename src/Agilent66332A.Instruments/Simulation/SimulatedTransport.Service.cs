using System.Globalization;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Simulation;

public sealed partial class SimulatedTransport
{
    private readonly SavedSimulatedState?[] simulatedSlots = new SavedSimulatedState?[4];
    private string simulatedPowerOnPolicy = "RST";
    private bool simulatedCompatibility;
    private int simulatedCompatibilityError;
    private int simulatedLegacyCondition;
    private int simulatedLegacyAccumulated;
    private int simulatedLegacyFaults;
    private int simulatedLegacyFaultMask;
    private bool legacyPowerOnRequestWritten;
    private bool legacyPowerOnRequest;
    private bool legacyServiceRequests;
    private bool legacyCloseRelayAtPowerOn = true;
    private bool simulatedLegacyPowerOnPending;
    public bool CompatibilityServiceRequestAsserted => simulatedCompatibility &&
        (simulatedLegacyPowerOnPending || (legacyServiceRequests && simulatedLegacyFaults != 0));

    private bool TrySimulateService(string command, out string? reply)
    {
        reply = null;
        switch (command)
        {
            case "SYST:LANG?": reply = simulatedCompatibility ? "COMP" : "SCPI"; return true;
            case "SYST:LANG COMP": ChangeSimulatedLanguage(true); return true;
            case "SYST:LANG SCPI": ChangeSimulatedLanguage(false); return true;
        }
        if (simulatedCompatibility) { reply = SimulateCompatibility(command); return true; }
        if (TrySimulateCalibration(command, out reply)) return true;
        switch (command)
        {
            case "*TST?": reply = "0"; return true;
            case "*RST": ResetSimulatedSupply(); return true;
            case "OUTP:PON:STAT?": reply = simulatedPowerOnPolicy; return true;
        }
        var split = command.Split(' ', 2);
        if (split.Length != 2) return false;
        switch (split[0])
        {
            case "OUTP:PON:STAT": simulatedPowerOnPolicy = split[1]; return true;
            case "*SAV":
                simulatedSlots[ScpiParser.Register(split[1])] = new(voltage, current, ovp, delay, output, ocp,
                    simulatedRelayClosed, simulatedRelayPolarity, simulatedDigitalOutputs, simulatedFaultEnabled,
                    simulatedFaultSource, simulatedDisplayEnabled, simulatedDisplayMode, simulatedDisplayText,
                    new(sweepPoints, sweepOffset, sweepInterval, sensor, detector, window, acquireSource, lowCurrentRange,
                        new Dictionary<string, string>(signalTriggerSettings, StringComparer.Ordinal)));
                return true;
            case "*RCL": RestoreSimulatedSlot(ScpiParser.Register(split[1])); return true;
            default: return false;
        }
    }

    private string? SimulateCompatibility(string command)
    {
        int state = SimulatedLegacyStatus();
        simulatedLegacyAccumulated |= state & ~simulatedLegacyCondition;
        simulatedLegacyFaults |= state & ~simulatedLegacyCondition & simulatedLegacyFaultMask;
        simulatedLegacyCondition = state;
        switch (command)
        {
            case "ID?": return "AGILENT66332A";
            case "ROM?": return "1.0";
            case "VOUT?": return F(output && questionable == 0 ? Math.Min(voltage, current * 100) : 0);
            case "IOUT?": return F(output && questionable == 0 ? Math.Min(voltage / 100, current) : 0);
            case "STS?": return StatusNumber(state);
            case "ASTS?":
                int accumulated = simulatedLegacyAccumulated;
                simulatedLegacyAccumulated = 0;
                return StatusNumber(accumulated);
            case "FAULT?":
                int faults = simulatedLegacyFaults;
                simulatedLegacyFaults = 0;
                return StatusNumber(faults);
            case "ERR?":
                int error = simulatedCompatibilityError;
                simulatedCompatibilityError = 0;
                return StatusNumber(error);
            case "TEST?": return "0";
            case "CLR": ResetSimulatedCompatibility(); return null;
            case "OUT 0":
                output = simulatedRelayClosed = false;
                return null;
        }

        var parts = command.Split(' ', 2);
        string argument = parts.Length == 2 ? parts[1] : "";
        switch (parts[0])
        {
            case "UNMASK": simulatedLegacyFaultMask = ScpiParser.Register(argument); break;
            case "PON":
                if (legacyPowerOnRequestWritten) simulatedCompatibilityError = 2;
                else { legacyPowerOnRequest = argument == "1"; legacyPowerOnRequestWritten = true; }
                break;
            case "SRQ": legacyServiceRequests = argument == "1"; break;
            case "RLYPON" when HasRelayOption: legacyCloseRelayAtPowerOn = argument == "1"; break;
            case "RLYPON": simulatedCompatibilityError = 4; break;
            default: simulatedCompatibilityError = 11; break;
        }
        return null;
    }

    private int SimulatedLegacyStatus()
    {
        int condition = FastOutputMode ? 1024 : 2048;
        if ((questionable & 1) != 0) condition |= 8;
        if ((questionable & 2) != 0) condition |= 64;
        if ((questionable & 16) != 0) condition |= 16;
        if ((questionable & 512) != 0) condition |= 256;
        if ((questionable & 1024) != 0) condition |= 4;
        if (simulatedCompatibilityError != 0) condition |= 128;
        if (output && questionable == 0)
            condition |= Scenario == SimulatorScenario.NegativeCurrent ? 512 : voltage / 100 > current ? 2 : 1;
        return condition;
    }

    private void ChangeSimulatedLanguage(bool compatibility)
    {
        simulatedCompatibility = compatibility;
        if (compatibility) ResetSimulatedCompatibility();
        else ResetSimulatedSupply();
        simulatedLegacyPowerOnPending = compatibility && legacyPowerOnRequest;
        ClearSimulatedEvents();
        if (simulatedPowerOnClear) simulatedEventEnable = simulatedServiceEnable = 0;
        simulatedStandardEvent |= 128;
        if (!compatibility && simulatedPowerOnPolicy == "RCL0") RestoreSimulatedSlot(0);
    }

    private void ResetSimulatedCompatibility()
    {
        ResetSimulatedSupply();
        simulatedCompatibilityError = simulatedLegacyCondition = simulatedLegacyAccumulated = simulatedLegacyFaults = simulatedLegacyFaultMask = 0;
        simulatedLegacyPowerOnPending = false;
        legacyServiceRequests = false;
        current = .02;
        output = true;
        simulatedRelayClosed = HasRelayOption && legacyCloseRelayAtPowerOn;
    }

    private void ResetSimulatedSupply()
    {
        voltage = 0;
        current = .511875;
        ovp = 22.5;
        delay = FastOutputMode ? .008 : .08;
        output = ocp = simulatedRelayClosed = simulatedFaultEnabled = false;
        simulatedRelayPolarity = "NORM";
        simulatedFaultSource = "OFF";
        simulatedDigitalOutputs = 0;
        simulatedDisplayEnabled = true;
        simulatedDisplayMode = "NORM";
        simulatedDisplayText = "";
        EndSimulatedCalibration();
        ResetSimulatedDigitizer();
    }

    private void RestoreSimulatedSlot(int address)
    {
        if (simulatedSlots[address] is not { } saved) { ResetSimulatedSupply(); return; }
        voltage = saved.Voltage;
        current = saved.Current;
        ovp = saved.Overvoltage;
        delay = saved.ProtectionDelay;
        output = saved.OutputOn;
        ocp = saved.OvercurrentEnabled;
        simulatedRelayClosed = saved.RelayClosed;
        simulatedRelayPolarity = saved.Polarity;
        simulatedDigitalOutputs = saved.DigitalOutputs;
        simulatedFaultEnabled = saved.FaultIndicatorEnabled;
        simulatedFaultSource = saved.FaultSource;
        simulatedDisplayEnabled = saved.DisplayEnabled;
        simulatedDisplayMode = saved.DisplayMode;
        simulatedDisplayText = saved.DisplayText;
        sweepPoints = saved.Digitizer.Points;
        sweepOffset = saved.Digitizer.Offset;
        sweepInterval = saved.Digitizer.Interval;
        sensor = saved.Digitizer.Signal;
        detector = saved.Digitizer.Detector;
        window = saved.Digitizer.Window;
        acquireSource = saved.Digitizer.AcquireSource;
        lowCurrentRange = saved.Digitizer.LowCurrentRange;
        foreach (var setting in saved.Digitizer.TriggerSettings) signalTriggerSettings[setting.Key] = setting.Value;
        EndSimulatedCalibration();
        transientContinuous = false;
        AbortSimulatedTriggers();
        simulatedWaveform = null;
    }

    private sealed record SavedSimulatedState(double Voltage, double Current, double Overvoltage, double ProtectionDelay,
        bool OutputOn, bool OvercurrentEnabled, bool RelayClosed, string Polarity, int DigitalOutputs,
        bool FaultIndicatorEnabled, string FaultSource, bool DisplayEnabled, string DisplayMode, string DisplayText, SavedSimulatedDigitizer Digitizer);

    private sealed record SavedSimulatedDigitizer(int Points, int Offset, double Interval, string Signal,
        string Detector, string Window, string AcquireSource, bool LowCurrentRange, IReadOnlyDictionary<string, string> TriggerSettings);
}
