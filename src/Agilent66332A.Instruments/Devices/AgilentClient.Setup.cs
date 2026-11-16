using System.Globalization;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Core.Policies;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Devices;

public partial class AgilentClient : IInstrumentSetup
{
    public Task<InstrumentSetupSnapshot> ReadInstrumentSetupAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureAdvancedScpi();
        return Guard(() => ReadInstrumentSetup(t));
    }, token);

    public Task<InstrumentSetupSnapshot> ConfigureDigitalPortAsync(DigitalPortSettings settings, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!Enum.IsDefined(settings.Function) || !Enum.IsDefined(settings.Inhibit) || !Enum.IsDefined(settings.FaultSource))
            throw new ArgumentException("Select a valid port function, inhibit mode and fault source.", nameof(settings));
        return Queue(t =>
        {
            EnsureAdvancedScpi(QualifiedFeature.DigitalIo);
            return Guard(() =>
            {
                SetOffVerified(t);
                var before = ReadDigitalPort(t);
                if (before == settings) return ReadInstrumentSetup(t);
                SendCheckedAdvanced("SYST:REM", t);
                if (before.FaultIndicatorEnabled && (settings.Function != before.Function || settings.FaultSource != before.FaultSource || !settings.FaultIndicatorEnabled))
                {
                    SendCheckedAdvanced("OUTP:DFI 0", t);
                    before = before with { FaultIndicatorEnabled = false };
                }
                // Configure inhibit and fault behavior before connecting those signals to the shared port.
                if (before.Inhibit != settings.Inhibit) SendCheckedAdvanced("OUTP:RI:MODE " + InhibitHeader(settings.Inhibit), t);
                if (before.FaultSource != settings.FaultSource) SendCheckedAdvanced("OUTP:DFI:SOUR " + FaultHeader(settings.FaultSource), t);
                if (before.Function != settings.Function)
                    SendCheckedAdvanced("DIG:FUNC " + (settings.Function == DigitalPortFunction.DigitalIo ? "DIG" : "RIDF"), t);
                if (before.FaultIndicatorEnabled != settings.FaultIndicatorEnabled)
                    SendCheckedAdvanced("OUTP:DFI " + BooleanHeader(settings.FaultIndicatorEnabled), t);
                var after = ReadInstrumentSetup(t);
                if (after.Port != settings) throw new IOException("Digital port settings did not match instrument readback.");
                return after;
            });
        }, token, configuration: true);
    }

    public Task<DigitalPortReading> WriteDigitalPortAsync(int outputMask, CancellationToken token = default)
    {
        if (outputMask is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(outputMask), "Use a digital output value from 0 through 7.");
        return Queue(t =>
        {
            EnsureAdvancedScpi(QualifiedFeature.DigitalIo);
            if (Guard(() => ReadPortFunction(t)) != DigitalPortFunction.DigitalIo)
                throw new InvalidOperationException("Select Digital I/O mode before writing the port.");
            return Guard(() =>
            {
                SetOffVerified(t);
                SendCheckedAdvanced("SYST:REM", t);
                SendCheckedAdvanced("DIG:DATA " + outputMask.ToString(CultureInfo.InvariantCulture), t);
                var actual = ReadDigitalPins(t);
                if ((actual.RawValue & 3) != (outputMask & 3)) throw new IOException("Digital outputs 0 and 1 did not match readback.");
                return actual;
            });
        }, token, configuration: true);
    }

    public Task<InstrumentSetupSnapshot> ConfigureRelayAsync(RelaySettings settings, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!Enum.IsDefined(settings.Polarity)) throw new ArgumentException("Select a valid relay polarity.", nameof(settings));
        return Queue(t =>
        {
            EnsureAdvancedScpi(QualifiedFeature.Relay);
            RequireRelay(t);
            return Guard(() =>
            {
                SendCheckedAdvanced("SYST:REM", t);
                SetOffVerified(t);
                var before = ReadRelay(t);
                if (before.Polarity != settings.Polarity)
                {
                    if (before.Closed) SendCheckedAdvanced("OUTP:REL 0", t);
                    if (ReadRelay(t).Closed) throw new IOException("Relay isolation could not be verified before changing polarity.");
                    SendCheckedAdvanced("OUTP:REL:POL " + (settings.Polarity == RelayPolarity.Normal ? "NORM" : "REV"), t);
                }
                if (ReadRelay(t).Closed != settings.Closed)
                    SendCheckedAdvanced("OUTP:REL " + BooleanHeader(settings.Closed), t);
                var after = ReadInstrumentSetup(t);
                if (after.Relay != settings) throw new IOException("Relay settings did not match instrument readback.");
                if (ReadState(t).Output != OutputState.Off) throw new IOException("Output changed during relay configuration.");
                return after;
            });
        }, token, configuration: true);
    }

    public Task<DeviceSnapshot> SetOutputWithoutRelayAsync(bool enabled, CancellationToken token = default) => Queue(t =>
    {
        EnsureAdvancedScpi(QualifiedFeature.Relay);
        RequireRelay(t);
        var before = Guard(() => ReadState(t));
        if (enabled)
        {
            SettingsPolicy.Validate(new(before.Settings.VoltageV, before.Settings.CurrentLimitA), before);
            if (before.Status.HasFault) throw new InvalidOperationException("Clear the protection fault before enabling output.");
        }
        return Guard(() =>
        {
            SendCheckedAdvanced("SYST:REM", t);
            var relay = ReadRelay(t);
            SendCheckedAdvanced("OUTP " + BooleanHeader(enabled) + ",NOR", t);
            var actual = ReadState(t);
            if (actual.Output != (enabled ? OutputState.On : OutputState.Off) ||
                (enabled && actual.Status.HasFault) || ReadRelay(t) != relay)
                throw new IOException("Independent output switching did not match instrument readback.");
            return actual;
        });
    }, token, configuration: true);

    public Task<InstrumentSetupSnapshot> ConfigurePanelDisplayAsync(PanelDisplaySettings settings, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        if (!Enum.IsDefined(settings.Mode) || !ValidPanelText(settings.Text))
            throw new ArgumentException("Panel text accepts up to 14 printable ASCII characters.", nameof(settings));
        return Queue(t =>
        {
            EnsureAdvancedScpi(QualifiedFeature.Service);
            return Guard(() =>
            {
                var before = ReadPanelDisplay(t);
                if (before.Mode != settings.Mode)
                    SendCheckedAdvanced("DISP:MODE " + (settings.Mode == PanelDisplayMode.Normal ? "NORM" : "TEXT"), t);
                if (settings.Mode == PanelDisplayMode.Text && before.Text != settings.Text)
                    SendCheckedAdvanced("DISP:TEXT \"" + settings.Text.Replace("\"", "\"\"") + "\"", t);
                if (before.Enabled != settings.Enabled) SendCheckedAdvanced("DISP " + BooleanHeader(settings.Enabled), t);
                var after = ReadInstrumentSetup(t);
                if (after.Display.Mode != settings.Mode || after.Display.Enabled != settings.Enabled ||
                    (settings.Mode == PanelDisplayMode.Text && after.Display.Text != settings.Text))
                    throw new IOException("Panel display settings did not match instrument readback.");
                return after;
            });
        }, token, configuration: true);
    }

    public Task SetPanelControlAsync(PanelControlMode mode, CancellationToken token = default)
    {
        string command = mode switch
        {
            PanelControlMode.Local => "SYST:LOC", PanelControlMode.Remote => "SYST:REM",
            PanelControlMode.RemoteLockout => "SYST:RWL", _ => throw new ArgumentOutOfRangeException(nameof(mode))
        };
        return Queue(t =>
        {
            EnsureAdvancedScpi(QualifiedFeature.Service);
            return Guard(() => { SendCheckedAdvanced(command, t); return true; });
        }, token, configuration: true);
    }

    private void EnsureAdvancedScpi(QualifiedFeature? feature = null)
    {
        EnsureReady();
        if (Language != ProgrammingLanguage.Scpi) throw new InvalidOperationException("Return to SCPI before using these controls.");
        if (CaptureOwnsBuffer) throw new InvalidOperationException("Finish the active capture or trigger operation before using these controls.");
        if (CalibrationInProgress) throw new InvalidOperationException("Finish the current calibration before using these controls.");
        if (feature is { } required && !IsQualified(required)) throw new InvalidOperationException("This instrument and connection profile have not been validated for the requested feature.");
    }

    private void SendCheckedAdvanced(string command, CancellationToken token)
    {
        channel.Write(command, token);
        CheckErrors(token);
    }

    private InstrumentSetupSnapshot ReadInstrumentSetup(CancellationToken token)
    {
        var options = ReadOptionCodes(token);
        var version = ScpiParser.Number(channel.Query("SYST:VERS?", token)).ToString("0.0########", CultureInfo.InvariantCulture);
        var bandwidth = ReadOutputBandwidth(token);
        var port = ReadDigitalPort(token);
        return new(snapshot!.Identity, version, Array.AsReadOnly(options), bandwidth, port,
            port.Function == DigitalPortFunction.DigitalIo ? ReadDigitalPins(token) : null,
            options.Contains("760", StringComparer.Ordinal) ? ReadRelay(token) : null, ReadPanelDisplay(token), clock.UtcNow);
    }

    private OutputBandwidth ReadOutputBandwidth(CancellationToken token) => Number("VOLT:ALC:BAND?", token) switch
    {
        15000 => OutputBandwidth.Normal, 60000 => OutputBandwidth.Fast, _ => OutputBandwidth.Unknown
    };

    private string[] ReadOptionCodes(CancellationToken token) => channel.Query("*OPT?", token).Trim()
        .Split([',', ' ', '\t'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Select(option => option.Trim('"', '\''))
        .Where(option => option is not ("0" or "")).Distinct(StringComparer.Ordinal).ToArray();

    private void RequireRelay(CancellationToken token)
    {
        if (!Guard(() => ReadOptionCodes(token)).Contains("760", StringComparer.Ordinal))
            throw new NotSupportedException("This instrument has no Option 760 output relay.");
    }

    private DigitalPortSettings ReadDigitalPort(CancellationToken token) => new(ReadPortFunction(token),
        SetupHeader("OUTP:RI:MODE?", token) switch
        {
            "OFF" => InhibitMode.Off, "LATC" or "LATCH" or "LATCHING" => InhibitMode.Latching,
            "LIVE" => InhibitMode.Live, _ => throw new FormatException("Unexpected remote inhibit mode.")
        }, ScpiParser.Boolean(channel.Query("OUTP:DFI?", token)),
        SetupHeader("OUTP:DFI:SOUR?", token) switch
        {
            "OFF" => FaultIndicatorSource.Off, "QUES" or "QUESTIONABLE" => FaultIndicatorSource.Questionable,
            "OPER" or "OPERATION" => FaultIndicatorSource.Operation, "ESB" => FaultIndicatorSource.StandardEvent,
            "RQS" => FaultIndicatorSource.RequestService, _ => throw new FormatException("Unexpected fault indicator source.")
        });

    private DigitalPortFunction ReadPortFunction(CancellationToken token) => SetupHeader("DIG:FUNC?", token) switch
    {
        "RIDF" or "RIDFI" => DigitalPortFunction.InhibitFault, "DIG" or "DIGIO" => DigitalPortFunction.DigitalIo,
        _ => throw new FormatException("Unexpected digital port function.")
    };

    private DigitalPortReading ReadDigitalPins(CancellationToken token)
    {
        int raw = ScpiParser.Register(channel.Query("DIG:DATA?", token));
        return raw <= 7 ? new(raw, clock.UtcNow) : throw new FormatException("Unexpected digital port value.");
    }

    private RelaySettings ReadRelay(CancellationToken token) => new(ScpiParser.Boolean(channel.Query("OUTP:REL?", token)),
        SetupHeader("OUTP:REL:POL?", token) switch
        {
            "NORM" or "NORMAL" => RelayPolarity.Normal, "REV" or "REVERSE" => RelayPolarity.Reverse,
            _ => throw new FormatException("Unexpected relay polarity.")
        });

    private PanelDisplaySettings ReadPanelDisplay(CancellationToken token)
    {
        bool enabled = ScpiParser.Boolean(channel.Query("DISP?", token));
        var mode = SetupHeader("DISP:MODE?", token) switch
        {
            "NORM" or "NORMAL" => PanelDisplayMode.Normal, "TEXT" => PanelDisplayMode.Text,
            _ => throw new FormatException("Unexpected panel display mode.")
        };
        string message = channel.Query("DISP:TEXT?", token);
        if (message.Length >= 2 && message[0] == '"' && message[^1] == '"')
            message = message[1..^1].Replace("\"\"", "\"");
        if (!ValidPanelText(message)) throw new FormatException("Unexpected front panel text.");
        return new(enabled, mode, message);
    }

    private string SetupHeader(string query, CancellationToken token) => channel.Query(query, token).Trim().Trim('"').ToUpperInvariant();
    private static bool ValidPanelText(string? value) => value is { Length: <= 14 } && value.All(c => c is >= ' ' and <= '~');
    private static string BooleanHeader(bool value) => value ? "1" : "0";

    private static string InhibitHeader(InhibitMode mode) => mode switch
    {
        InhibitMode.Latching => "LATC", InhibitMode.Live => "LIVE", _ => "OFF"
    };

    private static string FaultHeader(FaultIndicatorSource source) => source switch
    {
        FaultIndicatorSource.Questionable => "QUES", FaultIndicatorSource.Operation => "OPER",
        FaultIndicatorSource.StandardEvent => "ESB", FaultIndicatorSource.RequestService => "RQS", _ => "OFF"
    };
}
