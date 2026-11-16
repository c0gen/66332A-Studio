using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Storage.Recordings;

public static class WaveformExport
{
    internal const string FolderName = "Captures";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public static async Task<string> ExportAsync(string recordingRoot, WaveformCapture capture, CancellationToken token = default)
    {
        string timestamp = capture.StartedUtc.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture);
        string root = Path.Combine(Path.GetFullPath(recordingRoot), FolderName, $"{timestamp}-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string unit = capture.Settings.Signal == MeasurementSignal.Voltage ? "V" : "A";
        var csv = new StringBuilder().Append("sample,time_seconds,value_").AppendLine(unit);
        for (int index = 0; index < capture.Samples.Count; index++)
            csv.AppendLine(CultureInfo.InvariantCulture, $"{index},{capture.SecondsAt(index):R},{capture.Samples[index]:R}");

        string data = Path.Combine(root, "waveform.csv");
        string manifest = Path.Combine(root, "capture.json");
        await File.WriteAllTextAsync(data + ".partial", csv.ToString(), token).ConfigureAwait(false);
        var metadata = new
        {
            schemaVersion = 1, capture.Identity, capture.StartedUtc, capture.CompletedUtc,
            capture.Start, capture.Settings, capture.Statistics, sampleCount = capture.Samples.Count, unit
        };
        await File.WriteAllTextAsync(manifest + ".partial", JsonSerializer.Serialize(metadata, Json), token).ConfigureAwait(false);
        File.Move(data + ".partial", data);
        File.Move(manifest + ".partial", manifest);
        return root;
    }
}
