using System.Text.Json;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Storage.Preferences;

namespace Agilent66332A.Storage.Tests;

public sealed class HardwareQualificationFileTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "66332A-Tests", Guid.NewGuid().ToString("N"));
    private static readonly InstrumentIdentity Instrument = new("Agilent", "66332A", "US00010001", "A.01.00", "");
    private static readonly SerialAdapterIdentity Adapter = new("USB\\VID_067B&PID_2303\\1001", "{CLASS}\\0001", "oem10.inf", "1.2.3.4");
    private static readonly ConnectionProfile Profile = new("COM21", 4800, false, true, SerialParity.Even, SerialFlowControl.XonXoff, "bench-01", Adapter);
    private static readonly HardwareQualification Limits = new(new(0, 20.475, .005), new(0, 5.1188, .001),
        new(0, 22, .005), new(0, 2147483.647, .001), true, QualifiedFeature.Acquisition | QualifiedFeature.Diagnostics);

    private string FilePath => Path.Combine(directory, HardwareQualificationFile.FileName);
    private static HardwareQualificationEvidence Evidence => new(1, new(2026, 11, 13, 16, 0, 0, TimeSpan.Zero), "Bench transcript 01",
        new(Instrument.Manufacturer, Instrument.Model, Instrument.SerialNumber, Instrument.Firmware), Adapter,
        new(Profile.BaudRate, Profile.Parity, Profile.FlowControl, Profile.DtrEnabled, Profile.RtsEnabled, Profile.CableId!), Limits);

    [Fact]
    public async Task QualificationFollowsTheExactInstrumentAdapterCableAndSerialSettings()
    {
        await JsonFiles.WriteAtomicAsync(FilePath, Evidence);
        var result = await HardwareQualificationFile.LoadAsync(FilePath);
        Assert.Null(result.Warning);
        var qualification = Assert.IsAssignableFrom<IHardwareQualification>(result.Qualification);
        Assert.Equal(Limits, qualification.Match(Instrument, Profile));
        Assert.Equal(Limits, qualification.Match(Instrument, Profile with { PortName = "COM22" }));
        Assert.Null(qualification.Match(Instrument with { Model = "66312A" }, Profile));
        Assert.Null(qualification.Match(Instrument with { SerialNumber = "US00010002" }, Profile));
        Assert.Null(qualification.Match(Instrument with { Firmware = "A.01.01" }, Profile));
        Assert.Null(qualification.Match(Instrument, Profile with { Adapter = null }));
        Assert.Null(qualification.Match(Instrument, Profile with { Adapter = Adapter with { InstanceId = "another-adapter" } }));
        Assert.Null(qualification.Match(Instrument, Profile with { Adapter = Adapter with { DriverVersion = "1.2.3.5" } }));
        Assert.Null(qualification.Match(Instrument, Profile with { CableId = null }));
        Assert.Null(qualification.Match(Instrument, Profile with { CableId = "bench-02" }));
        Assert.Null(qualification.Match(Instrument, Profile with { BaudRate = 9600 }));
        Assert.Null(qualification.Match(Instrument, Profile with { Parity = SerialParity.None }));
        Assert.Null(qualification.Match(Instrument, Profile with { FlowControl = SerialFlowControl.None }));
        Assert.Null(qualification.Match(Instrument, Profile with { DtrEnabled = true }));
    }

    [Fact]
    public async Task ReportedSerialZeroNeedsMatchingChassisEvidenceAndAnExplicitLabel()
    {
        var zero = Instrument with { SerialNumber = "0" };
        var evidence = Evidence with { Device = Evidence.Device with { SerialNumber = "0" } };
        await JsonFiles.WriteAtomicAsync(FilePath, evidence);
        await AssertInvalidAsync();

        const string label = "US00010001";
        await JsonFiles.WriteAtomicAsync(FilePath, evidence with { Device = evidence.Device with { ChassisLabel = label } });
        var (match, warning) = await HardwareQualificationFile.LoadAsync(FilePath);
        Assert.Null(warning);
        var qualification = Assert.IsAssignableFrom<IHardwareQualification>(match);
        var identified = Profile with { ChassisLabel = label };
        Assert.Equal(Limits, qualification.Match(zero, identified));
        Assert.Null(qualification.Match(zero, Profile));
        Assert.Null(qualification.Match(zero, identified with { ChassisLabel = "US00010002" }));
        Assert.Null(qualification.Match(Instrument, identified));
        Assert.DoesNotContain("chassisLabel", JsonSerializer.Serialize(identified, JsonFiles.Options));
    }

    [Fact]
    public async Task MissingIncompleteUnverifiedAndUnsupportedEvidenceCannotEnableControl()
    {
        Assert.Equal((null, null), await HardwareQualificationFile.LoadAsync(FilePath));
        var unsettled = Evidence with { Qualification = Limits with { BreakValidated = false } };
        await JsonFiles.WriteAtomicAsync(FilePath, unsettled);
        await AssertInvalidAsync();

        var unknownFeature = Evidence with { Qualification = Limits with { Features = (QualifiedFeature)(1 << 30) } };
        await JsonFiles.WriteAtomicAsync(FilePath, unknownFeature);
        await AssertInvalidAsync();

        var incomplete = JsonSerializer.SerializeToNode(Evidence, JsonFiles.Options)!;
        Assert.True(incomplete["qualification"]!["voltage"]!.AsObject().Remove("minimum"));
        await File.WriteAllTextAsync(FilePath, incomplete.ToJsonString());
        await AssertInvalidAsync();
    }

    [Fact]
    public async Task EvidenceWithoutFeatureFlagsQualifiesBasicControlsOnly()
    {
        Directory.CreateDirectory(directory);
        var evidence = JsonSerializer.SerializeToNode(Evidence, JsonFiles.Options)!;
        Assert.True(evidence["qualification"]!.AsObject().Remove("features"));
        await File.WriteAllTextAsync(FilePath, evidence.ToJsonString());
        var (qualification, warning) = await HardwareQualificationFile.LoadAsync(FilePath);
        Assert.Null(warning);
        Assert.Equal(QualifiedFeature.None, qualification?.Match(Instrument, Profile)?.Features);
    }

    [Fact]
    public async Task OldPreferencesRetainBaudRateAndManualLineSettings()
    {
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "preferences.json"), """
            {
              "schemaVersion": 1,
              "profile": { "portName": "COM12", "baudRate": 4800, "dtrEnabled": false, "rtsEnabled": true },
              "pollSeconds": 2, "plotMinutes": 5, "recordingDirectory": "recordings"
            }
            """);
        var (preferences, warning) = await new PreferenceRepository(directory).LoadAsync();
        Assert.Null(warning);
        Assert.Equal(new("COM12", 4800, false, true), preferences.Profile);
        Assert.Equal(8, preferences.Profile.DataBits);
    }

    private async Task AssertInvalidAsync()
    {
        var (qualification, warning) = await HardwareQualificationFile.LoadAsync(FilePath);
        Assert.Null(qualification);
        Assert.NotNull(warning);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
    }
}
