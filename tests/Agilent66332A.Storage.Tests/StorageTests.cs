using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Core.Sequences;
using Agilent66332A.Storage.Preferences;
using Agilent66332A.Storage.Recordings;
using Agilent66332A.Storage.Sequences;
using Studio.Tests;

namespace Agilent66332A.Storage.Tests;
public class StorageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "66332A-Tests", Guid.NewGuid().ToString("N"));
    public StorageTests() => Directory.CreateDirectory(root);
    public void Dispose() => Directory.Delete(root, true);
    [Fact] public async Task CsvAndManifestCanBeIndependentlyParsed()
    {
        var context = new RecordingContext("Test, \"quoted\"", "Simulator", new(), TestModels.State, 1, SequenceDocument.Example);
        await using var s = await new RecordingFactory(() => root).StartAsync(context);
        s.Append(TestModels.Pair(), .1, new("step,\"1\"", 1, 2, "Holding", new(5, .1), true));
        s.Event(new(DateTimeOffset.UtcNow, .1, "test", "text\nwith newline"));
        var off = new ShutdownResult("Complete", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, ShutdownOutcome.VerifiedOff);
        Assert.Equal(RecordingOutcome.Complete, await s.CompleteAsync(ExecutionOutcome.Completed, off, TestModels.State));
        using var parser = new TextFieldParser(Path.Combine(s.DirectoryPath, "measurements.csv")); parser.SetDelimiters(","); parser.HasFieldsEnclosedInQuotes = true;
        var header = parser.ReadFields()!; var row = parser.ReadFields()!;
        Assert.Equal(header.Length, row.Length); Assert.Equal("-0.02", row[Array.IndexOf(header, "measured_current_a")]);
        Assert.Equal("step,\"1\"", row[Array.IndexOf(header, "step_id")]); Assert.Equal("2", row[Array.IndexOf(header, "repeat_index")]);
        using var metadata = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(s.DirectoryPath, "run.json")));
        Assert.Equal("complete", metadata.RootElement.GetProperty("status").GetString());
        Assert.Equal("Simulator", metadata.RootElement.GetProperty("sourceKind").GetString());
        Assert.False(File.Exists(Path.Combine(s.DirectoryPath, "measurements.csv.partial")));
        Assert.Single(RecordingCatalog.Discover(root));
    }
    [Fact] public async Task InvalidSequenceImportsAreRejectedAndValidRoundtripPreservesRows()
    {
        string path = Path.Combine(root, "sequence.json");
        await SequenceRepository.SaveAsync(path, SequenceDocument.Example);
        var d = await SequenceRepository.LoadAsync(path); Assert.Equal(3, d.Steps.Length);
        await File.WriteAllTextAsync(path, "{\"schemaVersion\":1}"); await Assert.ThrowsAsync<JsonException>(() => SequenceRepository.LoadAsync(path));
        await File.WriteAllTextAsync(path, JsonSerializer.Serialize(d, JsonFiles.Options).Replace("\"repeatCount\": 1", "\"repeatCount\": 1.5"));
        await Assert.ThrowsAsync<JsonException>(() => SequenceRepository.LoadAsync(path));
    }
    [Fact] public async Task OversizedImportIsRejectedBeforeDeserialization()
    {
        string path = Path.Combine(root, "oversized.json"); await File.WriteAllTextAsync(path, new string(' ', 5 * 1024 * 1024 + 1));
        await Assert.ThrowsAsync<InvalidDataException>(() => SequenceRepository.LoadAsync(path));
    }
    [Fact] public async Task CorruptPreferencesAreRetainedWithWarning()
    {
        string path = Path.Combine(root, "preferences.json"); await File.WriteAllTextAsync(path, "broken");
        var result = await new PreferenceRepository(root).LoadAsync(); Assert.NotNull(result.Warning); Assert.Equal("broken", await File.ReadAllTextAsync(path));
    }
    [Fact] public async Task AtomicReplacementKeepsLastKnownGoodBackup()
    {
        string path = Path.Combine(root, "value.json"); await JsonFiles.WriteAtomicAsync(path, new { n = 1 }); await JsonFiles.WriteAtomicAsync(path, new { n = 2 });
        Assert.Contains("1", await File.ReadAllTextAsync(path + ".bak")); Assert.Contains("2", await File.ReadAllTextAsync(path)); Assert.Empty(Directory.GetFiles(root, "*.tmp"));
    }
    [Fact] public void AbandonedPartialRecordingIsVisible()
    {
        string path = Path.Combine(root, "abandoned"); Directory.CreateDirectory(path); File.WriteAllText(Path.Combine(path, "measurements.csv.partial"), CsvEncoder.Header);
        var summary = Assert.Single(RecordingCatalog.Discover(root)); Assert.Contains("Incomplete", summary.Outcome); Assert.Equal("Unverified", summary.Shutdown);
    }
    [Fact] public async Task UnavailableDirectoryFailsPreflightWithoutSession()
    {
        string path = Path.Combine(root, "file"); await File.WriteAllTextAsync(path, "occupied");
        await Assert.ThrowsAnyAsync<IOException>(() => new RecordingFactory(() => path).StartAsync(new("test", "Simulator", new(), TestModels.State, 1)));
    }
    [Fact] public void MissingMeasurementsAreBlankAndCsvEscapingIsExact()
    {
        var pair = TestModels.Pair() with { Quality = SampleQuality.Missing, Voltage = TestModels.Pair().Voltage with { Value = null } };
        string row = CsvEncoder.Row("id", 1, "Simulator", pair, 0, null);
        Assert.Contains(",unknown,", CsvEncoder.Row("id", 1, "Simulator", pair with { Snapshot = pair.Snapshot with { Output = OutputState.Unknown } }, 0, null));
        Assert.Equal("\"a,\"\"b\"\"\n\"", CsvEncoder.Escape("a,\"b\"\n")); Assert.DoesNotContain("NaN", row);
    }
    [Fact] public async Task WriterIoFailureIsReportedAndPartialEvidenceIsRetained()
    {
        var streams = new List<SwitchableFileStream>();
        await using var session = await FileRecordingSession.CreateAsync(root, new("Disk failure", "Simulator", new(), TestModels.State, 1), default,
            path => { var stream = new SwitchableFileStream(path); streams.Add(stream); return stream; });
        foreach (var stream in streams) stream.Fail = true;
        session.Event(new(DateTimeOffset.UtcNow, 0, "large", new string('x', 8192)));
        for (int i = 0; i < 100 && session.Failure is null; i++) await Task.Delay(10);
        Assert.NotNull(session.Failure);
        Assert.Equal(RecordingOutcome.Failed, await session.CompleteAsync(ExecutionOutcome.Failed, null, TestModels.State));
        Assert.True(File.Exists(Path.Combine(session.DirectoryPath, "measurements.csv.partial")));
        Assert.Contains("Incomplete", RecordingCatalog.Discover(root).Single().Outcome);
    }
    [Fact] public async Task BlockedWriterHasBoundedQueueAndDetectsStall()
    {
        var streams = new List<SwitchableFileStream>();
        await using var session = await FileRecordingSession.CreateAsync(root, new("Stall", "Simulator", new(), TestModels.State, 1), default,
            path => { var stream = new SwitchableFileStream(path); streams.Add(stream); return stream; });
        foreach (var stream in streams) stream.Block = true;
        session.Event(new(DateTimeOffset.UtcNow, 0, "large", new string('x', 8192)));
        await Task.WhenAny(streams.Select(s => s.Entered.Task)).WaitAsync(TimeSpan.FromSeconds(2));
        await Task.Delay(3100);
        Assert.Contains("stalled", session.Failure!.Message);
        foreach (var stream in streams) stream.Release.TrySetResult();
        Assert.Equal(RecordingOutcome.Failed, await session.CompleteAsync(ExecutionOutcome.Failed, null, TestModels.State));
    }
    [Fact] public async Task QueueOverflowFailsInsteadOfDroppingData()
    {
        var streams = new List<SwitchableFileStream>();
        await using var session = await FileRecordingSession.CreateAsync(root, new("Queue", "Simulator", new(), TestModels.State, 1), default,
            path => { var stream = new SwitchableFileStream(path); streams.Add(stream); return stream; });
        foreach (var stream in streams) stream.Block = true;
        session.Event(new(DateTimeOffset.UtcNow, 0, "large", new string('x', 8192)));
        await Task.WhenAny(streams.Select(s => s.Entered.Task)).WaitAsync(TimeSpan.FromSeconds(2));
        for (int i = 0; i < 256; i++) session.Append(TestModels.Pair(), i, null);
        Assert.Throws<IOException>(() => session.Append(TestModels.Pair(), 257, null));
        foreach (var stream in streams) stream.Release.TrySetResult();
        Assert.Equal(RecordingOutcome.Failed, await session.CompleteAsync(ExecutionOutcome.Failed, null, TestModels.State));
    }
    private sealed class SwitchableFileStream(string path) : FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, true)
    {
        public bool Fail { get; set; }
        public bool Block { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken token = default)
        {
            if (Fail) throw new IOException("Injected disk-full failure.");
            if (Block) { Entered.TrySetResult(); await Release.Task.WaitAsync(token); }
            await base.WriteAsync(buffer, token);
        }
    }
}
