using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Agilent66332A.Desktop.Controls;

public static class Motion
{
    public static bool Enabled { get; set; } = SystemParameters.ClientAreaAnimation && !SystemParameters.HighContrast;

    public static void Reveal(FrameworkElement element, double offsetY = 8, int milliseconds = 160)
    {
        if (!Enabled) return;

        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var shift = new TranslateTransform();
        element.RenderTransform = shift;
        var duration = TimeSpan.FromMilliseconds(milliseconds);
        element.BeginAnimation(UIElement.OpacityProperty, new DoubleAnimation(0, 1, duration)
        {
            EasingFunction = ease, FillBehavior = FillBehavior.Stop
        });
        shift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(offsetY, 0, duration)
        {
            EasingFunction = ease, FillBehavior = FillBehavior.Stop
        });
    }
}
