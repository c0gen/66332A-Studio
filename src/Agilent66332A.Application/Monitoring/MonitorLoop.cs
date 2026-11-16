using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Application.Monitoring;

public static class MonitorLoop
{
    public static async Task RunAsync(IInstrumentClient instrument, IMonotonicClock clock, Func<double> interval,
        Action<MeasurementPair> publish, Action health, CancellationToken token, Action? stateRead = null)
    {
        while (true)
        {
            token.ThrowIfCancellationRequested();
            double start = clock.Seconds;
            // Ordinary control pauses let the bounded current transaction finish. Emergency
            // shutdown cancels it through the instrument worker's own generation latch.
            InstrumentStatus status;
            if (instrument.CanMeasureDc)
            {
                var sample = await instrument.MeasureAsync().ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                publish(sample);
                status = sample.Snapshot.Status;
            }
            else
            {
                var state = await instrument.ReadSnapshotAsync().ConfigureAwait(false);
                token.ThrowIfCancellationRequested();
                stateRead?.Invoke();
                status = state.Status;
            }
            if (status.HasFault) throw new IOException("Protection condition: " + status.FaultFlags);
            double due = start + interval();
            // Skip missed deadlines. Never accumulate polling work.
            while (clock.Seconds < due)
            {
                health();
                await clock.DelayAsync(TimeSpan.FromSeconds(Math.Max(.001, Math.Min(.2, due - clock.Seconds))), token).ConfigureAwait(false);
            }
        }
    }
}
