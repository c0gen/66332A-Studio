using Agilent66332A.Core.Models;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Devices;

public partial class AgilentClient
{
    private bool liveControlVerified;
    public string? LiveControlStatus { get; private set; }

    private DeviceSnapshot VerifyBasicControl(DeviceSnapshot state, CancellationToken token)
    {
        if (state.Output != OutputState.Off)
        {
            LiveControlStatus = "Turn output off, then reconnect to verify basic control.";
            return state;
        }

        if (state.Status.HasFault || state.Status.WaitingForTrigger || state.Status.CalibrationInProgress)
        {
            LiveControlStatus = "Resolve any fault, armed trigger, or active calibration, then reconnect to verify basic control.";
            return state;
        }

        int points = ScpiParser.Register(channel.Query("SENS:SWE:POIN?", token));
        if (points is < 64 or > 4096)
        {
            LiveControlStatus = "Live verification requires a voltage sweep of 64 to 4,096 points. Adjust the sweep and reconnect.";
            return state;
        }

        channel.Write("SYST:REM", token);
        channel.InterruptVoltageWaveform(points, token);
        using (var recovery = CancellationTokenSource.CreateLinkedTokenSource(token))
        {
            recovery.CancelAfter(TimeSpan.FromSeconds(5));
            channel.Recover(true, recovery.Token, VerifyRecoveryIdentity);
            SetOffVerified(recovery.Token);
        }
        channel.Write("SYST:LOC", token);
        var verified = ReadState(token);
        if (verified.Output != OutputState.Off || verified.Status.HasFault)
            throw new IOException("Live control verification requires output off and no reported faults.");

        // Use the 66332A programming tolerances backed by the unloaded bench measurements.
        var capabilities = verified.Capabilities with
        {
            Current = verified.Capabilities.Current with { Resolution = .00132 },
            Ovp = verified.Capabilities.Ovp with { Resolution = .1 },
            HardwareValidated = true
        };
        token.ThrowIfCancellationRequested();
        liveControlVerified = true;
        LiveControlStatus = "Live verification passed. Basic controls are available for this connection.";
        return snapshot = verified with { Capabilities = capabilities };
    }
}
