using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace Agilent66332A.Desktop.Features.Sequences;

public sealed class SequencePreview : FrameworkElement
{
    public SequencePreview()
    {
        Height = 64;
        System.Windows.Automation.AutomationProperties.SetName(this, "Requested voltage and current-limit preview for one repeat");
        DataContextChanged += (_, args) =>
        {
            if (args.OldValue is SequenceViewModel oldVm) oldVm.PropertyChanged -= Changed;
            if (args.NewValue is SequenceViewModel newVm) newVm.PropertyChanged += Changed;
            InvalidateVisual();
        };
    }
    private void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs args) { if (args.PropertyName is nameof(SequenceViewModel.Preview) or nameof(SequenceViewModel.ProgressText)) InvalidateVisual(); }
    protected override void OnRender(DrawingContext dc)
    {
        base.OnRender(dc);
        dc.DrawRectangle((Brush)FindResource("Well"), null, new Rect(RenderSize));
        if (DataContext is not SequenceViewModel vm) return;
        try
        {
            var d = vm.Build();
            if (d.Steps.Length == 0 || d.Steps.Any(s => !double.IsFinite(s.HoldSeconds) || s.HoldSeconds < 1 || !double.IsFinite(s.VoltageV) || !double.IsFinite(s.CurrentLimitA))) return;
            double duration = d.Steps.Sum(s => s.HoldSeconds);
            if (!double.IsFinite(duration) || duration <= 0) return;
            double maxV = Math.Max(.01, d.Steps.Max(s => s.VoltageV)), maxI = Math.Max(.001, d.Steps.Max(s => s.CurrentLimitA));
            double left = 120, right = Math.Max(left + 1, ActualWidth - 12);
            var face = new Typeface((FontFamily)FindResource("BodyFont"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            Brush muted = (Brush)FindResource("Muted");
            void DrawLabel(string text, double y)
            {
                var label = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, face, 12, muted,
                    VisualTreeHelper.GetDpi(this).PixelsPerDip);
                dc.DrawText(label, new Point(12, y));
            }

            DrawLabel($"Voltage ≤ {maxV:G4} V", 6);
            DrawLabel($"Limit ≤ {maxI:G4} A", 36);
            var voltagePen = new Pen((Brush)FindResource("Voltage"), 2);
            var currentPen = new Pen((Brush)FindResource("Current"), 2);
            var boundary = new Pen((Brush)FindResource("Line"), 1);
            Brush active = (Brush)FindResource("ActionSoft");
            double elapsed = 0;
            for (int index = 0; index < d.Steps.Length; index++)
            {
                var step = d.Steps[index];
                double start = left + elapsed / duration * (right - left);
                elapsed += step.HoldSeconds;
                double end = left + elapsed / duration * (right - left);
                if (vm.Rows[index].IsActive) dc.DrawRectangle(active, null, new Rect(start, 0, end - start, ActualHeight));
                dc.DrawLine(boundary, new(start, 0), new(start, ActualHeight));
                double voltageY = 26 - step.VoltageV / maxV * 20;
                double currentY = 56 - step.CurrentLimitA / maxI * 20;
                dc.DrawLine(voltagePen, new(start, voltageY), new(end, voltageY));
                dc.DrawLine(currentPen, new(start, currentY), new(end, currentY));
            }
            dc.DrawLine(boundary, new(right, 0), new(right, ActualHeight));
        }
        catch (ArgumentException) { }
    }
}
