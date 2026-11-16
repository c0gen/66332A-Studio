using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace Agilent66332A.Desktop.Controls;

internal static class WindowAppearance
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, uint attribute, ref int value, int size);

    public static void ApplyCaption(Window owner)
    {
        if (SystemParameters.HighContrast) return;

        IntPtr window = new WindowInteropHelper(owner).Handle;
        int darkMode = 1;
        DwmSetWindowAttribute(window, 20, ref darkMode, sizeof(int));
        SetCaptionColor(owner, window, 35, "Well");
        SetCaptionColor(owner, window, 34, "Line");
    }

    private static void SetCaptionColor(Window owner, IntPtr window, uint attribute, string resource)
    {
        var color = ((SolidColorBrush)owner.FindResource(resource)).Color;
        int colorRef = color.R | color.G << 8 | color.B << 16;
        DwmSetWindowAttribute(window, attribute, ref colorRef, sizeof(int));
    }

    public static void ApplyHighContrastPalette(Window owner)
    {
        if (!SystemParameters.HighContrast) return;

        foreach (string key in new[] { "Enclosure", "Surface", "Well", "DisplayWell", "TableStripe", "FaultSurface", "SurfaceRaised", "ActionSoft", "ReadySoft", "WarningSoft" })
            owner.Resources[key] = SystemColors.WindowBrush;
        foreach (string key in new[] { "Ink", "Muted", "Voltage", "Current", "Ready", "Warning", "Fault", "Line", "ControlLine", "FaultLine" })
            owner.Resources[key] = SystemColors.WindowTextBrush;
        owner.Resources["DisabledInk"] = SystemColors.GrayTextBrush;
        foreach (string key in new[] { "Action", "ActionHover", "ActionPressed", "Danger", "DangerHover" })
            owner.Resources[key] = SystemColors.HighlightBrush;
        owner.Resources["DangerInk"] = SystemColors.HighlightTextBrush;
        owner.Resources["ActionInk"] = SystemColors.HighlightTextBrush;
        owner.Resources["Selected"] = SystemColors.ControlBrush;
        owner.Resources["HoverOverlay"] = Brushes.Transparent;
        owner.Resources["PressedOverlay"] = Brushes.Transparent;
    }
}
