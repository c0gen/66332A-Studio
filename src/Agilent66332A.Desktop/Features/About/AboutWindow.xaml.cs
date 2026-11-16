using System.Reflection;
using System.Windows;
using System.Windows.Input;
using Agilent66332A.Desktop.Controls;
using Agilent66332A.Desktop.Shell;

namespace Agilent66332A.Desktop.Features.About;

public partial class AboutWindow : Window
{
    public AboutWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        var assembly = typeof(AboutWindow).Assembly;
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion.Split('+')[0]
            ?? assembly.GetName().Version?.ToString(3);
        VersionText.Text = $"Version {version ?? "unknown"}";
        WindowAppearance.ApplyHighContrastPalette(this);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowAppearance.ApplyCaption(this);
    }

    private void CloseAbout(object sender, ExecutedRoutedEventArgs args) => Close();
}
