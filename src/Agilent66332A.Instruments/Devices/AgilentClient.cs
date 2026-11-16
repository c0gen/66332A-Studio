using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Core.Policies;
using Agilent66332A.Instruments.Scpi;
using Agilent66332A.Instruments.Serial;

namespace Agilent66332A.Instruments.Devices;

public partial class AgilentClient : IInstrumentClient, IAcquisitionClient, ITransientClient
{
    private readonly IByteTransport transport;
    private readonly IMonotonicClock clock;
    private readonly CommandWorker worker = new();
    private readonly ScpiChannel channel;
    private readonly bool profileValidated;
    private readonly IHardwareQualification? qualification;
    private readonly bool verifyBasicControlOnConnect;
    private HardwareQualification? qualified;
    private readonly object controlGate = new();
    private CancellationTokenSource control = new();
    private bool shutdownPending;
    private Task<ShutdownResult>? queuedShutdown;
    private bool identified;
    private bool lost;
    private long session;
    private static long nextSession;
    private long cycle;
    private DeviceSnapshot? snapshot;
    public string SourceKind { get; }
    public bool CanConfigure => BreakValidated && IsSessionUsable && !shutdownPending;
    public bool IsSessionUsable => identified && !lost && transport.IsOpen && channel.Synchronized;
    public bool CanMonitor => Language == ProgrammingLanguage.Scpi;
    public bool CanMeasureDc => Volatile.Read(ref measurementSweepSeconds) is < 0 or (> 0 and <= 1);
    private bool BreakValidated => profileValidated || liveControlVerified || qualified?.BreakValidated == true;
    private QualifiedFeature QualifiedFeatures => profileValidated ? QualifiedFeature.All : qualified?.Features ?? QualifiedFeature.None;
    private bool IsQualified(QualifiedFeature feature) => CanConfigure && (QualifiedFeatures & feature) == feature;
    public DeviceSnapshot? Snapshot => Volatile.Read(ref snapshot);
    public event Action<string>? Trace;

    public AgilentClient(IByteTransport transport, IMonotonicClock clock, bool profileValidated = false, string sourceKind = "Hardware",
        IHardwareQualification? qualification = null, bool verifyBasicControlOnConnect = false)
    {
        this.transport = transport; this.clock = clock; this.profileValidated = profileValidated && sourceKind == "Simulator"; SourceKind = sourceKind;
        this.qualification = qualification;
        this.verifyBasicControlOnConnect = verifyBasicControlOnConnect && sourceKind == "Hardware";
        channel = new(transport, line => Trace?.Invoke(line), controlGate);
    }
    private Task<T> Queue<T>(Func<CancellationToken, T> operation, CancellationToken token, bool configuration = false, int priority = 1)
    {
        CancellationToken captured;
        lock (controlGate)
        {
            if (shutdownPending) return Task.FromException<T>(new OperationCanceledException("Output-off verification is pending."));
            captured = control.Token;
        }
        return worker.Enqueue(() =>
        {
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(captured, token);
            linked.Token.ThrowIfCancellationRequested();
            if (configuration && !CanConfigure) throw new InvalidOperationException("Hardware configuration requires adapter/Break validation.");
            return operation(linked.Token);
        }, priority);
    }
    public Task<DeviceSnapshot> ConnectAsync(ConnectionProfile profile, CancellationToken token = default) => Queue(t =>
    {
        if (transport.IsOpen) throw new InvalidOperationException("Disconnect before connecting again.");
        profile.Validate();
        liveControlVerified = false;
        LiveControlStatus = null;
        try
        {
            transport.Open(profile);
            channel.Opened(profile.BaudRate);
            Language = ReadProgrammingLanguage(t);
            var identity = Language == ProgrammingLanguage.Scpi
                ? ScpiParser.Identity(channel.Query("*IDN?", t, 5)) : ReadCompatibilityIdentity(t);
            identified = true; lost = false; session = Interlocked.Increment(ref nextSession);
            bool scpi = Language == ProgrammingLanguage.Scpi;
            qualified = scpi ? qualification?.Match(identity, profile) : null;
            var caps = new DeviceCapabilities(
                new(scpi ? Number("VOLT? MIN", t) : 0, scpi ? Number("VOLT? MAX", t) : 20.475, qualified?.Voltage.Resolution ?? .005),
                new(scpi ? Number("CURR? MIN", t) : 0, scpi ? Number("CURR? MAX", t) : 5.1188, qualified?.Current.Resolution ?? .001),
                new(scpi ? Number("VOLT:PROT? MIN", t) : 0, scpi ? Number("VOLT:PROT? MAX", t) : 22, qualified?.Ovp.Resolution ?? .005),
                new(0, 2147483.647, qualified?.OcpDelay.Resolution ?? .001), false);
            if (qualified is { } candidate && scpi &&
                !(SameLimits(caps.Voltage, candidate.Voltage) && SameLimits(caps.Current, candidate.Current) &&
                  SameLimits(caps.Ovp, candidate.Ovp) && SameLimits(caps.OcpDelay, candidate.OcpDelay)))
            {
                qualified = null;
                Trace?.Invoke("Reported limits differ from the bench qualification. Hardware control stays disabled.");
            }
            caps = caps with { HardwareValidated = BreakValidated && SourceKind == "Hardware", QualifiedFeatures = QualifiedFeatures };
            snapshot = new(session, identity, caps, new(0, 0, clock.UtcNow), new(caps.Ovp.Maximum, false, .08), OutputState.Unknown,
                Agilent66332AProfile.Status(0, 0, clock.UtcNow), clock.UtcNow);
            measurementSweepSeconds = -1;
            if (scpi && IsQualified(QualifiedFeature.Acquisition)) RefreshMeasurementDuration(t);
            if (!scpi) return snapshot;
            var state = ReadState(t);
            return verifyBasicControlOnConnect && !BreakValidated ? VerifyBasicControl(state, t) : state;
        }
        catch
        {
            if (identified) OffCore("Initialization failed");
            transport.Close(); identified = false; throw;
        }
    }, token);
    private static bool SameLimits(NumericRange observed, NumericRange qualified) =>
        Math.Abs(observed.Minimum - qualified.Minimum) <= qualified.Resolution / 2 &&
        Math.Abs(observed.Maximum - qualified.Maximum) <= qualified.Resolution / 2;
    private double Number(string command, CancellationToken token) => ScpiParser.Number(channel.Query(command, token));
    private void EnsureReady()
    {
        if (!identified || !transport.IsOpen || lost || snapshot is null) throw new IOException("No verified session. Connect explicitly.");
    }
    private DeviceSnapshot ReadState(CancellationToken token)
    {
        EnsureReady();
        var previous = snapshot!;
        if (!CanMonitor) return previous;
        var settings = new AcknowledgedSettings(Number("VOLT?", token), Number("CURR?", token), clock.UtcNow);
        var protections = new ProtectionSettings(Number("VOLT:PROT?", token), ScpiParser.Boolean(channel.Query("CURR:PROT:STAT?", token)), Number("OUTP:PROT:DEL?", token));
        var output = ScpiParser.Boolean(channel.Query("OUTP?", token)) ? OutputState.On : OutputState.Off;
        var status = Agilent66332AProfile.Status(ScpiParser.Register(channel.Query("STAT:OPER:COND?", token)),
            ScpiParser.Register(channel.Query("STAT:QUES:COND?", token)), clock.UtcNow);
        snapshot = previous with { Settings = settings, Protections = protections, Output = output, Status = status, ObservedUtc = clock.UtcNow };
        return snapshot;
    }
    private T Guard<T>(Func<T> action)
    {
        try { return action(); }
        catch (OperationCanceledException)
        {
            if (identified && !shutdownPending) OffCore("Operation cancelled");
            throw;
        }
        catch
        {
            if (identified) OffCore("Communication or verification failure");
            MarkLost(); throw;
        }
    }
    private void MarkLost()
    {
        lost = true;
        if (snapshot is { } state) snapshot = state with { Output = OutputState.Unknown };
    }
    public Task<DeviceSnapshot> ReadSnapshotAsync(CancellationToken token = default) => Queue(t => Guard(() => ReadState(t)), token, priority: 2);
    public Task<MeasurementPair> MeasureAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureBasicMode();
        if (!CanMeasureDc) throw new InvalidOperationException("The configured sweep is too long for live DC polling.");
        return Guard(() =>
        {
            DateTimeOffset vs = clock.UtcNow;
            double vm = clock.Seconds;
            double v = Number("MEAS:VOLT?", t);
            var voltage = new MeasurementSample(v, vs, clock.UtcNow, vm);
            DateTimeOffset cs = clock.UtcNow;
            double cm = clock.Seconds;
            double i = Number("MEAS:CURR?", t);
            var current = new MeasurementSample(i, cs, clock.UtcNow, cm);
            var state = ReadState(t);
            t.ThrowIfCancellationRequested();
            if (state.Status.MeasurementOverload) current = current with { Value = null };
            var quality = state.Status.HasFault ? SampleQuality.Interrupted : state.Status.MeasurementOverload ? SampleQuality.Missing : SampleQuality.Valid;
            return new MeasurementPair(session, ++cycle, voltage, current, quality, state);
        });
    }, token, priority: 2);

    private void EnsureBasicMode()
    {
        EnsureReady();
        if (!CanMonitor) throw new InvalidOperationException("Switch to SCPI in Service to use normal monitoring and control.");
        if (CaptureOwnsBuffer) throw new InvalidOperationException("Finish the active capture or trigger operation first.");
        if (CalibrationInProgress) throw new InvalidOperationException("Finish calibration before using normal controls.");
    }
    private void CheckErrors(CancellationToken token)
    {
        var error = ReadAndRecordDeviceError(token);
        if (error.Code != 0) throw new IOException($"Device error {error.Code}: {error.Message}");
    }
    public Task<ApplyResult> ApplyAsync(RequestedSettings settings, CancellationToken token = default) => Queue(t =>
    {
        EnsureBasicMode();
        var before = Guard(() => ReadState(t));
        SettingsPolicy.Validate(settings, before);
        return Guard(() =>
        {
            channel.Write("SYST:REM", t);
            foreach (var change in SettingsPolicy.Order(settings, before.Settings))
            {
                t.ThrowIfCancellationRequested();
                channel.Write($"{change.Field} {ScpiParser.Format(change.Value)}", t);
                double actual = Number(change.Field + "?", t);
                var range = change.Field == "VOLT" ? before.Capabilities.Voltage : before.Capabilities.Current;
                if (!range.Accepts(change.Value, actual)) throw new IOException($"{change.Field} readback mismatch: requested {change.Value}, acknowledged {actual}.");
                CheckErrors(t);
            }
            var final = ReadState(t);
            if (!before.Capabilities.Voltage.Accepts(settings.VoltageV, final.Settings.VoltageV) ||
                !before.Capabilities.Current.Accepts(settings.CurrentLimitA, final.Settings.CurrentLimitA) || final.Status.HasFault)
                throw new IOException("Final settings or fault verification failed.");
            return new ApplyResult(settings, final.Settings);
        });
    }, token, configuration: true);
    public Task<DeviceSnapshot> SetOutputAsync(bool enabled, CancellationToken token = default) => Queue(t =>
    {
        EnsureBasicMode();
        var before = Guard(() => ReadState(t));
        if (enabled)
        {
            SettingsPolicy.Validate(new(before.Settings.VoltageV, before.Settings.CurrentLimitA), before);
            if (before.Status.HasFault) throw new InvalidOperationException("Clear the protection fault before enabling output.");
        }
        return Guard(() =>
        {
            channel.Write("SYST:REM", t);
            channel.Write(enabled ? "OUTP 1" : "OUTP 0", t);
            var state = ReadState(t);
            if (state.Output != (enabled ? OutputState.On : OutputState.Off) || state.Status.HasFault && enabled)
                throw new IOException("Output verification failed.");
            return state;
        });
    }, token, configuration: enabled);
    public Task<DeviceSnapshot> ApplyProtectionsAsync(ProtectionSettings settings, CancellationToken token = default) => Queue(t =>
    {
        EnsureBasicMode();
        var state = Guard(() => ReadState(t));
        SettingsPolicy.ValidateProtections(settings, state);
        return Guard(() =>
        {
            SetOffVerified(t);
            channel.Write("SYST:REM", t);
            channel.Write($"VOLT:PROT {ScpiParser.Format(settings.OvpV)}", t);
            if (!state.Capabilities.Ovp.Accepts(settings.OvpV, Number("VOLT:PROT?", t))) throw new IOException("OVP readback mismatch.");
            CheckErrors(t);
            channel.Write($"CURR:PROT:STAT {(settings.OcpEnabled ? 1 : 0)}", t);
            if (ScpiParser.Boolean(channel.Query("CURR:PROT:STAT?", t)) != settings.OcpEnabled) throw new IOException("OCP readback mismatch.");
            CheckErrors(t);
            channel.Write($"OUTP:PROT:DEL {ScpiParser.Format(settings.OcpDelaySeconds)}", t);
            if (!state.Capabilities.OcpDelay.Accepts(settings.OcpDelaySeconds, Number("OUTP:PROT:DEL?", t))) throw new IOException("OCP delay readback mismatch.");
            CheckErrors(t);
            var final = ReadState(t);
            if (final.Output != OutputState.Off) throw new IOException("Output changed during protection editing.");
            return final;
        });
    }, token, configuration: true);
    public Task<DeviceSnapshot> ClearProtectionAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureBasicMode();
        return Guard(() =>
        {
            SetOffVerified(t);
            channel.Write("SYST:REM", t);
            channel.Write("OUTP:PROT:CLE", t);
            CheckErrors(t);
            var state = ReadState(t);
            if (state.Output != OutputState.Off) { SetOffVerified(t); throw new IOException("Output changed during fault clear."); }
            return state;
        });
    }, token, configuration: true);
    private void SetOffVerified(CancellationToken token)
    {
        channel.Write("OUTP 0", token);
        if (ScpiParser.Boolean(channel.Query("OUTP?", token))) throw new IOException("Output-off readback was not confirmed.");
        if (snapshot is { } state) snapshot = state with { Output = OutputState.Off, ObservedUtc = clock.UtcNow };
    }
    private void InvalidatePending()
    {
        lock (controlGate)
        {
            var old = control;
            control = new();
            old.Cancel();
            // Old tokens remain valid for queued requests; CTS is disposed with the client.
        }
    }
    private ShutdownResult OffCore(string reason)
    {
        InvalidatePending();
        var requested = clock.UtcNow;
        using var budget = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            if (!identified || !transport.IsOpen) throw new IOException("No identified open connection; check the supply locally.");
            bool recovered = !channel.Synchronized;
            if (recovered && !CanMonitor) throw new IOException("Compatibility mode cannot verify identity after Break. Check the supply locally.");
            if (recovered) channel.Recover(BreakValidated, budget.Token, VerifyRecoveryIdentity);
            if (Language == ProgrammingLanguage.Unknown) Language = ReadProgrammingLanguage(budget.Token);
            if (!CanMonitor)
            {
                if (Language != ProgrammingLanguage.Compatibility) throw new IOException("The instrument language is unknown. Check its front panel.");
                channel.Write("OUT 0", budget.Token);
                if (snapshot is { } state) snapshot = state with { Output = OutputState.Unknown };
                if (recovered) lost = true;
                return new(reason, requested, null, ShutdownOutcome.Unverified, "Sent an off request in Compatibility mode. Check the supply to verify output off.");
            }
            SetOffVerified(budget.Token);
            var verified = clock.UtcNow;
            var cleanup = new List<string>();
            try { StopTriggersForShutdown(budget.Token); } catch (Exception e) { cleanup.Add("Trigger disarm unverified. " + e.Message); }
            try { EndCalibrationForShutdown(budget.Token); } catch (Exception e) { cleanup.Add("Calibration exit unverified. " + e.Message); }
            try { channel.Write("SYST:LOC", budget.Token); } catch (Exception e) { cleanup.Add("Local control unverified. " + e.Message); }
            foreach (string message in cleanup) Trace?.Invoke(message);
            if (cleanup.Count > 0) lost = true;
            if (recovered) lost = true;
            return new(reason, requested, verified, ShutdownOutcome.VerifiedOff, cleanup.Count == 0 ? null : string.Join(" ", cleanup));
        }
        catch (Exception e) { MarkLost(); return new(reason, requested, null, ShutdownOutcome.Unverified, e.Message); }
    }
    private void VerifyRecoveryIdentity(CancellationToken token)
    {
        var actual = ScpiParser.Identity(channel.Query("*IDN?", token));
        if (snapshot?.Identity is not { } previous || actual.Firmware != previous.Firmware || actual.SerialNumber != previous.SerialNumber ||
            !StringComparer.OrdinalIgnoreCase.Equals(actual.Manufacturer, previous.Manufacturer))
            throw new IOException("The device identity or language changed during recovery.");
    }
    public Task<ShutdownResult> RequestOutputOffAsync(string reason)
    {
        lock (controlGate)
        {
            if (queuedShutdown is not null) return queuedShutdown;
            shutdownPending = true;
            InvalidatePending();
            var pending = worker.Enqueue(() => OffCore(reason), priority: 0);
            queuedShutdown = pending;
            return FinishOffAsync(pending);
        }
    }
    private async Task<ShutdownResult> FinishOffAsync(Task<ShutdownResult> pending)
    {
        try { return await pending.ConfigureAwait(false); }
        finally
        {
            lock (controlGate)
            {
                queuedShutdown = null;
                shutdownPending = false;
            }
        }
    }
    public async Task DisconnectAsync()
    {
        if (identified && transport.IsOpen) await RequestOutputOffAsync("Disconnect").ConfigureAwait(false);
        await worker.Enqueue(() => { transport.Close(); identified = false; return true; }, 0).ConfigureAwait(false);
    }
    public async ValueTask DisposeAsync()
    {
        await DisconnectAsync().ConfigureAwait(false);
        await worker.DisposeAsync().ConfigureAwait(false);
        transport.Dispose(); control.Dispose();
    }
}
