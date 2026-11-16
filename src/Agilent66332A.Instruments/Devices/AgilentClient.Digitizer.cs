using Agilent66332A.Core.Models;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Devices;

public partial class AgilentClient
{
    private const double SampleTick = .0000156;
    private double measurementSweepSeconds = -1;

    public Task<AcquisitionSettings> ReadAcquisitionAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureBasicMode();
        return Guard(() => ReadAcquisition(t));
    }, token);

    public Task<AcquisitionSettings> ApplyAcquisitionAsync(AcquisitionSettings target, CancellationToken token = default) => Queue(t =>
    {
        EnsureBasicMode();
        RequireFeature(QualifiedFeature.Acquisition);
        ValidateAcquisition(target);
        RequireIdleTriggers(t);
        return Guard(() => ProgramAcquisition(target, t));
    }, token, configuration: true);

    private AcquisitionSettings ReadAcquisition(CancellationToken token)
    {
        var sweep = new SweepSettings(Integer("SENS:SWE:POIN?", token), Number("SENS:SWE:TINT?", token),
            Integer("SENS:SWE:OFFS:POIN?", token));
        Volatile.Write(ref measurementSweepSeconds, sweep.Points * sweep.IntervalSeconds);
        var range = Number("SENS:CURR:RANG?", token) <= .020001 ? CurrentMeasurementRange.Low : CurrentMeasurementRange.High;
        return new(ReadSymbol("SENS:FUNC?", token, ("VOLT", MeasurementSignal.Voltage), ("CURR", MeasurementSignal.Current)), range,
            ReadSymbol("SENS:CURR:DET?", token, ("DC", CurrentDetector.Dc), ("ACDC", CurrentDetector.AcDc)),
            ReadSymbol("SENS:WIND?", token, ("HANN", MeasurementWindow.Hanning), ("RECT", MeasurementWindow.Rectangular)), sweep,
            ReadSymbol("TRIG:ACQ:SOUR?", token, ("INT", AcquisitionSource.Internal), ("BUS", AcquisitionSource.Bus)),
            ReadSignalTrigger("VOLT", token), ReadSignalTrigger("CURR", token));
    }

    private void RefreshMeasurementDuration(CancellationToken token) => Volatile.Write(ref measurementSweepSeconds,
        Integer("SENS:SWE:POIN?", token) * Number("SENS:SWE:TINT?", token));

    private SignalTrigger ReadSignalTrigger(string signal, CancellationToken token) => new(
        Number($"TRIG:ACQ:LEV:{signal}?", token), Number($"TRIG:ACQ:HYST:{signal}?", token),
        ReadSymbol($"TRIG:ACQ:SLOP:{signal}?", token, ("POS", TriggerSlope.Rising), ("NEG", TriggerSlope.Falling), ("EITH", TriggerSlope.Either)),
        Integer($"TRIG:ACQ:COUN:{signal}?", token));

    private AcquisitionSettings ProgramAcquisition(AcquisitionSettings target, CancellationToken token)
    {
        var before = ReadAcquisition(token);
        channel.Write("SYST:REM", token);
        if (before.Range != target.Range)
            WriteChecked("SENS:CURR:RANG " + (target.Range == CurrentMeasurementRange.Low ? "MIN" : "MAX"), token);
        if (target.Range == CurrentMeasurementRange.High && (before.Range != target.Range || before.Detector != target.Detector))
            WriteChecked("SENS:CURR:DET " + (target.Detector == CurrentDetector.Dc ? "DC" : "ACDC"), token);
        if (before.Signal != target.Signal) WriteChecked($"SENS:FUNC \"{SignalCode(target.Signal)}\"", token);
        if (before.Window != target.Window)
            WriteChecked("SENS:WIND " + (target.Window == MeasurementWindow.Hanning ? "HANN" : "RECT"), token);
        if (before.Source != target.Source)
            WriteChecked("TRIG:ACQ:SOUR " + (target.Source == AcquisitionSource.Internal ? "INT" : "BUS"), token);
        SetNumberWhenChanged("SENS:SWE:POIN", before.Sweep.Points, target.Sweep.Points, token);
        SetNumberWhenChanged("SENS:SWE:TINT", before.Sweep.IntervalSeconds, target.Sweep.IntervalSeconds, token);
        SetNumberWhenChanged("SENS:SWE:OFFS:POIN", before.Sweep.OffsetPoints, target.Sweep.OffsetPoints, token);
        ProgramSignalTrigger("VOLT", before.VoltageTrigger, target.VoltageTrigger, token);
        ProgramSignalTrigger("CURR", before.CurrentTrigger, target.CurrentTrigger, token);
        var actual = ReadAcquisition(token);
        var bounds = snapshot!.Capabilities;
        if (target.Signal != actual.Signal || target.Source != actual.Source || target.Range != actual.Range || target.Window != actual.Window ||
            target.Range == CurrentMeasurementRange.High && target.Detector != actual.Detector ||
            target.Sweep.Points != actual.Sweep.Points || target.Sweep.OffsetPoints != actual.Sweep.OffsetPoints ||
            !NearlyEqual(target.Sweep.IntervalSeconds, actual.Sweep.IntervalSeconds, SampleTick) ||
            !TriggerMatches(target.VoltageTrigger, actual.VoltageTrigger, bounds.Voltage.Resolution) ||
            !TriggerMatches(target.CurrentTrigger, actual.CurrentTrigger, bounds.Current.Resolution))
            throw new IOException("Digitizer or trigger readback did not match the requested settings.");
        return actual;
    }

    private void ProgramSignalTrigger(string signal, SignalTrigger before, SignalTrigger target, CancellationToken token)
    {
        SetNumberWhenChanged($"TRIG:ACQ:COUN:{signal}", before.Count, target.Count, token);
        SetNumberWhenChanged($"TRIG:ACQ:LEV:{signal}", before.Level, target.Level, token);
        SetNumberWhenChanged($"TRIG:ACQ:HYST:{signal}", before.Hysteresis, target.Hysteresis, token);
        if (before.Slope == target.Slope) return;
        string slope = target.Slope switch { TriggerSlope.Rising => "POS", TriggerSlope.Falling => "NEG", _ => "EITH" };
        WriteChecked($"TRIG:ACQ:SLOP:{signal} {slope}", token);
    }

    private void SetNumberWhenChanged(string command, double before, double target, CancellationToken token)
    {
        if (before != target) WriteChecked($"{command} {ScpiParser.Format(target)}", token);
    }

    private void WriteChecked(string command, CancellationToken token)
    {
        channel.Write(command, token);
        CheckErrors(token);
    }

    private void ValidateAcquisition(AcquisitionSettings settings)
    {
        if (!Enum.IsDefined(settings.Signal) || !Enum.IsDefined(settings.Range) || !Enum.IsDefined(settings.Detector) ||
            !Enum.IsDefined(settings.Window) || !Enum.IsDefined(settings.Source))
            throw new ArgumentException("Select valid digitizer settings.");
        var sweep = settings.Sweep;
        if (sweep.Points is < 0 or > 4096 || sweep.OffsetPoints is < -4095 or > 2_000_000_000 ||
            !double.IsFinite(sweep.IntervalSeconds) || sweep.IntervalSeconds is < SampleTick or > 31_200)
            throw new ArgumentException("The sweep settings are outside the 66332A limits.");
        ValidateTrigger(settings.VoltageTrigger, snapshot!.Capabilities.Voltage.Maximum);
        ValidateTrigger(settings.CurrentTrigger, snapshot.Capabilities.Current.Maximum);
    }

    private static void ValidateTrigger(SignalTrigger trigger, double maximum)
    {
        if (!Enum.IsDefined(trigger.Slope) || trigger.Count is < 1 or > 100 ||
            !double.IsFinite(trigger.Level) || trigger.Level < 0 || trigger.Level > maximum ||
            !double.IsFinite(trigger.Hysteresis) || trigger.Hysteresis < 0 || trigger.Hysteresis > maximum)
            throw new ArgumentException("Trigger edge, level, hysteresis or sweep count is outside the 66332A limits.");
    }

    private int Integer(string command, CancellationToken token)
    {
        double number = Number(command, token);
        if (number != Math.Truncate(number) || number is < int.MinValue or > int.MaxValue)
            throw new FormatException($"{command} did not return an integer.");
        return (int)number;
    }

    private T ReadSymbol<T>(string query, CancellationToken token, params (string Mnemonic, T Value)[] values)
    {
        string response = channel.Query(query, token).Trim().Trim('"');
        foreach (var (mnemonic, value) in values)
            if (response.StartsWith(mnemonic, StringComparison.OrdinalIgnoreCase)) return value;
        throw new FormatException($"Unexpected reply to {query}.");
    }

    private static bool NearlyEqual(double requested, double actual, double resolution) =>
        Math.Abs(requested - actual) <= resolution / 2 + Math.Max(1e-10, Math.Abs(actual) * 1e-7);

    private static bool TriggerMatches(SignalTrigger expected, SignalTrigger actual, double resolution) =>
        actual.Count == expected.Count && actual.Slope == expected.Slope &&
        NearlyEqual(expected.Level, actual.Level, resolution) && NearlyEqual(expected.Hysteresis, actual.Hysteresis, resolution);

    private static string SignalCode(MeasurementSignal signal) => signal == MeasurementSignal.Voltage ? "VOLT" : "CURR";
}
