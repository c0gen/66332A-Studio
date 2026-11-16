using System.Text.Json;
using System.Text.Json.Serialization;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Storage.Preferences;

public sealed record QualifiedInstrument(string Manufacturer, string Model, string SerialNumber, string Firmware,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? ChassisLabel = null);

public sealed record QualifiedSerialLink(int BaudRate, SerialParity Parity, SerialFlowControl FlowControl,
    bool DtrEnabled, bool RtsEnabled, string CableId);

public sealed record HardwareQualificationEvidence(int SchemaVersion, DateTimeOffset VerifiedUtc, string BenchReport,
    QualifiedInstrument Device, SerialAdapterIdentity Adapter, QualifiedSerialLink Serial, HardwareQualification Qualification);

public static class HardwareQualificationFile
{
    public const string FileName = "hardware-qualification.json";
    private const int MaximumBytes = 16 * 1024;
    private static readonly JsonSerializerOptions StrictOptions = new(JsonFiles.Options)
    {
        RespectRequiredConstructorParameters = true,
        AllowDuplicateProperties = false
    };
    private static readonly QualifiedFeature KnownFeatures = Enum.GetValues<QualifiedFeature>()
        .Aggregate(QualifiedFeature.None, (mask, feature) => mask | feature);

    public static async Task<(IHardwareQualification? Qualification, string? Warning)> LoadAsync(
        string path, CancellationToken token = default)
    {
        try
        {
            var evidence = await JsonFiles.ReadAsync<HardwareQualificationEvidence>(path, MaximumBytes, token, StrictOptions)
                .ConfigureAwait(false);
            Validate(evidence);
            return (new Matcher(evidence), null);
        }
        catch (Exception error) when (error is FileNotFoundException or DirectoryNotFoundException)
        {
            return (null, null);
        }
        catch (Exception error) when (error is IOException or InvalidDataException or JsonException or UnauthorizedAccessException or ArgumentException)
        {
            return (null, $"Bench qualification could not be loaded. {error.Message}");
        }
    }

    private static void Validate(HardwareQualificationEvidence evidence)
    {
        if (evidence.SchemaVersion != 1 || evidence.VerifiedUtc == default || string.IsNullOrWhiteSpace(evidence.BenchReport))
            throw new InvalidDataException("Supply a version 1 bench report reference and verification date.");

        var device = evidence.Device;
        if (device is null || !StringComparer.OrdinalIgnoreCase.Equals(device.Model, "66332A") ||
            string.IsNullOrWhiteSpace(device.Manufacturer) || string.IsNullOrWhiteSpace(device.SerialNumber) ||
            string.IsNullOrWhiteSpace(device.Firmware))
            throw new InvalidDataException("Bench evidence must identify a 66332A by manufacturer, serial number and firmware.");
        if (device.SerialNumber.Trim() == "0" && !ValidLabel(device.ChassisLabel))
            throw new InvalidDataException("Bench evidence for an instrument that reports serial 0 must include a chassis serial or asset label.");

        if (evidence.Adapter?.IsComplete != true || evidence.Serial is not { } serial ||
            !ValidLabel(serial.CableId))
            throw new InvalidDataException("Bench evidence must identify the Windows adapter, driver and labeled serial cable.");
        new ConnectionProfile(BaudRate: serial.BaudRate, Parity: serial.Parity, FlowControl: serial.FlowControl).Validate();

        var qualification = evidence.Qualification;
        if (qualification is null || !qualification.BreakValidated || (qualification.Features & ~KnownFeatures) != 0 ||
            !ValidRange(qualification.Voltage) || !ValidRange(qualification.Current) ||
            !ValidRange(qualification.Ovp) || !ValidRange(qualification.OcpDelay))
            throw new InvalidDataException("Bench evidence must include verified Break recovery, supported features and the measured programming ranges and resolutions.");
    }

    private static bool ValidLabel(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 80;

    private static bool ValidRange(NumericRange? range) => range is not null &&
        double.IsFinite(range.Minimum) && range.Minimum >= 0 && double.IsFinite(range.Maximum) &&
        double.IsFinite(range.Resolution) && range.Resolution > 0 && range.Resolution <= range.Maximum - range.Minimum;

    private sealed class Matcher(HardwareQualificationEvidence evidence) : IHardwareQualification
    {
        public HardwareQualification? Match(InstrumentIdentity identity, ConnectionProfile connection)
        {
            var device = evidence.Device;
            var comparer = StringComparer.OrdinalIgnoreCase;
            if (!comparer.Equals(device.Manufacturer.Trim(), identity.Manufacturer) ||
                !comparer.Equals(device.Model, identity.Model) ||
                !comparer.Equals(device.SerialNumber.Trim(), identity.SerialNumber) ||
                !comparer.Equals(device.Firmware.Trim(), identity.Firmware) ||
                !evidence.Adapter.Matches(connection.Adapter))
                return null;
            if (identity.SerialNumber == "0" && !StringComparer.Ordinal.Equals(device.ChassisLabel?.Trim(), connection.ChassisLabel?.Trim()))
                return null;

            var serial = evidence.Serial;
            if (serial.BaudRate != connection.BaudRate || serial.Parity != connection.Parity || serial.FlowControl != connection.FlowControl ||
                (!connection.DtrControlledByHandshake && serial.DtrEnabled != connection.DtrEnabled) ||
                (!connection.RtsControlledByHandshake && serial.RtsEnabled != connection.RtsEnabled) ||
                !StringComparer.Ordinal.Equals(serial.CableId.Trim(), connection.CableId?.Trim()))
                return null;

            return evidence.Qualification;
        }
    }
}
