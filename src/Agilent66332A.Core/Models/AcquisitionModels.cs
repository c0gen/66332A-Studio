namespace Agilent66332A.Core.Models;

public enum MeasurementSignal { Voltage, Current }
public enum CurrentMeasurementRange { Low, High }
public enum CurrentDetector { Dc, AcDc }
public enum MeasurementWindow { Hanning, Rectangular }
public enum TriggerSlope { Either, Rising, Falling }
public enum AcquisitionSource { Internal, Bus }
public enum CaptureStart { MeasureNow, Internal, ImmediateTrigger, SerialTrigger }
public enum TransientTrigger { Immediate, Serial }
public enum CapturePhase { Configuring, Waiting, Downloading, ReadingStatistics }

public sealed record SweepSettings(int Points = 2048, double IntervalSeconds = .0000156, int OffsetPoints = 0);
public sealed record SignalTrigger(double Level = 0, double Hysteresis = 0, TriggerSlope Slope = TriggerSlope.Either, int Count = 1);

public sealed record AcquisitionSettings(MeasurementSignal Signal, CurrentMeasurementRange Range, CurrentDetector Detector,
    MeasurementWindow Window, SweepSettings Sweep, AcquisitionSource Source, SignalTrigger VoltageTrigger, SignalTrigger CurrentTrigger)
{
    public SignalTrigger SelectedTrigger => Signal == MeasurementSignal.Voltage ? VoltageTrigger : CurrentTrigger;
    public static AcquisitionSettings Default { get; } = new(MeasurementSignal.Voltage, CurrentMeasurementRange.High,
        CurrentDetector.AcDc, MeasurementWindow.Hanning, new(), AcquisitionSource.Internal, new(), new());
}

public sealed record CaptureRequest(AcquisitionSettings Settings, CaptureStart Start, TimeSpan WaitLimit,
    bool AcknowledgeFaultOutputEvents = false);
public sealed record CaptureProgress(CapturePhase Phase, int Received = 0, int Expected = 0, bool CanFire = false);
public sealed record MeasurementStatistics(double Dc, double Rms, double Minimum, double Maximum, double PulseLow, double PulseHigh);
public sealed record WaveformCapture(InstrumentIdentity Identity, DateTimeOffset StartedUtc, DateTimeOffset CompletedUtc,
    AcquisitionSettings Settings, CaptureStart Start, IReadOnlyList<double> Samples, MeasurementStatistics Statistics)
{
    public double SecondsAt(int index) => (index + (Start == CaptureStart.MeasureNow ? 0d : Settings.Sweep.OffsetPoints)) * Settings.Sweep.IntervalSeconds;
}

public sealed record TransientSettings(RequestedSettings Pending, bool WaitingForTrigger);
public sealed record TransientRequest(RequestedSettings Pending, TransientTrigger Trigger, bool Continuous, TimeSpan WaitLimit);
public sealed record TransientProgress(bool CanFire, int Fired, AcknowledgedSettings? LastReadback = null);
