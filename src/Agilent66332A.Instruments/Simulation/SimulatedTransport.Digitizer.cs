using System.Diagnostics;
using Agilent66332A.Core.Models;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Simulation;

public sealed partial class SimulatedTransport
{
    private int sweepPoints = 2048;
    private int sweepOffset;
    private double sweepInterval = .0000156;
    private string sensor = "VOLT", detector = "ACDC", window = "HANN", acquireSource = "INT";
    private bool lowCurrentRange;
    private readonly Dictionary<string, string> signalTriggerSettings = new(StringComparer.Ordinal)
    {
        ["COUN:VOLT"] = "1", ["COUN:CURR"] = "1", ["LEV:VOLT"] = "0", ["LEV:CURR"] = "0",
        ["HYST:VOLT"] = "0", ["HYST:CURR"] = "0", ["SLOP:VOLT"] = "EITH", ["SLOP:CURR"] = "EITH"
    };
    private bool acquisitionArmed, acquisitionOpcRequested, transientArmed, transientContinuous;
    private int remainingSweeps;
    private double? simulatedSweepDue, triggeredVoltage, triggeredCurrent;
    private double[]? simulatedWaveform;
    private string? waveformSignal;

    private int SimulatedOperationCondition =>
        (acquisitionArmed || transientArmed ? 32 : 0) |
        (!output || questionable != 0 ? 0 : Scenario == SimulatorScenario.NegativeCurrent ? 2048 : voltage / 100 > current ? 1024 : 256);

    private bool TrySimulateDigitizer(string command, out string? reply)
    {
        var parts = command.Split(' ', 2);
        string header = parts[0];
        string arg = parts.Length == 2 ? parts[1].Trim('"') : "";
        reply = null;
        switch (header)
        {
            case "SENS:SWE:POIN?": reply = F(sweepPoints); return true;
            case "SENS:SWE:POIN": sweepPoints = (int)ScpiParser.Number(arg); return true;
            case "SENS:SWE:OFFS:POIN?": reply = F(sweepOffset); return true;
            case "SENS:SWE:OFFS:POIN": sweepOffset = (int)ScpiParser.Number(arg); return true;
            case "SENS:SWE:TINT?": reply = F(sweepInterval); return true;
            case "SENS:SWE:TINT": sweepInterval = Quantize(ScpiParser.Number(arg), .0000156); return true;
            case "SENS:FUNC?": reply = $"\"{sensor}\""; return true;
            case "SENS:FUNC": sensor = arg; return true;
            case "SENS:CURR:RANG?": reply = lowCurrentRange ? "0.02" : "5.11875"; return true;
            case "SENS:CURR:RANG": lowCurrentRange = arg == "MIN"; return true;
            case "SENS:CURR:DET?": reply = lowCurrentRange ? "DC" : detector; return true;
            case "SENS:CURR:DET": detector = arg; return true;
            case "SENS:WIND?": reply = window; return true;
            case "SENS:WIND": window = arg; return true;
            case "TRIG:ACQ:SOUR?": reply = acquireSource; return true;
            case "TRIG:ACQ:SOUR": acquireSource = arg; return true;
            case "VOLT:TRIG?": reply = F(triggeredVoltage ?? voltage); return true;
            case "CURR:TRIG?": reply = F(triggeredCurrent ?? current); return true;
            case "VOLT:TRIG": triggeredVoltage = Quantize(ScpiParser.Number(arg), .005); return true;
            case "CURR:TRIG": triggeredCurrent = Quantize(ScpiParser.Number(arg), .001); return true;
            case "TRIG:SOUR": return true;
            case "INIT:SEQ1": transientArmed = true; return true;
            case "INIT:CONT:SEQ1":
                transientContinuous = arg == "ON";
                if (transientContinuous) transientArmed = true;
                return true;
            case "INIT:SEQ2":
                acquisitionArmed = true;
                remainingSweeps = (int)ScpiParser.Number(signalTriggerSettings[$"COUN:{sensor}"]);
                simulatedSweepDue = null;
                return true;
            case "TRIG:TRAN": FireSimulatedTransient(); return true;
            case "TRIG:ACQ": FireSimulatedAcquisition(); return true;
            case "*TRG":
                FireSimulatedTransient();
                if (acquireSource == "BUS") FireSimulatedAcquisition();
                return true;
            case "*OPC" when acquisitionArmed: acquisitionOpcRequested = true; return true;
            case "ABOR": AbortSimulatedTriggers(); return true;
        }

        if (header.StartsWith("TRIG:ACQ:", StringComparison.Ordinal))
        {
            string key = header[9..].TrimEnd('?');
            if (!signalTriggerSettings.TryGetValue(key, out string? setting)) return false;
            if (header.EndsWith('?')) reply = setting;
            else signalTriggerSettings[key] = arg;
            return true;
        }

        if (header is "MEAS:ARR:VOLT?" or "MEAS:ARR:CURR?")
        {
            GenerateSimulatedWaveform(header[9..^1]);
            reply = string.Join(',', simulatedWaveform!.Select(F));
            return true;
        }
        if (header.StartsWith("FETC:", StringComparison.Ordinal))
        {
            reply = FetchSimulatedWaveform(header[5..^1]);
            return true;
        }
        return false;
    }

    private void AdvanceSimulatedAcquisition()
    {
        if (!acquisitionArmed) return;
        if (simulatedSweepDue is { } due)
        {
            if (SimulatorSeconds < due) return;
            simulatedSweepDue = null;
            if (--remainingSweeps == 0)
            {
                GenerateSimulatedWaveform(sensor);
                acquisitionArmed = false;
                if (acquisitionOpcRequested) simulatedStandardEvent |= 1;
                acquisitionOpcRequested = false;
            }
        }

        if (!acquisitionArmed || acquireSource != "INT" || !output || questionable != 0) return;
        double high = SimulatedSignal(sensor);
        double low = high * .8;
        double threshold = ScpiParser.Number(signalTriggerSettings[$"LEV:{sensor}"]);
        double hysteresis = ScpiParser.Number(signalTriggerSettings[$"HYST:{sensor}"]);
        if (Math.Min(low, high) <= threshold - hysteresis / 2 && Math.Max(low, high) >= threshold + hysteresis / 2)
            FireSimulatedAcquisition();
    }

    private static double SimulatorSeconds => (double)Stopwatch.GetTimestamp() / Stopwatch.Frequency;

    private void FireSimulatedAcquisition()
    {
        if (!acquisitionArmed || simulatedSweepDue is not null) return;
        simulatedSweepDue = SimulatorSeconds + Math.Max(.005, (sweepPoints + Math.Max(0d, sweepOffset)) * sweepInterval);
    }

    private void FireSimulatedTransient()
    {
        if (!transientArmed) return;
        voltage = triggeredVoltage ?? voltage;
        current = triggeredCurrent ?? current;
        transientArmed = transientContinuous;
    }

    private void AbortSimulatedTriggers()
    {
        acquisitionArmed = acquisitionOpcRequested = false;
        transientArmed = transientContinuous;
        triggeredVoltage = triggeredCurrent = simulatedSweepDue = null;
    }

    private void ResetSimulatedDigitizer()
    {
        transientContinuous = false;
        AbortSimulatedTriggers();
        sweepPoints = 2048;
        sweepInterval = .0000156;
        sweepOffset = 0;
        lowCurrentRange = false;
        sensor = "VOLT";
        detector = "ACDC";
        window = "HANN";
        acquireSource = "INT";
        foreach (string signal in new[] { "VOLT", "CURR" })
        {
            signalTriggerSettings[$"COUN:{signal}"] = "1";
            signalTriggerSettings[$"LEV:{signal}"] = signal == "VOLT" ? "20.475" : "5.11875";
            signalTriggerSettings[$"HYST:{signal}"] = "0";
            signalTriggerSettings[$"SLOP:{signal}"] = "POS";
        }
        simulatedWaveform = null;
    }

    private void GenerateSimulatedWaveform(string signal)
    {
        waveformSignal = signal;
        double high = SimulatedSignal(signal);
        simulatedWaveform = Enumerable.Range(0, sweepPoints)
            .Select(index => high * ((index % 40 < 12) ? 1 : .8)).ToArray();
    }

    private double SimulatedSignal(string signal)
    {
        if (!output || questionable != 0) return 0;
        return signal == "VOLT" ? Math.Min(voltage, current * 100) :
            Scenario == SimulatorScenario.NegativeCurrent ? -.002 : Math.Min(voltage / 100, current);
    }

    private string FetchSimulatedWaveform(string header)
    {
        var path = header.Split(':');
        if (simulatedWaveform is not { Length: > 0 } waveform || waveformSignal != (path[0] == "ARR" ? path[1] : path[0]))
            throw new IOException("The simulator has no buffered capture for this signal.");
        if (path[0] == "ARR") return string.Join(',', waveform.Select(F));
        double low = waveform.Min(), high = waveform.Max();
        return F(path.ElementAtOrDefault(1) switch
        {
            "MIN" or "LOW" => low,
            "MAX" or "HIGH" => high,
            "ACDC" => Math.Sqrt(waveform.Average(value => value * value)),
            _ => waveform.Average()
        });
    }

}
