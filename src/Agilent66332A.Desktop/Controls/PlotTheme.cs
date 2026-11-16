using System.IO;
using System.Windows;
using System.Windows.Media;
using ScottPlot;
using ScottPlot.Plottables;
using Color = ScottPlot.Color;

namespace Agilent66332A.Desktop.Controls;

internal static class PlotTheme
{
    private const string FontName = "IBM Plex Sans";
    private static readonly Lazy<bool> FontReady = new(RegisterFont);

    private static bool RegisterFont()
    {
        string file = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "IBMPlexSans-Regular.ttf");
        if (!File.Exists(file)) return false;

        ScottPlot.Fonts.AddFontFile(FontName, file);
        return true;
    }

    public static Color Color(FrameworkElement owner, string key)
    {
        var color = ((SolidColorBrush)owner.FindResource(key)).Color;
        return new(color.R, color.G, color.B, color.A);
    }

    public static void Apply(Plot plot, FrameworkElement owner)
    {
        plot.FigureBackground.Color = plot.DataBackground.Color = Color(owner, "Well");
        plot.Axes.Color(Color(owner, "Muted"));
        plot.Axes.FrameColor(Color(owner, "Line"));
        plot.Axes.Top.FrameLineStyle.Width = 0;
        plot.Axes.Right.FrameLineStyle.Width = 0;
        plot.Grid.MajorLineColor = Color(owner, "Line").WithAlpha(0.6);
        plot.Axes.Bottom.MinorTickStyle.Length = plot.Axes.Left.MinorTickStyle.Length = 0;
        plot.Axes.Bottom.MajorTickStyle.Length = plot.Axes.Left.MajorTickStyle.Length = 4;
        if (FontReady.Value) plot.Font.Set(FontName);
        plot.Axes.Bottom.TickLabelStyle.FontSize = plot.Axes.Left.TickLabelStyle.FontSize = 12;
    }

    public static void StyleTrace(Scatter trace, Color channel, double[] values)
    {
        trace.Color = channel;
        trace.LineWidth = 2;
        if (SystemParameters.HighContrast || !values.All(double.IsFinite)) return;

        trace.FillY = true;
        trace.FillYColor = channel.WithAlpha(0.10);
    }
}

