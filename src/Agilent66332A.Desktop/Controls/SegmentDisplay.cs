using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace Agilent66332A.Desktop.Controls;

public sealed class SegmentDisplay : FrameworkElement
{
    private static DependencyProperty Register(string name, Type type, object defaultValue) =>
        DependencyProperty.Register(name, type, typeof(SegmentDisplay),
            new FrameworkPropertyMetadata(defaultValue, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TextProperty = Register(nameof(Text), typeof(string), "");
    public static readonly DependencyProperty PlaceholderProperty = Register(nameof(Placeholder), typeof(string), "-.----");
    public static readonly DependencyProperty DigitSizeProperty = Register(nameof(DigitSize), typeof(double), 64.0);
    public static readonly DependencyProperty MinIntegerDigitsProperty = Register(nameof(MinIntegerDigits), typeof(int), 1);
    public static readonly DependencyProperty ForegroundProperty = Register(nameof(Foreground), typeof(Brush), Brushes.White);
    public static readonly DependencyProperty IdleBrushProperty = Register(nameof(IdleBrush), typeof(Brush), Brushes.Gray);
    public static readonly DependencyProperty FontFamilyProperty = Register(nameof(FontFamily), typeof(FontFamily), new FontFamily("Consolas"));

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public double DigitSize { get => (double)GetValue(DigitSizeProperty); set => SetValue(DigitSizeProperty, value); }
    public int MinIntegerDigits { get => (int)GetValue(MinIntegerDigitsProperty); set => SetValue(MinIntegerDigitsProperty, value); }
    public Brush Foreground { get => (Brush)GetValue(ForegroundProperty); set => SetValue(ForegroundProperty, value); }
    public Brush IdleBrush { get => (Brush)GetValue(IdleBrushProperty); set => SetValue(IdleBrushProperty, value); }
    public FontFamily FontFamily { get => (FontFamily)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }

    private readonly record struct Cell(char Character, double Width, bool HasGhost);
    private bool Live => !string.IsNullOrEmpty(Text) && Text != "—";
    private static bool IsDigitCell(char character) => char.IsDigit(character) || character == '-';

    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property != TextProperty && change.Property != ForegroundProperty) return;

        if (!Live || SystemParameters.HighContrast || Foreground is not SolidColorBrush color)
            Effect = null;
        else if (Effect is not DropShadowEffect glow || glow.Color != color.Color)
            Effect = new DropShadowEffect { ShadowDepth = 0, BlurRadius = 18, Opacity = 0.45, Color = color.Color };
    }

    private FormattedText Glyph(string text, Brush brush) => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
        new Typeface(FontFamily, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), DigitSize, brush,
        VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private List<Cell> Cells(double digitWidth)
    {
        string shown = Live ? Text : Placeholder;
        var cells = new List<Cell>();
        for (int i = shown.TakeWhile(IsDigitCell).Count(); i < MinIntegerDigits; i++)
            cells.Add(new(' ', digitWidth, true));

        foreach (char character in shown)
        {
            bool digitCell = IsDigitCell(character);
            double width = digitCell ? digitWidth : Glyph(character.ToString(), Foreground).WidthIncludingTrailingWhitespace;
            cells.Add(new(character, width, digitCell));
        }
        return cells;
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var reference = Glyph("8", Foreground);
        return new(Cells(reference.WidthIncludingTrailingWhitespace).Sum(cell => cell.Width), reference.Height);
    }

    protected override void OnRender(DrawingContext context)
    {
        bool highContrast = SystemParameters.HighContrast;
        var ghostBrush = Foreground.CloneCurrentValue();
        ghostBrush.Opacity *= 0.07;
        ghostBrush.Freeze();
        var ghost = Glyph("8", ghostBrush);
        Brush lit = Live ? Foreground : IdleBrush;
        double x = 0;
        foreach (var cell in Cells(ghost.WidthIncludingTrailingWhitespace))
        {
            if (cell.HasGhost && !highContrast) context.DrawText(ghost, new Point(x, 0));
            if (cell.Character != ' ')
            {
                var glyph = Glyph(cell.Character.ToString(), lit);
                double offset = cell.Width - glyph.WidthIncludingTrailingWhitespace;
                context.DrawText(glyph, new Point(x + (cell.Character == '-' ? offset / 2 : offset), 0));
            }
            x += cell.Width;
        }
    }
}
