using System.Text.Json;
using System.Text.Json.Serialization;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Storage.Preferences;

public static class JsonFiles
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter() }
    };
    public static Task WriteAtomicAsync<T>(string path, T value, CancellationToken token = default) =>
        Task.Run(() => WriteAtomicCoreAsync(path, value, token), token);
    private static async Task WriteAtomicCoreAsync<T>(string path, T value, CancellationToken token)
    {
        string full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        string temp = full + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (var file = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, true))
            {
                await JsonSerializer.SerializeAsync(file, value, Options, token).ConfigureAwait(false);
                await file.FlushAsync(token).ConfigureAwait(false);
            }
            if (File.Exists(full)) File.Replace(temp, full, full + ".bak"); else File.Move(temp, full);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    public static Task<T> ReadAsync<T>(string path, long maximumBytes = 5 * 1024 * 1024, CancellationToken token = default,
        JsonSerializerOptions? options = null) =>
        Task.Run(() => ReadCoreAsync<T>(path, maximumBytes, token, options ?? Options), token);
    private static async Task<T> ReadCoreAsync<T>(string path, long maximumBytes, CancellationToken token, JsonSerializerOptions options)
    {
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
        if (file.Length > maximumBytes) throw new InvalidDataException($"File exceeds {maximumBytes} bytes.");
        return await JsonSerializer.DeserializeAsync<T>(file, options, token).ConfigureAwait(false)
            ?? throw new InvalidDataException("JSON document is empty.");
    }
}

public sealed record StudioPreferences(int SchemaVersion, ConnectionProfile Profile, double PollSeconds, int PlotMinutes, string RecordingDirectory)
{
    public static StudioPreferences Default => new(1, new(), 1, 5, UserPaths.Recordings);
}
public sealed record SettingsPreset(int SchemaVersion, string TargetModel, string Name, RequestedSettings Settings, ProtectionSettings Protections);
public static class UserPaths
{
    public static string Preferences => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "66332A Studio");
    public static string Recordings => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "66332A Studio", "Recordings");
}
public sealed class PreferenceRepository(string directory)
{
    public string HardwareQualificationPath => Path.Combine(directory, HardwareQualificationFile.FileName);

    public async Task<(StudioPreferences Value, string? Warning)> LoadAsync()
    {
        string path = Path.Combine(directory, "preferences.json");
        if (!File.Exists(path)) return (StudioPreferences.Default, null);
        try
        {
            var p = await JsonFiles.ReadAsync<StudioPreferences>(path).ConfigureAwait(false);
            if (p.SchemaVersion != 1 || p.Profile is null || p.PollSeconds is not (1 or 2 or 5 or 10) || p.PlotMinutes is not (1 or 5 or 15 or 60) || string.IsNullOrWhiteSpace(p.RecordingDirectory))
                throw new InvalidDataException("Unsupported or invalid preferences.");
            p.Profile.Validate();
            return (p, null);
        }
        catch (Exception e) when (e is IOException or InvalidDataException or JsonException or ArgumentException or UnauthorizedAccessException)
        { return (StudioPreferences.Default, $"Preferences could not be read: {e.Message} The original file was retained."); }
    }
    public Task SaveAsync(StudioPreferences p) => JsonFiles.WriteAtomicAsync(Path.Combine(directory, "preferences.json"), p);
    public Task SavePresetAsync(SettingsPreset preset)
    {
        if (string.IsNullOrWhiteSpace(preset.Name)) throw new ArgumentException("Enter a preset name.");
        string name = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(preset.Name)))[..16];
        return JsonFiles.WriteAtomicAsync(Path.Combine(directory, "Presets", name + ".json"), preset);
    }
    public async Task<IReadOnlyList<SettingsPreset>> PresetsAsync()
    {
        string path = Path.Combine(directory, "Presets");
        if (!Directory.Exists(path)) return [];
        var result = new List<SettingsPreset>();
        foreach (string file in Directory.EnumerateFiles(path, "*.json").Take(1000))
        {
            var p = await JsonFiles.ReadAsync<SettingsPreset>(file).ConfigureAwait(false);
            if (p.SchemaVersion != 1 || p.TargetModel != "66332A" || p.Settings is null || p.Protections is null) throw new InvalidDataException("Unsupported preset: " + file);
            result.Add(p);
        }
        return result.OrderBy(p => p.Name).ToArray();
    }
}
