using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace Agilent66332A.Desktop.Features.Service;

public partial class ServiceView : UserControl
{
    public ServiceView()
    {
        InitializeComponent();
        AddHandler(Hyperlink.RequestNavigateEvent, new RequestNavigateEventHandler(OpenGuide));
    }

    private static void OpenGuide(object sender, RequestNavigateEventArgs e)
    {
        e.Handled = true;
        string message = "";
        try
        {
            using var process = Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch (Win32Exception)
        {
            message = "  Windows couldn’t open the guide.";
        }
        if (e.OriginalSource is Hyperlink { Tag: Run status }) status.Text = message;
    }

    private async void StartCalibration(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ServiceViewModel { CanStartCalibration: true } service }) return;
        string passcode = Passcode.Password;
        Passcode.Clear();
        await service.StartCalibrationAsync(passcode);
    }

    private async void ChangePasscode(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: ServiceViewModel { CanChangePasscode: true } service }) return;
        string oldPasscode = Passcode.Password;
        string newPasscode = NewPasscode.Password;
        Passcode.Clear(); NewPasscode.Clear();
        await service.ChangePasscodeAsync(oldPasscode, newPasscode);
    }
}
