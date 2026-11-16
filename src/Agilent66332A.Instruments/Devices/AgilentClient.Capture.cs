using Agilent66332A.Core.Models;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Devices;

public partial class AgilentClient
{
    private int triggerOperation;
    private bool triggersMayBeArmed;
    private bool continuousMayBeEnabled;
    private bool CaptureOwnsBuffer => Volatile.Read(ref triggerOperation) != 0;

    public async Task<WaveformCapture> CaptureAsync(CaptureRequest request, IProgress<CaptureProgress>? progress = null,
        Func<bool>? takeFireRequest = null, CancellationToken token = default)
    {
        using var lifetime = EnterTriggerOperation(token);
        token = lifetime.Token;
        bool prepared = false;
        try
        {
            DateTimeOffset started = clock.UtcNow;
            var settings = await Queue(t =>
            {
                RequireFeature(QualifiedFeature.Acquisition);
                ValidateCapture(request, takeFireRequest is not null);
                RequireIdleTriggers(t);
                if (request.Start != CaptureStart.MeasureNow && !request.AcknowledgeFaultOutputEvents && UsesStandardEventFaultOutput(t))
                    throw new InvalidOperationException("This capture reads standard events and may clear the wired fault output. Review its source or allow acknowledgment for this capture.");

                prepared = true;
                progress?.Report(new(CapturePhase.Configuring));
                var target = request.Start switch
                {
                    CaptureStart.Internal => request.Settings with { Source = AcquisitionSource.Internal },
                    CaptureStart.ImmediateTrigger or CaptureStart.SerialTrigger => request.Settings with { Source = AcquisitionSource.Bus },
                    _ => request.Settings
                };
                var actual = ProgramAcquisition(target, t);
                if (request.Start == CaptureStart.MeasureNow) return actual;

                ReadAndRecordStandardEvent(t); // Remove a previous OPC latch before arming this capture.
                triggersMayBeArmed = true;
                WriteChecked("INIT:SEQ2", t);
                channel.Write("*OPC", t);
                return actual;
            }, token, configuration: true).ConfigureAwait(false);

            if (request.Start != CaptureStart.MeasureNow)
                await WaitForCaptureAsync(request, settings, takeFireRequest, progress, token).ConfigureAwait(false);

            return await Queue(t => DownloadCapture(request.Start, settings, started, progress, t), token).ConfigureAwait(false);
        }
        catch
        {
            if (prepared || !channel.Synchronized) await RequestOutputOffAsync("Capture stopped").ConfigureAwait(false);
            throw;
        }
        finally { Volatile.Write(ref triggerOperation, 0); }
    }

    private void ValidateCapture(CaptureRequest request, bool canFire)
    {
        ValidateAcquisition(request.Settings);
        ValidateWait(request.WaitLimit);
        if (!Enum.IsDefined(request.Start)) throw new ArgumentException("Select a capture mode.");
        int points = request.Settings.Sweep.Points;
        int count = request.Start == CaptureStart.MeasureNow ? 1 : request.Settings.SelectedTrigger.Count;
        if (points == 0 || (long)points * count > 4096)
            throw new ArgumentException("Use at least one point and at most 4,096 points across the selected signal’s sweeps.");
        if (request.Start == CaptureStart.ImmediateTrigger) RequireFeature(QualifiedFeature.AcquisitionImmediate);
        if (request.Start == CaptureStart.SerialTrigger) RequireFeature(QualifiedFeature.AcquisitionBus);
        if (request.Start is CaptureStart.ImmediateTrigger or CaptureStart.SerialTrigger && !canFire)
            throw new ArgumentException("An armed software trigger needs an explicit Fire action.");
        var sweep = request.Settings.Sweep;
        double minimumSeconds = request.Start == CaptureStart.MeasureNow ? sweep.Points * sweep.IntervalSeconds :
            Math.Max(0, -sweep.OffsetPoints) * sweep.IntervalSeconds + count * SweepSeconds(sweep);
        if (minimumSeconds > request.WaitLimit.TotalSeconds)
            throw new ArgumentException("Increase the wait limit to cover the configured sweep and trigger offset.");
    }

    private async Task WaitForCaptureAsync(CaptureRequest request, AcquisitionSettings settings, Func<bool>? takeFireRequest,
        IProgress<CaptureProgress>? progress, CancellationToken token)
    {
        double start = clock.Seconds;
        double nextFire = start + Math.Max(0, -settings.Sweep.OffsetPoints) * settings.Sweep.IntervalSeconds;
        int fired = 0;
        bool manual = request.Start is CaptureStart.ImmediateTrigger or CaptureStart.SerialTrigger;
        while (clock.Seconds - start < request.WaitLimit.TotalSeconds)
        {
            token.ThrowIfCancellationRequested();
            bool mayFire = manual && fired < settings.SelectedTrigger.Count && clock.Seconds >= nextFire;
            progress?.Report(new(CapturePhase.Waiting, CanFire: mayFire));
            bool send = mayFire && takeFireRequest!.Invoke();
            bool completed = await Queue(t =>
            {
                VerifyTriggerProtection(t);
                if (send) WriteChecked(request.Start == CaptureStart.SerialTrigger ? "*TRG" : "TRIG:ACQ", t);
                bool done = (ReadAndRecordStandardEvent(t) & 1) != 0;
                if (done) triggersMayBeArmed = false;
                return done;
            }, token).ConfigureAwait(false);
            if (completed) return;
            if (send)
            {
                fired++;
                nextFire = clock.Seconds + SweepSeconds(settings.Sweep);
            }
            await clock.DelayAsync(TimeSpan.FromMilliseconds(150), token).ConfigureAwait(false);
        }
        throw new TimeoutException("The capture did not complete before the trigger wait limit.");
    }

    private WaveformCapture DownloadCapture(CaptureStart start, AcquisitionSettings settings, DateTimeOffset started,
        IProgress<CaptureProgress>? progress, CancellationToken token)
    {
        string signal = SignalCode(settings.Signal);
        string prefix = start == CaptureStart.MeasureNow ? "MEAS" : "FETC";
        int points = settings.Sweep.Points;
        progress?.Report(new(CapturePhase.Downloading, Expected: points));
        // The dictionary defines returned length by sweep points. Repeated sweeps are combined by the instrument.
        var samples = channel.QueryArray($"{prefix}:ARR:{signal}?", points,
            start == CaptureStart.MeasureNow ? settings.Sweep.Points * settings.Sweep.IntervalSeconds : 0,
            token, count => progress?.Report(new(CapturePhase.Downloading, count, points)));
        progress?.Report(new(CapturePhase.ReadingStatistics, points, points));
        double Stat(string metric = "") => Number($"FETC:{signal}{metric}?", token);
        var statistics = new MeasurementStatistics(Stat(), Stat(":ACDC"), Stat(":MIN"), Stat(":MAX"), Stat(":LOW"), Stat(":HIGH"));
        CheckErrors(token);
        VerifyTriggerProtection(token);
        return new(snapshot!.Identity, started, clock.UtcNow, settings, start, samples, statistics);
    }

    private bool UsesStandardEventFaultOutput(CancellationToken token)
    {
        if (!ScpiParser.Boolean(channel.Query("OUTP:DFI?", token)) ||
            channel.Query("DIG:FUNC?", token).StartsWith("DIG", StringComparison.OrdinalIgnoreCase)) return false;
        string source = channel.Query("OUTP:DFI:SOUR?", token).Trim();
        return source is "ESB" or "RQS";
    }

    private CancellationTokenSource EnterTriggerOperation(CancellationToken token)
    {
        lock (controlGate)
        {
            if (shutdownPending) throw new OperationCanceledException("Output-off verification is pending.");
            if (CaptureOwnsBuffer) throw new InvalidOperationException("Finish the active capture or trigger operation first.");
            var lifetime = CancellationTokenSource.CreateLinkedTokenSource(token, control.Token);
            Volatile.Write(ref triggerOperation, 1);
            return lifetime;
        }
    }

    private void RequireFeature(QualifiedFeature feature)
    {
        EnsureReady();
        if (CalibrationInProgress) throw new InvalidOperationException("Finish calibration before using the digitizer or triggers.");
        if (!CanMonitor || !IsQualified(feature))
            throw new InvalidOperationException("This operation requires SCPI and bench qualification for the selected serial profile.");
    }

    private void RequireIdleTriggers(CancellationToken token)
    {
        if (WaitingForTrigger(token)) throw new InvalidOperationException("An instrument trigger is already armed. Abort it before starting a new operation.");
    }

    private bool WaitingForTrigger(CancellationToken token) =>
        (ScpiParser.Register(channel.Query("STAT:OPER:COND?", token)) & 32) != 0;

    private void VerifyTriggerProtection(CancellationToken token)
    {
        int questionable = ScpiParser.Register(channel.Query("STAT:QUES:COND?", token));
        var status = Agilent66332AProfile.Status(0, questionable, clock.UtcNow);
        if (status.HasFault) throw new IOException("Protection condition: " + status.FaultFlags);
        if ((questionable & 16384) != 0) throw new IOException("The low current measurement range overloaded. Select high range and retry.");
    }

    private static void ValidateWait(TimeSpan wait)
    {
        if (wait <= TimeSpan.Zero || wait > TimeSpan.FromDays(1))
            throw new ArgumentException("Set a positive wait limit of at most 24 hours.");
    }

    private static double SweepSeconds(SweepSettings settings) =>
        (settings.Points + Math.Max(0d, settings.OffsetPoints)) * settings.IntervalSeconds;
}
