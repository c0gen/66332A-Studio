using System.Text;
using Agilent66332A.Core.Models;
using Agilent66332A.Instruments.Serial;
using Agilent66332A.Instruments.Simulation;
namespace Agilent66332A.Instruments.Tests;
public sealed class ScriptedTransport : IByteTransport
{
    public bool IsOpen { get; private set; }
    public Queue<(string Command, byte[][] Chunks)> Script { get; } = new();
    private readonly Queue<byte[]> chunks = new();
    public List<string> Writes { get; } = [];
    public void Open(ConnectionProfile profile) => IsOpen = true;
    public void Write(ReadOnlySpan<byte> bytes)
    {
        string actual = Encoding.ASCII.GetString(bytes); Writes.Add(actual);
        var expected = Script.Dequeue(); Assert.Equal(expected.Command + "\r\n", actual);
        foreach (var chunk in expected.Chunks) chunks.Enqueue(chunk);
    }
    public int Read(Span<byte> buffer)
    {
        if (!chunks.TryDequeue(out var chunk)) { Thread.Sleep(1); throw new TimeoutException(); }
        chunk.CopyTo(buffer); return chunk.Length;
    }
    public void SendBreak(CancellationToken token) => chunks.Clear();
    public void DiscardInput() => chunks.Clear();
    public void Close() => IsOpen = false;
    public void Dispose() => Close();
}
public sealed class InterceptTransport : IByteTransport
{
    public SimulatedTransport Inner { get; } = new();
    public Action<string>? BeforeWrite { get; set; }
    public Func<string, string?>? ReplaceReply { get; set; }
    public bool BlockVoltageMeasurement { get; set; }
    public ManualResetEventSlim MeasurementEntered { get; } = new();
    private byte[]? replacement;
    private bool blocked;
    public bool IsOpen => Inner.IsOpen;
    public void Open(ConnectionProfile profile) => Inner.Open(profile);
    public void Write(ReadOnlySpan<byte> bytes)
    {
        string command = Encoding.ASCII.GetString(bytes).Trim();
        BeforeWrite?.Invoke(command);
        if (BlockVoltageMeasurement && command == "MEAS:VOLT?") { blocked = true; MeasurementEntered.Set(); }
        Inner.Write(bytes);
        if (ReplaceReply?.Invoke(command) is { } value) { Inner.DiscardInput(); replacement = Encoding.ASCII.GetBytes(value + "\r\n"); }
    }
    public int Read(Span<byte> buffer)
    {
        if (blocked) { Thread.Sleep(10); throw new TimeoutException(); }
        if (replacement is { } data) { data.CopyTo(buffer); replacement = null; return data.Length; }
        return Inner.Read(buffer);
    }
    public void SendBreak(CancellationToken token) { blocked = false; Inner.SendBreak(token); replacement = null; }
    public void DiscardInput() => Inner.DiscardInput();
    public void Close() => Inner.Close();
    public void Dispose() { MeasurementEntered.Dispose(); Inner.Dispose(); }
}
