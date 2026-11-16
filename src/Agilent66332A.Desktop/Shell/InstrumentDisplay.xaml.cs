using System.Windows;
using System.Windows.Controls;

namespace Agilent66332A.Desktop.Shell;

public partial class InstrumentDisplay : UserControl
{
    public InstrumentDisplay() => InitializeComponent();

    public static readonly DependencyProperty IsCompactProperty =
        DependencyProperty.Register(nameof(IsCompact), typeof(bool), typeof(InstrumentDisplay));

    public bool IsCompact
    {
        get => (bool)GetValue(IsCompactProperty);
        set => SetValue(IsCompactProperty, value);
    }
}
