using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Threading;
using Agilent66332A.Core.Models;
using Agilent66332A.Desktop.Shell;

namespace Agilent66332A.Desktop.Verification;

internal static partial class DesktopVerification
{
    private static async Task<ConnectionWindow> OpenConnectionSettingsAsync(MainWindow window)
    {
        var button = (Button)window.FindName("ConnectionButton");
        var peer = new ButtonAutomationPeer(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        return window.OwnedWindows.OfType<ConnectionWindow>().Single(settings => settings.IsVisible);
    }

    private static async Task VerifyConnectionSettingsAsync(MainWindow window, ShellViewModel vm, string path)
    {
        Require(window.OwnedWindows.Count == 0, "Connection settings opened at startup.");
        var settings = await OpenConnectionSettingsAsync(window);
        try
        {
            Require(window.IsEnabled && ReferenceEquals(settings.DataContext, vm), "Connection settings did not share the active shell.");
            Require(ReferenceEquals(settings, await OpenConnectionSettingsAsync(window)), "Connection settings opened a second window.");
            Require(FindVisibleButton(settings, "Connect")?.Command == vm.ConnectHardwareCommand &&
                    FindVisibleButton(settings, "Disconnect")?.Command == vm.DisconnectCommand &&
                    FindVisibleButton(settings, "Refresh ports")?.Command == vm.RefreshPortsCommand,
                "Connection settings lost a connection control.");
            await CaptureAsync(settings, path, "15-connection-settings");
        }
        finally { settings.Close(); }
    }

    private static async Task VerifyConnectionOutputOffAsync(MainWindow window, ShellViewModel vm)
    {
        var settings = await OpenConnectionSettingsAsync(window);
        settings.Close();
        Require(vm.Session.State == ConnectionState.Ready && vm.Session.Snapshot?.Output == OutputState.On,
            "Closing connection settings changed the active session.");

        settings = await OpenConnectionSettingsAsync(window);
        try
        {
            await ClickAsync(settings, "Output Off");
            Require(vm.Session.State == ConnectionState.Ready && vm.Session.Snapshot?.Output == OutputState.Off &&
                    vm.Session.LastShutdown?.Outcome == ShutdownOutcome.VerifiedOff,
                "Output Off from connection settings was not verified.");
        }
        finally { settings.Close(); }
    }
}
