using Agilent66332A.Core.Models;
using Agilent66332A.Core.Policies;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Devices;

public partial class AgilentClient
{
    public Task<TransientSettings> ReadTransientAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureBasicMode();
        return Guard(() => ReadTransient(t));
    }, token);

    public async Task<DeviceSnapshot> RunTransientAsync(TransientRequest request, Func<bool> takeFireRequest,
        IProgress<TransientProgress>? progress = null, CancellationToken token = default)
    {
        using var lifetime = EnterTriggerOperation(token);
        token = lifetime.Token;
        bool prepared = false;
        try
        {
            await Queue(t =>
            {
                if (!Enum.IsDefined(request.Trigger)) throw new ArgumentException("Select a transient trigger.");
                RequireFeature(request.Trigger == TransientTrigger.Serial ? QualifiedFeature.TransientBus : QualifiedFeature.TransientImmediate);
                ValidateWait(request.WaitLimit);
                var before = ReadState(t);
                SettingsPolicy.Validate(request.Pending, before);
                if (before.Status.HasFault) throw new InvalidOperationException("Clear the protection fault before arming.");
                RequireIdleTriggers(t);

                prepared = true;
                channel.Write("SYST:REM", t);
                WriteChecked("TRIG:SOUR BUS", t);
                WriteChecked($"VOLT:TRIG {ScpiParser.Format(request.Pending.VoltageV)}", t);
                WriteChecked($"CURR:TRIG {ScpiParser.Format(request.Pending.CurrentLimitA)}", t);
                var actual = ReadTransient(t).Pending;
                if (!before.Capabilities.Voltage.Accepts(request.Pending.VoltageV, actual.VoltageV) ||
                    !before.Capabilities.Current.Accepts(request.Pending.CurrentLimitA, actual.CurrentLimitA))
                    throw new IOException("Pending transient levels did not match readback.");

                triggersMayBeArmed = true;
                continuousMayBeEnabled = request.Continuous;
                WriteChecked(request.Continuous ? "INIT:CONT:SEQ1 ON" : "INIT:SEQ1", t);
                if (!WaitingForTrigger(t)) throw new IOException("The supply did not report an armed trigger.");
                return true;
            }, token, configuration: true).ConfigureAwait(false);

            int fired = 0;
            double start = clock.Seconds;
            progress?.Report(new(true, fired));
            while (clock.Seconds - start < request.WaitLimit.TotalSeconds)
            {
                token.ThrowIfCancellationRequested();
                if (takeFireRequest())
                {
                    var verified = await Queue(t => FireTransient(request, t), token).ConfigureAwait(false);
                    progress?.Report(new(request.Continuous, ++fired, verified.Settings));
                    if (!request.Continuous) return verified;
                    start = clock.Seconds;
                }
                else await Queue(t => { VerifyTriggerProtection(t); return true; }, token).ConfigureAwait(false);
                await clock.DelayAsync(TimeSpan.FromMilliseconds(150), token).ConfigureAwait(false);
            }
            throw new TimeoutException("The armed output trigger reached its wait limit.");
        }
        catch
        {
            if (prepared || !channel.Synchronized) await RequestOutputOffAsync("Transient stopped").ConfigureAwait(false);
            throw;
        }
        finally { Volatile.Write(ref triggerOperation, 0); }
    }

    private DeviceSnapshot FireTransient(TransientRequest request, CancellationToken token)
    {
        var before = ReadState(token);
        SettingsPolicy.Validate(request.Pending, before);
        if (before.Status.HasFault || !WaitingForTrigger(token)) throw new IOException("The output trigger is no longer ready.");
        WriteChecked(request.Trigger == TransientTrigger.Immediate ? "TRIG:TRAN" : "*TRG", token);
        var actual = ReadState(token);
        bool waiting = (actual.Status.OperationCondition & 32) != 0;
        if (waiting != request.Continuous || actual.Status.HasFault || actual.Output != before.Output ||
            !actual.Capabilities.Voltage.Accepts(request.Pending.VoltageV, actual.Settings.VoltageV) ||
            !actual.Capabilities.Current.Accepts(request.Pending.CurrentLimitA, actual.Settings.CurrentLimitA))
            throw new IOException("The triggered output change could not be verified.");
        triggersMayBeArmed = waiting;
        return actual;
    }

    public Task<TransientSettings> AbortTriggersAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureBasicMode();
        if ((QualifiedFeatures & (QualifiedFeature.Acquisition | QualifiedFeature.TransientImmediate | QualifiedFeature.TransientBus)) == 0)
            throw new InvalidOperationException("Abort requires trigger qualification for this serial profile.");
        return Guard(() =>
        {
            channel.Write("SYST:REM", t);
            AbortTriggers(t, disableContinuous: true);
            return ReadTransient(t);
        });
    }, token, configuration: true);

    private TransientSettings ReadTransient(CancellationToken token) =>
        new(new(Number("VOLT:TRIG?", token), Number("CURR:TRIG?", token)), WaitingForTrigger(token));

    private void StopTriggersForShutdown(CancellationToken token)
    {
        if (triggersMayBeArmed || continuousMayBeEnabled)
            AbortTriggers(token, continuousMayBeEnabled);
    }

    private void AbortTriggers(CancellationToken token, bool disableContinuous)
    {
        if (disableContinuous) channel.Write("INIT:CONT:SEQ1 OFF", token);
        channel.Write("ABOR", token);
        if (WaitingForTrigger(token)) throw new IOException("The supply still reports a waiting trigger.");
        continuousMayBeEnabled = triggersMayBeArmed = false;
    }
}
