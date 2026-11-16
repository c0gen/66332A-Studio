using System.ComponentModel;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Agilent66332A.Desktop.Controls;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using Agilent66332A.Core.Models;
using Agilent66332A.Desktop.Features.About;

namespace Agilent66332A.Desktop.Shell;
public partial class MainWindow : Window
{
    private readonly ShellViewModel vm;
    private bool allowClose, closing;
    private ConnectionState displayedConnection;
    private ConnectionWindow? connectionWindow;

    public MainWindow(ShellViewModel vm)
    {
        this.vm = vm;
        InitializeComponent();
        DataContext = vm;
        displayedConnection = vm.ConnectionStatus;
        vm.PropertyChanged += ConnectionChanged;
        Width = Math.Min(1440, SystemParameters.WorkArea.Width); Height = Math.Min(900, SystemParameters.WorkArea.Height);
        Closing += OnClosing;
        SizeChanged += (_, args) => MeasurementDisplay.IsCompact = args.NewSize.Height < 780;
        SystemEvents.PowerModeChanged += PowerChanged;
        Closed += (_, _) =>
        {
            vm.StopTimer();
            vm.PropertyChanged -= ConnectionChanged;
            SystemEvents.PowerModeChanged -= PowerChanged;
        };
        WindowAppearance.ApplyHighContrastPalette(this);
        UpdateConnectionPulse();
    }

    private void ConnectionChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName != nameof(ShellViewModel.ConnectionStatus) || displayedConnection == vm.ConnectionStatus) return;
        displayedConnection = vm.ConnectionStatus;
        UpdateConnectionPulse();
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        WindowAppearance.ApplyCaption(this);
    }

    private void OpenConnectionSettings(object sender, RoutedEventArgs args)
    {
        if (closing || allowClose) return;

        if (connectionWindow is { } existing)
        {
            if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            existing.Activate();
            return;
        }

        var settings = new ConnectionWindow(vm) { Owner = this };
        settings.Closed += (_, _) => connectionWindow = null;
        connectionWindow = settings;
        settings.Show();
    }

    private void OpenAbout(object sender, RoutedEventArgs args)
    {
        if (closing || allowClose) return;

        if (OwnedWindows.OfType<AboutWindow>().FirstOrDefault() is { } existing)
        {
            existing.Activate();
            return;
        }

        new AboutWindow(vm) { Owner = this }.Show();
    }

    private void UpdateConnectionPulse()
    {
        var scale = (ScaleTransform)ConnectionPulse.RenderTransform;
        if (displayedConnection == ConnectionState.Ready && Motion.Enabled)
        {
            var fade = new DoubleAnimation(0.5, 0, TimeSpan.FromSeconds(1.8)) { RepeatBehavior = RepeatBehavior.Forever };
            var grow = new DoubleAnimation(1, 2.4, fade.Duration) { RepeatBehavior = RepeatBehavior.Forever };
            ConnectionPulse.BeginAnimation(OpacityProperty, fade);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, grow);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, grow);
        }
        else
        {
            ConnectionPulse.BeginAnimation(OpacityProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
        }
    }

    private async void OnClosing(object? sender, CancelEventArgs args)
    {
        if (allowClose) return;
        args.Cancel = true;
        if (closing) return;
        closing = true;
        try
        {
            connectionWindow?.Close();
            if (vm.Session.State != ConnectionState.Disconnected) await vm.Session.DisconnectAsync();
            await vm.SavePreferencesAsync();
            if (vm.Session.LastShutdown?.Outcome == ShutdownOutcome.Unverified)
            {
                vm.CloseUnverified = true;
                vm.Message = "Output off unverified. Check the supply. Reconnect to retry.";
                return;
            }
            allowClose = true; Close();
        }
        catch (Exception e) { vm.Message = "Close needs attention: " + e.Message; vm.CloseUnverified = true; }
        finally { closing = false; }
    }
    private void AcknowledgeClose(object sender, RoutedEventArgs args) { allowClose = true; Close(); }
    public void SelectWorkspace(int index) => WorkspaceTabs.SelectedIndex = index;
    private async void PowerChanged(object sender, PowerModeChangedEventArgs args)
    {
        if (args.Mode is PowerModes.Suspend or PowerModes.Resume)
            await Dispatcher.InvokeAsync(async () => { await vm.EmergencyOffAsync("Windows suspend/resume — reconnect required"); await vm.Session.DisconnectAsync(); });
    }
    private async void WorkspaceChanged(object sender, SelectionChangedEventArgs args)
    {
        if (args.Source != WorkspaceTabs) return;
        if (WorkspaceTabs.SelectedContent is FrameworkElement view && IsLoaded) Motion.Reveal(view);
        if (WorkspaceTabs.SelectedIndex == 2 && DataContext is ShellViewModel shell)
            await shell.Recordings.RefreshAsync();
    }
}
