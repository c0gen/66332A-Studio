using System.Windows;
using System.Windows.Controls;
using System.Globalization;
namespace Agilent66332A.Desktop.Controls;
public partial class NumericUnitEditor : UserControl
{
    public NumericUnitEditor() => InitializeComponent();
    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(nameof(Label), typeof(string), typeof(NumericUnitEditor));
    public static readonly DependencyProperty UnitProperty = DependencyProperty.Register(nameof(Unit), typeof(string), typeof(NumericUnitEditor));
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(NumericUnitEditor), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));
    public string Label { get => (string)GetValue(LabelProperty); set => SetValue(LabelProperty, value); }
    public string Unit { get => (string)GetValue(UnitProperty); set => SetValue(UnitProperty, value); }
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public static double Parse(string text)
    {
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out double value) || !double.IsFinite(value))
            throw new ArgumentException("Enter a finite number using your decimal separator. Omit digit grouping.");
        return value;
    }
}
