using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Storage.Preferences;

namespace Agilent66332A.Storage.Recordings;

public sealed class FileRecordingSession : IRecordingSession
{
    private sealed record Entry(bool IsEvent, string Text);
    private readonly Channel<Entry> queue = Channel.CreateBounded<Entry>(new BoundedChannelOptions(256) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
    private readonly RecordingContext context;
    private readonly string id;
    private readonly DateTimeOffset started = DateTimeOffset.UtcNow;
    private readonly StreamWriter csv;
    private readonly StreamWriter events;
    private readonly CancellationTokenSource stop = new();
    private Task writer = Task.CompletedTask;
    private Exception? failure;
    private long pendingSince;
    private long sampleIndex;
    private bool completed;
    public string DirectoryPath { get; }
    public Exception? Failure
    {
        get
        {
            long since = Interlocked.Read(ref pendingSince);
            if (since != 0 && Stopwatch.GetElapsedTime(since).TotalSeconds > 3)
                Interlocked.CompareExchange(ref failure, new IOException("Recording writer stalled for more than 3 seconds."), null);
            return Volatile.Read(ref failure);
        }
    }
    private FileRecordingSession(string directory, string id, RecordingContext context, Func<string, Stream>? openStream)
    {
        DirectoryPath = directory; this.id = id; this.context = context;
        csv = NewWriter(Path.Combine(directory, "measurements.csv.partial"), openStream);
        try { events = NewWriter(Path.Combine(directory, "events.jsonl.partial"), openStream); }
        catch { csv.Dispose(); throw; }
    }
    private static StreamWriter NewWriter(string path, Func<string, Stream>? openStream) => new(openStream?.Invoke(path) ?? new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, true), new UTF8Encoding(false));
    public static async Task<FileRecordingSession> CreateAsync(string root, RecordingContext context, CancellationToken token, Func<string, Stream>? openStream = null)
    {
        string id = Guid.NewGuid().ToString("N");
        string directory = Path.Combine(root, DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHHmmssZ") + "_" + id[..8]);
        Directory.CreateDirectory(directory);
        var session = new FileRecordingSession(directory, id, context, openStream);
        try
        {
            await session.csv.WriteLineAsync(CsvEncoder.Header.AsMemory(), token).ConfigureAwait(false);
            await session.csv.FlushAsync(token).ConfigureAwait(false);
            await session.events.FlushAsync(token).ConfigureAwait(false);
            if (context.Sequence is not null) await JsonFiles.WriteAtomicAsync(Path.Combine(directory, "sequence.json"), context.Sequence, token).ConfigureAwait(false);
            await session.WriteManifestAsync("inProgress", null, null, null, null, token).ConfigureAwait(false);
            session.writer = Task.Run(session.WriteLoopAsync);
            return session;
        }
        catch { session.csv.Dispose(); session.events.Dispose(); throw; }
    }
    public void Append(MeasurementPair sample, double elapsedSeconds, RunPosition? position) =>
        Enqueue(new(false, CsvEncoder.Row(id, Interlocked.Increment(ref sampleIndex), context.SourceKind, sample, elapsedSeconds, position)));
    public void Event(StudioEvent entry) => Enqueue(new(true, JsonSerializer.Serialize(new
    {
        id = Guid.NewGuid(), entry.Utc, entry.ElapsedSeconds, entry.Type, entry.Message, entry.StepId
    }, new JsonSerializerOptions(JsonFiles.Options) { WriteIndented = false })));
    private void Enqueue(Entry entry)
    {
        if (Failure is { } error) throw new IOException("Recording failed.", error);
        if (completed || !queue.Writer.TryWrite(entry))
        {
            Interlocked.CompareExchange(ref failure, new IOException("Recording queue is full or closed; no record was silently dropped."), null);
            throw new IOException("Recording queue is full or closed.", failure);
        }
    }
    private async Task WriteLoopAsync()
    {
        try
        {
            Task<bool>? ready = null;
            while (true)
            {
                ready ??= queue.Reader.WaitToReadAsync(stop.Token).AsTask();
                await Task.WhenAny(ready, Task.Delay(1000, stop.Token)).ConfigureAwait(false);
                if (ready.IsCompleted) ready = null;
                while (queue.Reader.TryRead(out var entry))
                {
                    Interlocked.Exchange(ref pendingSince, Stopwatch.GetTimestamp());
                    await (entry.IsEvent ? events : csv).WriteLineAsync(entry.Text.AsMemory(), stop.Token).ConfigureAwait(false);
                    Interlocked.Exchange(ref pendingSince, 0);
                }
                Interlocked.Exchange(ref pendingSince, Stopwatch.GetTimestamp());
                await csv.FlushAsync(stop.Token).ConfigureAwait(false);
                await events.FlushAsync(stop.Token).ConfigureAwait(false);
                Interlocked.Exchange(ref pendingSince, 0);
                if (queue.Reader.Completion.IsCompleted) break;
            }
        }
        catch (Exception e) { Interlocked.CompareExchange(ref failure, e, null); queue.Writer.TryComplete(e); }
        finally
        {
            Interlocked.Exchange(ref pendingSince, Stopwatch.GetTimestamp());
            try { await csv.DisposeAsync().ConfigureAwait(false); await events.DisposeAsync().ConfigureAwait(false); }
            catch (Exception e) { Interlocked.CompareExchange(ref failure, e, null); }
            Interlocked.Exchange(ref pendingSince, 0);
        }
    }
    public async Task<RecordingOutcome> CompleteAsync(ExecutionOutcome execution, ShutdownResult? shutdown, DeviceSnapshot? final, CancellationToken token = default)
    {
        if (completed) return Failure is null ? RecordingOutcome.Complete : RecordingOutcome.Failed;
        completed = true;
        queue.Writer.TryComplete();
        try
        {
            await writer.WaitAsync(TimeSpan.FromSeconds(4), token).ConfigureAwait(false);
            if (Failure is not null) throw new IOException("Writer failed.", Failure);
            File.Move(Path.Combine(DirectoryPath, "measurements.csv.partial"), Path.Combine(DirectoryPath, "measurements.csv"));
            File.Move(Path.Combine(DirectoryPath, "events.jsonl.partial"), Path.Combine(DirectoryPath, "events.jsonl"));
            await WriteManifestAsync("complete", execution, shutdown, final, RecordingOutcome.Complete, token).ConfigureAwait(false);
            return RecordingOutcome.Complete;
        }
        catch (Exception e)
        {
            Interlocked.CompareExchange(ref failure, e, null);
            stop.Cancel();
            // An inProgress manifest and partial files are discoverable even if the disk cannot be updated.
            return RecordingOutcome.Failed;
        }
    }
    private Task WriteManifestAsync(string status, ExecutionOutcome? execution, ShutdownResult? shutdown, DeviceSnapshot? final, RecordingOutcome? outcome, CancellationToken token) =>
        JsonFiles.WriteAtomicAsync(Path.Combine(DirectoryPath, "run.json"), new
        {
            schemaVersion = 1, appVersion = "0.1.0-preview", recordingId = id, name = context.Name, status,
            sourceKind = context.SourceKind, context.Profile, context.InitialSnapshot, finalSnapshot = final,
            startedUtc = started, endedUtc = status == "complete" ? DateTimeOffset.UtcNow : (DateTimeOffset?)null,
            pollingSeconds = context.PollSeconds, clockSemantics = "UTC correlation; monotonic elapsed seconds for duration; one-based row/repeat indexes",
            units = new { voltage = "V", current = "A", estimatedPower = "W", duration = "s" },
            sequenceSha256 = context.Sequence is null ? null : Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(context.Sequence, JsonFiles.Options))),
            executionOutcome = execution, shutdownOutcome = shutdown?.Outcome, shutdown, recordingOutcome = outcome,
            sampleCount = sampleIndex, dataFiles = new[] { "measurements.csv", "events.jsonl" }
        }, token);
    public async ValueTask DisposeAsync()
    {
        if (!completed) await CompleteAsync(ExecutionOutcome.Failed, null, null).ConfigureAwait(false);
        if (writer.IsCompleted) stop.Dispose();
    }
}
