using System.Diagnostics;

namespace Agilent66332A.Core.Abstractions;

public interface IMonotonicClock
{
    double Seconds { get; }
    DateTimeOffset UtcNow { get; }
    Task DelayAsync(TimeSpan duration, CancellationToken token);
}
public sealed class SystemClock : IMonotonicClock
{
    private readonly Stopwatch watch = Stopwatch.StartNew();
    public double Seconds => watch.Elapsed.TotalSeconds;
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
    public Task DelayAsync(TimeSpan duration, CancellationToken token) => Task.Delay(duration, token);
}
