using System.Windows;
using System.Windows.Controls;
namespace Agilent66332A.Desktop.Controls;
public partial class StatusBadge : UserControl
{
    public StatusBadge() => InitializeComponent();
    public static readonly DependencyProperty TextProperty = DependencyProperty.Register(nameof(Text), typeof(string), typeof(StatusBadge));
    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
}
