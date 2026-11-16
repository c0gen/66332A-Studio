using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;

namespace Studio.Tests;
public static class TestModels
{
    public static DeviceSnapshot State => new(1, new("AGILENT", "66332A", "TEST", "1", "AGILENT,66332A,TEST,1"),
        new(new(0, 20, .005), new(0, 5, .001), new(0, 22, .005), new(0, 2147483.647, .001), true),
        new(5, .1, DateTimeOffset.UnixEpoch), new(22, false, .08), OutputState.Off,
        new("CV", 256, 0, "", DateTimeOffset.UnixEpoch), DateTimeOffset.UnixEpoch);
    public static MeasurementPair Pair(double elapsed = 0, double current = -.02) => new(1, 1,
        new(5, DateTimeOffset.UnixEpoch, DateTimeOffset.UnixEpoch, elapsed),
        new(current, DateTimeOffset.UnixEpoch.AddMilliseconds(5), DateTimeOffset.UnixEpoch.AddMilliseconds(10), elapsed + .01), SampleQuality.Valid, State);
}
public sealed class FakeClock : IMonotonicClock
{
    public double Seconds { get; set; }
    public DateTimeOffset UtcNow => DateTimeOffset.UnixEpoch.AddSeconds(Seconds);
    public Action? OnDelay { get; set; }
    public Task DelayAsync(TimeSpan duration, CancellationToken token)
    { token.ThrowIfCancellationRequested(); Seconds += duration.TotalSeconds; OnDelay?.Invoke(); token.ThrowIfCancellationRequested(); return Task.CompletedTask; }
}
