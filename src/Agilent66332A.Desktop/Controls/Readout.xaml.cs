using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Agilent66332A.Desktop.Controls;
public partial class Readout : UserControl
{
    public Readout() => InitializeComponent();
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(Readout));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(string), typeof(Readout));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(Readout));
    public static readonly DependencyProperty DetailProperty = DependencyProperty.Register(nameof(Detail), typeof(string), typeof(Readout));
    public static readonly DependencyProperty AccentProperty = DependencyProperty.Register(nameof(Accent), typeof(Brush), typeof(Readout), new PropertyMetadata(Brushes.White));
    public static readonly DependencyProperty DigitSizeProperty = DependencyProperty.Register(nameof(DigitSize), typeof(double), typeof(Readout), new PropertyMetadata(48.0));
    public static readonly DependencyProperty PlaceholderProperty = DependencyProperty.Register(nameof(Placeholder), typeof(string), typeof(Readout), new PropertyMetadata("-.----"));
    public static readonly DependencyProperty MinIntegerDigitsProperty = DependencyProperty.Register(nameof(MinIntegerDigits), typeof(int), typeof(Readout), new PropertyMetadata(1));
    public string Placeholder { get => (string)GetValue(PlaceholderProperty); set => SetValue(PlaceholderProperty, value); }
    public int MinIntegerDigits { get => (int)GetValue(MinIntegerDigitsProperty); set => SetValue(MinIntegerDigitsProperty, value); }
    public double DigitSize { get => (double)GetValue(DigitSizeProperty); set => SetValue(DigitSizeProperty, value); }
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Value { get => (string)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string Detail { get => (string)GetValue(DetailProperty); set => SetValue(DetailProperty, value); }
    public Brush Accent { get => (Brush)GetValue(AccentProperty); set => SetValue(AccentProperty, value); }
}
