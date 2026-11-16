using Agilent66332A.Core.Models;

namespace Agilent66332A.Application.Monitoring;

public sealed class MeasurementHistory
{
    private readonly object gate = new();
    private readonly Queue<MeasurementPair> samples = new();
    public void Add(MeasurementPair sample)
    {
        lock (gate)
        {
            samples.Enqueue(sample);
            while (samples.Count > 3602 || samples.Count > 0 && sample.OldestMonotonicSeconds - samples.Peek().OldestMonotonicSeconds > 3600) samples.Dequeue();
        }
    }
    public MeasurementPair[] Snapshot() { lock (gate) return samples.ToArray(); }
    public void Clear() { lock (gate) samples.Clear(); }
}
