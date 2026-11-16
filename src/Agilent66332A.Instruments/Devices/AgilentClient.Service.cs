using System.Globalization;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Devices;

public partial class AgilentClient : IInstrumentService
{
    public ProgrammingLanguage Language { get; private set; } = ProgrammingLanguage.Unknown;

    public Task<InstrumentServiceSnapshot> ReadServiceAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureReady();
        if (CalibrationInProgress) throw new InvalidOperationException("Finish the calibration first.");
        return Guard(() => ReadServiceState(t));
    }, token);

    public Task<SelfTestResult> RunSelfTestAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureAdvancedScpi(QualifiedFeature.Service);
        return Guard(() =>
        {
            SetOffVerified(t);
            int code = ScpiParser.Register(channel.Query("*TST?", t, 20));
            var errors = ReadErrorBatch(t);
            SetOffVerified(t);
            return new SelfTestResult(code, errors, clock.UtcNow);
        });
    }, token, configuration: true);

    public Task<DeviceSnapshot> ResetInstrumentAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureAdvancedScpi(QualifiedFeature.Service);
        return Guard(() =>
        {
            SetOffVerified(t);
            SendCheckedAdvanced("*RST", t);
            SetOffVerified(t);
            if (IsQualified(QualifiedFeature.Acquisition)) RefreshMeasurementDuration(t);
            return ReadState(t);
        });
    }, token, configuration: true);

    public Task SaveDeviceStateAsync(int slot, CancellationToken token = default)
    {
        string address = MemorySlot(slot);
        return Queue(t =>
        {
            EnsureAdvancedScpi(QualifiedFeature.Service);
            return Guard(() =>
            {
                SendCheckedAdvanced("SYST:REM", t);
                SetOffVerified(t);
                SendCheckedAdvanced("*SAV " + address, t);
                return true;
            });
        }, token, configuration: true);
    }

    public Task<DeviceSnapshot> RecallDeviceStateAsync(int slot, bool fixtureIsolated, CancellationToken token = default)
    {
        string address = MemorySlot(slot);
        if (!fixtureIsolated) throw new InvalidOperationException("Isolate the load before recalling instrument memory.");
        return Queue(t =>
        {
            EnsureAdvancedScpi(QualifiedFeature.Service);
            return Guard(() =>
            {
                SendCheckedAdvanced("SYST:REM", t);
                SetOffVerified(t);
                channel.Write("*RCL " + address, t);
                // Recall can restore a saved ON state. The caller must have physically isolated the fixture.
                SetOffVerified(t);
                CheckErrors(t);
                AbortTriggers(t, disableContinuous: true);
                CheckErrors(t);
                if (IsQualified(QualifiedFeature.Acquisition)) RefreshMeasurementDuration(t);
                return ReadState(t);
            });
        }, token, configuration: true);
    }

    public Task<InstrumentServiceSnapshot> ConfigurePowerOnAsync(PowerOnMode mode, DeviceSnapshot? reviewedState = null,
        CancellationToken token = default)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentOutOfRangeException(nameof(mode));
        if (mode == PowerOnMode.RecallSlotZero && reviewedState is null)
            throw new ArgumentException("Review the settings before writing startup slot zero.", nameof(reviewedState));
        return Queue(t =>
        {
            EnsureAdvancedScpi(QualifiedFeature.Service);
            if (mode == PowerOnMode.RecallSlotZero)
            {
                var live = Guard(() => ReadState(t));
                if (!MatchesReviewedState(live, reviewedState!)) throw new InvalidOperationException("Settings changed after your review. Refresh and review the startup settings again.");
            }
            return Guard(() =>
            {
                SendCheckedAdvanced("SYST:REM", t);
                SetOffVerified(t);
                if (mode == PowerOnMode.RecallSlotZero)
                    SendCheckedAdvanced("*SAV 0", t); // Use only this freshly written, verified-off state.
                if (ReadPowerOnMode(t) != mode)
                    SendCheckedAdvanced("OUTP:PON:STAT " + (mode == PowerOnMode.Reset ? "RST" : "RCL0"), t);
                var result = ReadServiceState(t);
                if (result.PowerOn != mode || ReadState(t).Output != OutputState.Off)
                    throw new IOException("Power-on policy or output-off did not match instrument readback.");
                return result;
            });
        }, token, configuration: true);
    }

    public Task<InstrumentServiceSnapshot> SetProgrammingLanguageAsync(ProgrammingLanguage language, bool fixtureIsolated,
        CancellationToken token = default)
    {
        if (language is not (ProgrammingLanguage.Scpi or ProgrammingLanguage.Compatibility)) throw new ArgumentOutOfRangeException(nameof(language));
        if (!fixtureIsolated) throw new InvalidOperationException("Isolate the load before switching the programming language.");
        return Queue(t =>
        {
            EnsureReady();
            if (CalibrationInProgress || CaptureOwnsBuffer || !IsQualified(QualifiedFeature.Compatibility))
                throw new InvalidOperationException("Language switching requires a validated instrument and connection. Use the front panel Address menu to select SCPI if needed.");
            if (Language == ProgrammingLanguage.Unknown) throw new InvalidOperationException("Identify the active device language before changing it.");
            return Guard(() => Language == language ? ReadServiceState(t) : SwitchLanguage(language, t));
        }, token);
    }

    public Task<CompatibilityReading> ReadCompatibilityAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureCompatibility();
        return Guard(() => new CompatibilityReading(snapshot!.Identity, Number("VOUT?", t), Number("IOUT?", t),
            ReadRegister("STS?", t), clock.UtcNow));
    }, token);

    public Task<CompatibilityEvents> AcknowledgeCompatibilityEventsAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureCompatibility();
        if (!IsQualified(QualifiedFeature.Compatibility)) throw new InvalidOperationException("Legacy event acknowledgment requires validation.");
        return Guard(() => new CompatibilityEvents(ReadRegister("ASTS?", t), ReadRegister("FAULT?", t),
            ReadLegacyError(t), clock.UtcNow));
    }, token);

    private InstrumentServiceSnapshot SwitchLanguage(ProgrammingLanguage target, CancellationToken token)
    {
        var original = snapshot!.Identity;
        if (Language == ProgrammingLanguage.Scpi)
        {
            compatibilityRelayEvidence = (snapshot.Generation, ReadOptionCodes(token).Contains("760", StringComparer.Ordinal));
            SetOffVerified(token);
        }
        else channel.Write("OUT 0", token);

        Language = ProgrammingLanguage.Unknown;
        snapshot = snapshot with { Output = OutputState.Unknown, ObservedUtc = clock.UtcNow };
        if (target == ProgrammingLanguage.Compatibility)
        {
            channel.Write("SYST:LANG COMP", token);
            // Compatibility powers up with OUT enabled and exposes no verified enable readback.
            channel.Write("OUT 0", token);
        }
        else
        {
            channel.Write("SYST:LANG SCPI", token);
            // SCPI can recall slot zero at startup. This is the same explicitly authorized session.
            channel.Write("OUTP 0", token);
        }

        Language = ReadProgrammingLanguage(token);
        if (Language != target) throw new IOException("The requested programming language could not be verified.");
        if (Language == ProgrammingLanguage.Compatibility)
        {
            // The reboot may have flushed the first request. Send it again after the new parser responds.
            channel.Write("OUT 0", token);
            int error = ReadLegacyError(token);
            if (error != 0) throw new IOException($"Compatibility command error {error}.");
        }
        else
        {
            InstrumentIdentity identity;
            try { identity = ScpiParser.Identity(channel.Query("*IDN?", token, 5)); }
            catch (NotSupportedException) { identified = false; throw; }
            if (!string.Equals(identity.Model, original.Model, StringComparison.OrdinalIgnoreCase) ||
                (!string.Equals(original.SerialNumber, "0", StringComparison.Ordinal) &&
                 !string.Equals(identity.SerialNumber, original.SerialNumber, StringComparison.OrdinalIgnoreCase)) ||
                !string.Equals(identity.Firmware, original.Firmware, StringComparison.OrdinalIgnoreCase))
            {
                identified = false;
                throw new IOException("Instrument identity changed during the language transition.");
            }
            snapshot = snapshot with { Identity = identity };
            SetOffVerified(token);
            CheckErrors(token);
            AbortTriggers(token, disableContinuous: true);
            CheckErrors(token);
            if (IsQualified(QualifiedFeature.Acquisition)) RefreshMeasurementDuration(token);
            ReadState(token);
        }
        return ReadServiceState(token);
    }

    private ProgrammingLanguage ReadProgrammingLanguage(CancellationToken token) => SetupHeader("SYST:LANG?", token) switch
    {
        "SCPI" => ProgrammingLanguage.Scpi, "COMP" or "COMPATIBILITY" => ProgrammingLanguage.Compatibility,
        _ => throw new FormatException("The instrument returned an unrecognized programming language.")
    };

    private InstrumentIdentity ReadCompatibilityIdentity(CancellationToken token)
    {
        string raw = channel.Query("ID?", token, 5).Trim();
        string compact = raw.Replace(" ", "", StringComparison.Ordinal).ToUpperInvariant();
        // Compatibility on a related model may report an emulated identifier. Accept only exact 66332A.
        var aliases = new (string Prefix, string Manufacturer)[]
        {
            ("HEWLETT-PACKARD", "HEWLETT-PACKARD"), ("HP", "HP"), ("AGILENTTECHNOLOGIES", "AGILENT TECHNOLOGIES"),
            ("AGILENT", "AGILENT"), ("KEYSIGHTTECHNOLOGIES", "KEYSIGHT TECHNOLOGIES"), ("KEYSIGHT", "KEYSIGHT")
        };
        var match = aliases.FirstOrDefault(alias => compact == alias.Prefix + "66332A");
        if (match == default) throw new NotSupportedException("Compatibility mode did not identify an exact 66332A. Select SCPI from the front panel and reconnect.");
        string firmware = channel.Query("ROM?", token).Trim();
        if (string.IsNullOrWhiteSpace(firmware)) throw new FormatException("Compatibility mode returned no firmware revision.");
        return new(match.Manufacturer, "66332A", "0", firmware, raw);
    }

    private InstrumentServiceSnapshot ReadServiceState(CancellationToken token) => Language switch
    {
        ProgrammingLanguage.Scpi => new(Language, ReadPowerOnMode(token), ScpiParser.Boolean(channel.Query("CAL:STAT?", token)), clock.UtcNow),
        ProgrammingLanguage.Compatibility => new(Language, null, null, clock.UtcNow, CompatibilityRelayPresent),
        _ => throw new InvalidOperationException("The instrument programming language is unknown.")
    };

    private PowerOnMode ReadPowerOnMode(CancellationToken token) => SetupHeader("OUTP:PON:STAT?", token) switch
    {
        "RST" or "*RST" => PowerOnMode.Reset, "RCL0" => PowerOnMode.RecallSlotZero,
        _ => throw new FormatException("Unexpected output power-on policy.")
    };

    private void EnsureCompatibility()
    {
        EnsureReady();
        if (Language != ProgrammingLanguage.Compatibility) throw new InvalidOperationException("Select Compatibility mode first.");
    }

    private int ReadLegacyError(CancellationToken token)
    {
        string value = channel.Query("ERR?", token).Trim();
        int code = ScpiParser.Register(value.StartsWith("ERR ", StringComparison.OrdinalIgnoreCase) ? value[4..] : value);
        if (code != 0) Trace?.Invoke($"Compatibility error {code}.");
        return code;
    }

    private static string MemorySlot(int slot) => slot is >= 0 and <= 3 ? slot.ToString(CultureInfo.InvariantCulture)
        : throw new ArgumentOutOfRangeException(nameof(slot), "Instrument memory slots range from 0 to 3.");

    private static bool MatchesReviewedState(DeviceSnapshot live, DeviceSnapshot reviewed) => live.Generation == reviewed.Generation &&
        live.Settings.VoltageV == reviewed.Settings.VoltageV && live.Settings.CurrentLimitA == reviewed.Settings.CurrentLimitA &&
        live.Protections == reviewed.Protections;
}
