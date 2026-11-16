using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using Agilent66332A.Desktop.Controls;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Desktop.Features.Capture;

public partial class CaptureView : UserControl
{
    public CaptureView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => Draw();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (args.OldValue is CaptureViewModel old) old.PropertyChanged -= OnCaptureChanged;
        if (args.NewValue is CaptureViewModel current) current.PropertyChanged += OnCaptureChanged;
        if (IsLoaded) Draw();
    }

    private void OnCaptureChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(CaptureViewModel.Result) && IsLoaded) Draw();
    }

    private void Draw()
    {
        var plot = WaveformPlot.Plot;
        plot.Clear();
        PlotTheme.Apply(plot, WaveformPlot);
        if ((DataContext as CaptureViewModel)?.Result is { } capture)
        {
            double[] seconds = Enumerable.Range(0, capture.Samples.Count).Select(capture.SecondsAt).ToArray();
            double[] values = capture.Samples.ToArray();
            var line = plot.Add.Scatter(seconds, values);
            bool voltage = capture.Settings.Signal == MeasurementSignal.Voltage;
            string unit = voltage ? "V" : "A";
            PlotTheme.StyleTrace(line, PlotTheme.Color(WaveformPlot, voltage ? "Voltage" : "Current"), values);
            line.MarkerSize = seconds.Length < 100 ? 3 : 0;
            plot.Axes.Bottom.Label.Text = capture.Start == CaptureStart.MeasureNow ? "Time (s)" : "Time from trigger (s)";
            plot.Axes.Left.Label.Text = $"{capture.Settings.Signal} ({unit})";
            plot.Axes.AutoScale();
            var s = capture.Statistics;
            DcStat.Text = $"{s.Dc:G6}";
            RmsStat.Text = $"{s.Rms:G6}";
            MinimumStat.Text = $"{s.Minimum:G6}";
            MaximumStat.Text = $"{s.Maximum:G6}";
            PulseLowStat.Text = $"{s.PulseLow:G6}";
            PulseHighStat.Text = $"{s.PulseHigh:G6}";
            StatisticsText.Text = $"DC {s.Dc:G6} {unit}   ·   RMS {s.Rms:G6} {unit}   ·   Min {s.Minimum:G6}   Max {s.Maximum:G6}   ·   Pulse low {s.PulseLow:G6}   high {s.PulseHigh:G6}";
        }
        else
        {
            plot.Axes.Bottom.Label.Text = "Time (s)";
            plot.Axes.Left.Label.Text = "";
            StatisticsText.Text = "";
        }
        bool hasStatistics = StatisticsText.Text.Length != 0;
        StatisticsTiles.Visibility = hasStatistics ? Visibility.Visible : Visibility.Collapsed;
        EmptyWaveform.Visibility = hasStatistics ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(StatisticsTiles, StatisticsText.Text);
        WaveformPlot.Refresh();
    }
}
