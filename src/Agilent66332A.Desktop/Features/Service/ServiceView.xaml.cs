using System.Windows;
using System.Windows.Controls;

namespace Agilent66332A.Desktop.Features.Service;

public partial class ServiceView : UserControl
{
    public ServiceView() => InitializeComponent();

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
