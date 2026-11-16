using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Agilent66332A.Core.Models;
using Agilent66332A.Desktop.Shell;
using ScottPlot;
using ScottPlot.WPF;
using Color = ScottPlot.Color;

namespace Agilent66332A.Desktop.Controls;
public partial class TrendPanel : UserControl
{
    private ShellViewModel? vm;
    private bool synchronizing;
    private double? cursorX;
    private readonly List<ScottPlot.Plottables.VerticalLine> cursorLines = [];
    public TrendPanel()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (vm is not null) return;
            vm = (ShellViewModel)DataContext;
            vm.PlotChanged += Render;
            foreach (var plot in new[] { VoltagePlot, CurrentPlot, PowerPlot })
            {
                plot.PreviewMouseDown += (_, _) => vm.FollowLive = false;
                plot.PreviewMouseWheel += (_, _) => vm.FollowLive = false;
                plot.MouseMove += Inspect;
                StylePlot(plot);
            }
            VoltagePlot.Plot.RenderManager.RenderFinished += (_, _) => SynchronizeAll(VoltagePlot);
            CurrentPlot.Plot.RenderManager.RenderFinished += (_, _) => SynchronizeAll(CurrentPlot);
            PowerPlot.Plot.RenderManager.RenderFinished += (_, _) => SynchronizeAll(PowerPlot);
            Render();
        };
    }
    private void SynchronizeAll(WpfPlot from)
    {
        foreach (var to in new[] { VoltagePlot, CurrentPlot, PowerPlot }) if (to != from) Synchronize(from, to);
    }
    private void Synchronize(WpfPlot from, WpfPlot to)
    {
        if (vm?.FollowLive != false || synchronizing) return;
        var a = from.Plot.Axes.GetLimits(); var b = to.Plot.Axes.GetLimits();
        if (Math.Abs(a.Left - b.Left) < 1e-10 && Math.Abs(a.Right - b.Right) < 1e-10) return;
        synchronizing = true;
        try { to.Plot.Axes.SetLimitsX(a.Left, a.Right); to.Refresh(); }
        finally { synchronizing = false; }
    }
    private void Inspect(object sender, MouseEventArgs e)
    {
        if (vm is null || vm.FreezeCursor || sender is not WpfPlot host) return;
        var point = e.GetPosition(host);
        var coordinate = host.Plot.GetCoordinates(new Pixel((float)(point.X * host.DisplayScale), (float)(point.Y * host.DisplayScale)));
        var samples = vm.Session.History.Snapshot();
        if (samples.Length == 0) return;
        var p = samples.MinBy(s => Math.Abs(s.Voltage.StartedUtc.LocalDateTime.ToOADate() - coordinate.X))!;
        cursorX = p.Voltage.StartedUtc.LocalDateTime.ToOADate();
        if (cursorLines.Count == 0) Render();
        foreach (var line in cursorLines) line.X = cursorX.Value;
        VoltagePlot.Refresh(); CurrentPlot.Refresh(); PowerPlot.Refresh();
        string voltage = p.Voltage.Value?.ToString("G6") ?? "—";
        string current = p.Current.Value?.ToString("G6") ?? "—";
        string quality = p.Quality == SampleQuality.Valid ? "" : $"  •  {p.Quality}";
        CursorText.Text = $"{voltage} V at {p.Voltage.CompletedUtc.LocalDateTime:HH:mm:ss.fff}  /  " +
                          $"{current} A at {p.Current.CompletedUtc.LocalDateTime:HH:mm:ss.fff}{quality}";
    }
    private void Render()
    {
        if (vm is null) return;
        VoltageRow.Height = vm.ShowVoltage ? new(1, GridUnitType.Star) : new(0);
        CurrentRow.Height = vm.ShowCurrent ? new(1, GridUnitType.Star) : new(0);
        PowerRow.Height = vm.ShowPower ? new(1, GridUnitType.Star) : new(0);
        NoChannels.Visibility = vm.ShowVoltage || vm.ShowCurrent || vm.ShowPower ? Visibility.Collapsed : Visibility.Visible;
        cursorLines.Clear();
        var samples = vm.Session.History.Snapshot();
        double last = samples.Length > 0 ? samples[^1].Voltage.StartedUtc.LocalDateTime.ToOADate() : DateTime.Now.ToOADate();
        var x = new List<double>(); var v = new List<double>(); var i = new List<double>(); var power = new List<double>();
        MeasurementPair? previous = null;
        foreach (var s in samples)
        {
            if (previous is not null && s.OldestMonotonicSeconds - previous.OldestMonotonicSeconds > Math.Max(3, vm.PollSeconds * 3))
            { x.Add(s.Voltage.StartedUtc.LocalDateTime.ToOADate()); v.Add(double.NaN); i.Add(double.NaN); power.Add(double.NaN); }
            x.Add(s.Voltage.StartedUtc.LocalDateTime.ToOADate());
            v.Add(s.Quality == SampleQuality.Valid ? s.Voltage.Value ?? double.NaN : double.NaN);
            i.Add(s.Quality == SampleQuality.Valid ? s.Current.Value ?? double.NaN : double.NaN); power.Add(s.EstimatedPowerW ?? double.NaN); previous = s;
        }
        double[] time = x.ToArray();
        Draw(VoltagePlot, time, v.ToArray(), "Voltage", vm.Session.Snapshot?.Settings.VoltageV, last, !vm.ShowCurrent && !vm.ShowPower);
        Draw(CurrentPlot, time, i.ToArray(), "Current", vm.Session.Snapshot?.Settings.CurrentLimitA, last, !vm.ShowPower);
        if (vm.ShowPower) Draw(PowerPlot, time, power.ToArray(), "Ink", null, last, true);
    }

    private static void StylePlot(WpfPlot host)
    {
        var plot = host.Plot;
        plot.Axes.Rules.Clear();
        PlotTheme.Apply(plot, host);
        plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.DateTimeAutomatic { LabelFormatter = dt => dt.ToString("HH:mm:ss") };
        plot.Axes.Left.MinimumSize = 53;
    }

    private void Draw(WpfPlot host, double[] x, double[] y, string colorResource, double? setpoint, double last, bool showTimeLabels)
    {
        var plot = host.Plot;
        var limits = plot.Axes.GetLimits();
        plot.Clear();
        plot.Axes.Bottom.TickLabelStyle.IsVisible = showTimeLabels;
        Color channel = PlotTheme.Color(host, colorResource);
        if (x.Length > 0)
        {
            var line = plot.Add.Scatter(x, y);
            PlotTheme.StyleTrace(line, channel, y);
            line.MarkerSize = 0;
            if (double.IsFinite(y[^1])) plot.Add.Marker(x[^1], y[^1], MarkerShape.FilledCircle, 7, channel);
        }
        if (setpoint is { } value)
        {
            var target = plot.Add.HorizontalLine(value);
            target.Color = channel.WithAlpha(0.5);
            target.LinePattern = LinePattern.Dashed;
        }
        foreach (var entry in vm!.TimelineEvents.Where(e => e.Type is "transition" or "shutdown" or "failure"))
        {
            double when = entry.Utc.LocalDateTime.ToOADate();
            if (when < last - vm.PlotMinutes / 1440.0 || when > last) continue;
            var marker = plot.Add.VerticalLine(when);
            marker.Color = PlotTheme.Color(host, entry.Type == "failure" ? "Fault" : "Muted").WithAlpha(.55);
            marker.LinePattern = LinePattern.Dotted;
        }
        if (cursorX is { } cx)
        {
            var cursor = plot.Add.VerticalLine(cx);
            cursor.Color = PlotTheme.Color(host, "Ink").WithAlpha(0.5);
            cursor.LinePattern = LinePattern.Dotted;
            cursorLines.Add(cursor);
        }
        plot.Axes.Margins(0, 0.15);
        plot.Axes.AutoScaleY();
        if (vm!.FollowLive) plot.Axes.SetLimitsX(last - vm.PlotMinutes / 1440.0, last + 2.0 / 86400);
        else plot.Axes.SetLimitsX(limits.Left, limits.Right);
        host.Refresh();
    }
}
