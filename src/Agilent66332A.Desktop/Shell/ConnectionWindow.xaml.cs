using System.Windows;
using Agilent66332A.Desktop.Controls;

namespace Agilent66332A.Desktop.Shell;

public partial class ConnectionWindow : Window
{
    public ConnectionWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Height = Math.Min(Height, SystemParameters.WorkArea.Height);
        WindowAppearance.ApplyHighContrastPalette(this);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowAppearance.ApplyCaption(this);
    }
}
