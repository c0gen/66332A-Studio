using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Agilent66332A.Core.Models;
using Agilent66332A.Desktop.Shell;
using Agilent66332A.Instruments.Simulation;
using Agilent66332A.Storage.Recordings;
using CommunityToolkit.Mvvm.Input;

namespace Agilent66332A.Desktop.Verification;

// Opt-in real-WPF integration checks and off-screen bitmap rendering, isolated with --data-root.
internal static partial class DesktopVerification
{
    public static async Task RunAsync(MainWindow window, ShellViewModel vm, string path)
    {
        var readoutFont = (FontFamily)window.FindResource("MeasurementFont");
        Require(new Typeface(readoutFont, FontStyles.Normal, FontWeights.Regular, FontStretches.Normal).TryGetGlyphTypeface(out var readoutFace)
            && readoutFace.FontUri.AbsolutePath.EndsWith("DSEG7Classic-Regular.ttf", StringComparison.OrdinalIgnoreCase),
            "The measurement display did not resolve the bundled DSEG7 Classic font.");

        var plex = new FontFamily(new Uri("pack://application:,,,/Agilent66332A.Desktop;component/Assets/Fonts/"), "./#IBM Plex Sans");
        foreach (var weight in new[] { FontWeights.Regular, FontWeights.Medium, FontWeights.SemiBold })
            Require(new Typeface(plex, FontStyles.Normal, weight, FontStretches.Normal).TryGetGlyphTypeface(out var face)
                && face.Weight == weight && face.StyleSimulations == StyleSimulations.None, $"IBM Plex Sans {weight} is not bundled.");

        Directory.CreateDirectory(path);
        var errors = new List<string>();
        var listener = new BindingErrors(errors);
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        try
        {
            await (vm.RefreshPortsCommand.ExecutionTask ?? Task.CompletedTask);
            Require(vm.Session.State == ConnectionState.Disconnected, "Startup opened an instrument session.");
            string discoveredPorts = vm.Ports.Count == 0 ? "none" : string.Join(", ", vm.Ports.Select(port => port.DisplayName));
            await CaptureAsync(window, path, "01-disconnected");
            window.SelectWorkspace(6);
            await VerifyServiceHelpAsync(window, vm.Service);
            window.SelectWorkspace(0);
            await VerifyConnectionSettingsAsync(window, vm, path);
            await vm.ConnectSimulatorAsync();
            Require(vm.Session.State == ConnectionState.Ready, "Simulator connection failed: " + vm.Message);
            await Task.Delay(1100);
            await CaptureAsync(window, path, "02-simulator-off");
            var outputControl = (Button)window.FindName("OutputControlButton");
            Require(ReferenceEquals(outputControl, FindVisibleButton(window, "Output On")) && outputControl.IsEnabled,
                "The header did not offer Output On after connection.");
            Require(FindVisibleButton(window, "Apply & turn on") is { IsEnabled: true },
                "Manual Apply was unavailable with output off and acknowledged setpoints.");
            vm.Manual.Voltage = "4";
            Require(vm.Manual.IsDirty && !outputControl.IsEnabled, "Dirty draft did not block Output On.");
            await ClickAsync(window, "Apply & turn on");
            Require(vm.Session.Snapshot is { Output: OutputState.On, Settings.VoltageV: 4 },
                "Manual Apply did not verify the new setpoint and enable output.");
            await Task.Delay(1100);
            await CaptureAsync(window, path, "03-output-on");
            vm.Manual.Voltage = "3";
            Require(ReferenceEquals(outputControl, FindVisibleButton(window, "Output Off")) && outputControl.IsEnabled &&
                FindVisibleButton(window, "Output On") is null, "The header did not offer a single Output Off action while energized.");
            await CaptureAsync(window, path, "04-pending-draft");
            await ClickAsync(window, "Output Off");
            Require(vm.Session.LastShutdown?.Outcome == ShutdownOutcome.VerifiedOff &&
                ReferenceEquals(outputControl, FindVisibleButton(window, "Output On")) && !outputControl.IsEnabled,
                "The header did not return to Output On after verified shutdown with pending edits.");
            vm.Manual.RestoreCommand.Execute(null);
            window.Width = 1120; window.Height = 680;
            await CaptureAsync(window, path, "05-compact-1120x680");
            window.Width = 1366; window.Height = 768;
            await CaptureAsync(window, path, "06-laptop-1366x768");
            window.Width = 1280; window.Height = 720;
            await CaptureAsync(window, path, "07-layout-1280x720-at-150", 1.5);
            window.Width = 1440; window.Height = 900;
            await ClickAsync(window, "Start recording");
            Require(vm.Session.IsRecording, "Start recording did not start a recording.");
            await ClickAsync(window, "Stop recording");
            Require(!vm.Session.IsRecording, "Stop recording did not finish the recording.");

            window.SelectWorkspace(1);
            var run = vm.Sequences.RunCommand.ExecuteAsync(null);
            await Task.Delay(700);
            Require(vm.Session.IsRunning && vm.Sequences.IsLocked, "Active sequence did not lock editing.");
            Require(vm.Sequences.Rows.Count(row => row.IsActive) == 1 && !vm.Sequences.IsDirty,
                "The active-step indicator did not follow the run independently of edits.");
            await CaptureAsync(window, path, "08-active-sequence");
            var latency = Stopwatch.StartNew();
            await ClickAsync(window, "Stop run");
            double shutdownMs = latency.Elapsed.TotalMilliseconds;
            await run;
            Require(vm.Session.LastRun?.ExecutionOutcome == ExecutionOutcome.Cancelled && vm.Session.LastShutdown?.Outcome == ShutdownOutcome.VerifiedOff,
                "Stop did not produce Cancelled / VerifiedOff.");
            window.SelectWorkspace(2); await vm.Recordings.RefreshAsync();
            await CaptureAsync(window, path, "09-recordings");
            window.SelectWorkspace(3);
            await CaptureAsync(window, path, "10-diagnostics");
            await VerifyAdvancedWorkspacesAsync(window, vm, path);
            window.SelectWorkspace(0);
            vm.Scenario = SimulatorScenario.CableLoss;
            await Task.Delay(1400);
            Require(vm.Session.State == ConnectionState.Lost && vm.Session.Snapshot!.Output == OutputState.Unknown, "Cable loss did not invalidate state.");
            await CaptureAsync(window, path, "11-lost-unknown");
            await VerifyMinimumLayoutAsync(window);
            await File.WriteAllTextAsync(Path.Combine(path, "verification.txt"),
                $"Real WPF simulator integration passed. Output-off confirmation: {shutdownMs:F1} ms.\n" +
                $"Hardware port discovery at disconnected startup: {discoveredPorts}.\n" +
                "Dirty-draft enable guard, run edit lock, Stop, recording discovery, and cable-loss unknown state checked.\n" +
                "WPF recording controls, active-step indication, and both actions on the header output control checked.\n" +
                "Waveform capture/export, serial trigger fire, Output Off during capture, setup readback, self-test and calibration discard checked.\n" +
                "Service guidance and manual link targets checked at 1000 × 620, without acknowledgment, disconnected and in the simulator.\n" +
                "All seven workspaces keep the output control visible at 1000 × 620.\n" +
                "Modeless connection settings reuse one window and can verify Output Off without disconnecting.\n" +
                "150% image is render-scale evidence, not a mixed-monitor DPI/accessibility acceptance.\n" +
                "Binding errors: " + errors.Count + "\n" + string.Join("\n", errors));
            Require(errors.Count == 0, "WPF binding errors: " + string.Join("\n", errors));
        }
        finally { PresentationTraceSources.DataBindingSource.Listeners.Remove(listener); }
    }

    private static async Task VerifyAdvancedWorkspacesAsync(MainWindow window, ShellViewModel vm, string path)
    {
        vm.Manual.RestoreCommand.Execute(null);
        await ClickAsync(window, "Output On");
        Require(vm.Session.Snapshot?.Output == OutputState.On, "The simulator did not enable for waveform testing: " + vm.Message);
        window.SelectWorkspace(4);
        window.Width = 1120;
        window.Height = 680;
        vm.Capture.Points = "128";
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await ClickAsync(window, "Start capture");
        Require(vm.Capture.Result is { } capture && capture.Samples.Count == 128 && capture.Statistics.Maximum > 0,
            "Waveform capture failed: " + vm.Message);
        await ClickAsync(window, "Export capture");
        Require(File.Exists(Path.Combine(vm.Capture.ExportLocation, "waveform.csv")) &&
                File.Exists(Path.Combine(vm.Capture.ExportLocation, "capture.json")), "Capture export is incomplete.");
        Require(RecordingCatalog.Discover(vm.RecordingDirectory).All(recording => Path.GetFileName(recording.DirectoryPath) != "Captures"),
            "Capture exports appeared in the trend recording catalog.");
        await CaptureAsync(window, path, "12-waveform-1120x680");

        vm.Capture.Start = CaptureStart.SerialTrigger;
        var armedCapture = vm.Capture.StartCaptureCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => vm.Capture.FireCommand.CanExecute(null));
        vm.Capture.FireCommand.Execute(null);
        await armedCapture.WaitAsync(TimeSpan.FromSeconds(5));
        Require(vm.Capture.Result?.Start == CaptureStart.SerialTrigger, "Serial trigger capture did not complete.");

        vm.Capture.Start = CaptureStart.Internal;
        vm.Capture.VoltageTrigger.Level = (vm.Session.Snapshot!.Settings.VoltageV + 1).ToString(System.Globalization.CultureInfo.CurrentCulture);
        var waitingCapture = vm.Capture.StartCaptureCommand.ExecuteAsync(null);
        await WaitUntilAsync(() => vm.Capture.ProgressText == "Waiting for acquisition");
        await VerifyConnectionOutputOffAsync(window, vm);
        await waitingCapture.WaitAsync(TimeSpan.FromSeconds(5));
        Require(!vm.Session.IsFeatureActive && vm.Session.LastShutdown?.Outcome == ShutdownOutcome.VerifiedOff,
            "Output Off did not release the capture and verify shutdown.");

        window.SelectWorkspace(5);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await ClickAsync(window, "Read instrument");
        await CaptureAsync(window, path, "13-instrument-setup-1120x680");
        window.SelectWorkspace(6);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await ClickAsync(window, "Read service settings");
        await CaptureAsync(window, path, "14-service-1120x680");
        vm.Service.SelectedProcedure = vm.Service.CalibrationProcedures.Single(choice => choice.Procedure == CalibrationProcedure.Overvoltage);
        await VerifyServiceHelpAsync(window, vm.Service);
        vm.Service.DestructiveActionConfirmed = true;
        await ClickAsync(window, "Run self-test");
        Require(vm.Session.Snapshot?.Output == OutputState.Off, "Service self-test did not leave the output off.");

        vm.Service.CalibrationWiringConfirmed = true;
        var calibration = vm.Service.StartCalibrationAsync("");
        await WaitUntilAsync(() => vm.Service.PendingPrompt?.Kind == CalibrationPromptKind.Fixture);
        vm.Service.ProceedCalibrationCommand.Execute(null);
        await WaitUntilAsync(() => vm.Service.PendingPrompt?.Kind == CalibrationPromptKind.Save);
        vm.Service.CancelCalibrationCommand.Execute(null);
        await calibration.WaitAsync(TimeSpan.FromSeconds(5));
        Require(vm.Service.Result.Contains("discarded", StringComparison.OrdinalIgnoreCase) && vm.Session.Snapshot?.Output == OutputState.Off,
            "Calibration discard did not leave the output off: " + vm.Message);
        window.Width = 1440;
        window.Height = 900;
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var timeout = Stopwatch.StartNew();
        while (!condition() && timeout.Elapsed < TimeSpan.FromSeconds(5)) await Task.Delay(30);
        Require(condition(), "The instrument did not become ready for a trigger.");
    }

    private static Button? FindVisibleButton(DependencyObject root, string caption)
    {
        if (root is Button { IsVisible: true } button && Equals(button.Content, caption)) return button;
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            if (FindVisibleButton(VisualTreeHelper.GetChild(root, i), caption) is { } match) return match;
        }
        return null;
    }

    private static async Task ClickAsync(Window window, string caption)
    {
        var button = FindVisibleButton(window, caption);
        if (button is not { IsEnabled: true, Command: IAsyncRelayCommand command })
            throw new InvalidOperationException($"Button unavailable: {caption}.");

        var peer = new ButtonAutomationPeer(button);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        await (command.ExecutionTask ?? throw new InvalidOperationException($"Command did not run: {caption}."));
    }

    private static async Task VerifyMinimumLayoutAsync(MainWindow window)
    {
        var tabs = (TabControl)window.FindName("WorkspaceTabs");
        var navigation = (ListBox)window.FindName("WorkspaceNav");
        var content = (FrameworkElement)window.Content;
        window.Width = 1000;
        window.Height = 620;
        for (int index = 0; index < tabs.Items.Count; index++)
        {
            window.SelectWorkspace(index);
            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();
            Require(navigation.SelectedIndex == index, $"Navigation selection does not match workspace {index}.");
            AssertOutputControlVisible(window, content, $"workspace {index} at 1000 × 620");
            var peer = UIElementAutomationPeer.CreatePeerForElement(tabs);
            Require(peer?.GetChildren()?.Count > 0, $"Workspace {index} has no accessible content.");
        }
        window.SelectWorkspace(0);
    }

    private static void AssertOutputControlVisible(Window window, FrameworkElement content, string state)
    {
        var control = window is MainWindow
            ? window.FindName("OutputControlButton") as Button
            : FindVisibleButton(content, "Output Off");
        if (control is not { IsVisible: true }) throw new InvalidOperationException($"The output control is hidden in {state}.");
        Rect bounds = control.TransformToAncestor(content).TransformBounds(new Rect(control.RenderSize));
        Require(bounds.Width > 0 && bounds.Height > 0 && new Rect(content.RenderSize).Contains(bounds), $"The output control is clipped in {state}.");
    }

    private static void Require(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    private static async Task CaptureAsync(Window window, string path, string name, double scale = 1)
    {
        await Task.Delay(100);
        await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
        window.UpdateLayout();
        var content = (FrameworkElement)window.Content;
        AssertOutputControlVisible(window, content, name);
        var bitmap = new RenderTargetBitmap((int)Math.Ceiling(content.ActualWidth * scale), (int)Math.Ceiling(content.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        var drawing = new DrawingVisual();
        var brush = new VisualBrush(content) { ViewboxUnits = BrushMappingMode.Absolute, Viewbox = new Rect(content.Margin.Left, content.Margin.Top, content.ActualWidth, content.ActualHeight), Stretch = Stretch.Fill };
        using (var dc = drawing.RenderOpen()) dc.DrawRectangle(brush, null, new Rect(0, 0, content.ActualWidth, content.ActualHeight));
        bitmap.Render(drawing);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        await using var file = File.Create(Path.Combine(path, name + ".png")); encoder.Save(file);
    }
    private sealed class BindingErrors(List<string> errors) : TraceListener
    {
        public override void Write(string? message) { if (!string.IsNullOrWhiteSpace(message)) errors.Add(message); }
        public override void WriteLine(string? message) => Write(message);
    }
}
