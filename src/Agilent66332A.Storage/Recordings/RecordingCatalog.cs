using System.Text.Json;

namespace Agilent66332A.Storage.Recordings;

public sealed record RecordingSummary(string Name, string StartedUtc, string Outcome, string Shutdown, string SourceKind, string DirectoryPath);
public static class RecordingCatalog
{
    public static IReadOnlyList<RecordingSummary> Discover(string root)
    {
        if (!Directory.Exists(root)) return [];
        var results = new List<RecordingSummary>();
        foreach (string directory in Directory.EnumerateDirectories(root)
                     .Where(path => !string.Equals(Path.GetFileName(path), WaveformExport.FolderName, StringComparison.OrdinalIgnoreCase))
                     .OrderDescending().Take(200))
        {
            try
            {
                using var json = JsonDocument.Parse(File.ReadAllText(Path.Combine(directory, "run.json")));
                var r = json.RootElement;
                string Get(string name) => r.TryGetProperty(name, out var v) && v.ValueKind != JsonValueKind.Null ? v.ToString() : "Unavailable";
                results.Add(new(Get("name"), Get("startedUtc"), Get("status") == "complete" ? $"{Get("executionOutcome")} / {Get("recordingOutcome")}" : "Incomplete", Get("shutdownOutcome"), Get("sourceKind"), directory));
            }
            catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
            { results.Add(new(Path.GetFileName(directory), "Unavailable", "Incomplete / unreadable manifest", "Unverified", "Unknown", directory)); }
        }
        return results;
    }
}
