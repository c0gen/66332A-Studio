using System.Globalization;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Devices;

public partial class AgilentClient : IInstrumentDiagnostics
{
    // Leave the live indicator sources intact during ordinary status monitoring.
    public Task<StatusRegisterSnapshot> ReadDiagnosticsAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureAdvancedScpi();
        return Guard(() => ReadStatusRegisters(t));
    }, token);

    public Task<StatusRegisterSnapshot> ConfigureStatusAsync(InstrumentStatusSettings settings, CancellationToken token = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ValidateMasks(settings.Operation, 32727);
        ValidateMasks(settings.Questionable, 32767);
        if (settings.StandardEventEnable is < 0 or > 255 || settings.ServiceRequestEnable is < 0 or > 255 ||
            (settings.ServiceRequestEnable & 64) != 0)
            throw new ArgumentException("Use byte masks from 0 through 255. Service request bit 6 is reserved.", nameof(settings));

        return Queue(t =>
        {
            EnsureAdvancedScpi(QualifiedFeature.Diagnostics);
            return Guard(() =>
            {
                var before = ReadStatusRegisters(t).Settings;
                if (before.ClearEnablesAtPowerOn != settings.ClearEnablesAtPowerOn && settings.ClearEnablesAtPowerOn)
                    SendCheckedAdvanced("*PSC 1", t);
                WriteChangedMasks("STAT:OPER:", before.Operation, settings.Operation, t);
                WriteChangedMasks("STAT:QUES:", before.Questionable, settings.Questionable, t);
                WriteChangedRegister("*ESE", before.StandardEventEnable, settings.StandardEventEnable, t);
                WriteChangedRegister("*SRE", before.ServiceRequestEnable, settings.ServiceRequestEnable, t);
                if (before.ClearEnablesAtPowerOn != settings.ClearEnablesAtPowerOn && !settings.ClearEnablesAtPowerOn)
                    SendCheckedAdvanced("*PSC 0", t);
                var after = ReadStatusRegisters(t);
                return after.Settings == settings ? after : throw new IOException("Status masks or power-on policy did not match instrument readback.");
            });
        }, token, configuration: true);
    }

    public Task<StatusRegisterSnapshot> PresetStatusAsync(CancellationToken token = default) => RunStatusCommandAsync("STAT:PRES", token);

    public Task<StatusRegisterSnapshot> ClearStatusAsync(CancellationToken token = default) => RunStatusCommandAsync("*CLS", token);

    private Task<StatusRegisterSnapshot> RunStatusCommandAsync(string command, CancellationToken token) => Queue(t =>
    {
        EnsureAdvancedScpi(QualifiedFeature.Diagnostics);
        return Guard(() =>
        {
            SendCheckedAdvanced(command, t);
            return ReadStatusRegisters(t);
        });
    }, token, configuration: true);

    public Task<StatusEventReading> ReadAndAcknowledgeStatusEventAsync(StatusEventRegister register, CancellationToken token = default)
    {
        string query = register switch
        {
            StatusEventRegister.Operation => "STAT:OPER:EVEN?", StatusEventRegister.Questionable => "STAT:QUES:EVEN?",
            StatusEventRegister.Standard => "*ESR?", _ => throw new ArgumentOutOfRangeException(nameof(register))
        };
        return Queue(t =>
        {
            EnsureAdvancedScpi(QualifiedFeature.Diagnostics);
            return Guard(() =>
            {
                int value = register == StatusEventRegister.Standard ? ReadAndRecordStandardEvent(t) : ReadRegister(query, t);
                if (register != StatusEventRegister.Standard) Trace?.Invoke($"Acknowledged {register} events 0x{value:X4}.");
                return new StatusEventReading(register, value, clock.UtcNow);
            });
        }, token, configuration: true);
    }

    public Task<DeviceErrorBatch> DrainDeviceErrorsAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureAdvancedScpi();
        return Guard(() => ReadErrorBatch(t));
    }, token);

    private DeviceErrorBatch ReadErrorBatch(CancellationToken token)
    {
        const int maximum = 10;
        var errors = new List<DeviceErrorReading>();
        for (int i = 0; i < maximum; i++)
        {
            var error = ReadAndRecordDeviceError(token);
            if (error.Code == 0) return new(errors.AsReadOnly(), false);
            errors.Add(new(error.Code, error.Message, clock.UtcNow));
        }
        return new(errors.AsReadOnly(), true);
    }

    private (int Code, string Message) ReadAndRecordDeviceError(CancellationToken token)
    {
        var error = ScpiParser.Error(channel.Query("SYST:ERR?", token));
        if (error.Code != 0) Trace?.Invoke($"Instrument error {error.Code}: {error.Message}");
        return error;
    }

    private int ReadAndRecordStandardEvent(CancellationToken token)
    {
        int value = ReadRegister("*ESR?", token);
        if (value > 255) throw new FormatException("Unexpected standard event register value.");
        if (value != 0) Trace?.Invoke($"Acknowledged standard events 0x{value:X2}.");
        return value;
    }

    private StatusRegisterSnapshot ReadStatusRegisters(CancellationToken token)
    {
        int operation = ReadRegister("STAT:OPER:COND?", token);
        int questionable = ReadRegister("STAT:QUES:COND?", token);
        int statusByte = ReadRegister("*STB?", token);
        var settings = new InstrumentStatusSettings(ReadMasks("STAT:OPER:", token), ReadMasks("STAT:QUES:", token),
            ReadRegister("*ESE?", token), ReadRegister("*SRE?", token), ScpiParser.Boolean(channel.Query("*PSC?", token)));
        return new(operation, questionable, statusByte, settings, clock.UtcNow);
    }

    private StatusGroupMasks ReadMasks(string prefix, CancellationToken token) =>
        new(ReadRegister(prefix + "ENAB?", token), ReadRegister(prefix + "PTR?", token), ReadRegister(prefix + "NTR?", token));

    private static void ValidateMasks(StatusGroupMasks masks, int maximum)
    {
        ArgumentNullException.ThrowIfNull(masks);
        if (masks.Enable < 0 || masks.Enable > maximum || masks.PositiveTransitions is < 0 or > 32727 || masks.NegativeTransitions is < 0 or > 32727)
            throw new ArgumentException("Status mask is outside the instrument register range.", nameof(masks));
    }

    private void WriteChangedMasks(string prefix, StatusGroupMasks before, StatusGroupMasks after, CancellationToken token)
    {
        WriteChangedRegister(prefix + "PTR", before.PositiveTransitions, after.PositiveTransitions, token);
        WriteChangedRegister(prefix + "NTR", before.NegativeTransitions, after.NegativeTransitions, token);
        WriteChangedRegister(prefix + "ENAB", before.Enable, after.Enable, token);
    }

    private void WriteChangedRegister(string header, int before, int after, CancellationToken token)
    {
        if (before != after) SendCheckedAdvanced(header + " " + after.ToString(CultureInfo.InvariantCulture), token);
    }

    private int ReadRegister(string query, CancellationToken token) => ScpiParser.Register(channel.Query(query, token));
}
