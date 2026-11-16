using System.IO;
using System.Windows;
using System.Windows.Controls;
using Agilent66332A.Application.Sessions;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Desktop.Shell;
using Agilent66332A.Desktop.Controls;
using Agilent66332A.Storage.Preferences;
using Agilent66332A.Storage.Recordings;

namespace Agilent66332A.Desktop;

public partial class App : System.Windows.Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        EventManager.RegisterClassHandler(typeof(Expander), Expander.ExpandedEvent, new RoutedEventHandler(RevealExpander));
        try { await StartAsync(e); }
        catch (Exception error)
        {
            string? report = Argument(e.Args, "--smoke-report");
            if (report is not null) await File.WriteAllTextAsync(report, error.ToString());
            else MessageBox.Show(error.Message, "66332A Studio startup failed");
            Shutdown(1);
        }
    }
    private static void RevealExpander(object sender, RoutedEventArgs args)
    {
        if (sender is Expander expander && ReferenceEquals(args.OriginalSource, expander) &&
            expander.Template?.FindName("ExpandSite", expander) is FrameworkElement content)
            Motion.Reveal(content, 6, 140);
    }

    private static string? Argument(string[] args, string key) => args.SkipWhile(a => a != key).Skip(1).FirstOrDefault();
    private async Task StartAsync(StartupEventArgs e)
    {
        var clock = new SystemClock();
        string? dataRoot = Argument(e.Args, "--data-root");
        var preferences = new PreferenceRepository(dataRoot is null ? UserPaths.Preferences : Path.Combine(dataRoot, "Preferences"));
        var (saved, warning) = await preferences.LoadAsync();
        if (dataRoot is not null) saved = saved with { RecordingDirectory = Path.Combine(dataRoot, "Recordings") };
        string recordingsPath = saved.RecordingDirectory;
        var session = new SessionCoordinator(clock, new RecordingFactory(() => recordingsPath));
        var vm = new ShellViewModel(session, clock, preferences, saved, path => recordingsPath = path);
        string? capturePath = Argument(e.Args, "--capture-artifacts");
        if (capturePath is not null) Motion.Enabled = false;
        var window = new MainWindow(vm);
        MainWindow = window;
        window.Show();
        if (warning is not null) vm.Message = warning;
        DispatcherUnhandledException += (_, args) => { vm.Message = "Application error: " + args.Exception.Message; args.Handled = true; _ = vm.EmergencyOffAsync("Application error"); };
        if (capturePath is not null)
        {
            await Verification.DesktopVerification.RunAsync(window, vm, capturePath);
            await session.DisconnectAsync();
            Shutdown(0); return;
        }
        if (e.Args.Contains("--simulator")) await vm.ConnectSimulatorAsync();
        if (e.Args.Contains("--smoke-test"))
        {
            try
            {
                if (session.State != Agilent66332A.Core.Models.ConnectionState.Ready) await vm.ConnectSimulatorAsync();
                await session.ApplyAsync(new(1, .01));
                var off = await session.OutputOffAsync("Packaged smoke test");
                string? output = Argument(e.Args, "--smoke-report");
                if (output is not null) await File.WriteAllTextAsync(output, $"WPF simulator startup; apply verified; shutdown {off.Outcome}\nRuntime directory: {System.Runtime.InteropServices.RuntimeEnvironment.GetRuntimeDirectory()}");
                await session.DisconnectAsync();
                Shutdown(off.Outcome == Agilent66332A.Core.Models.ShutdownOutcome.VerifiedOff ? 0 : 1);
            }
            catch (Exception error) { Console.Error.WriteLine(error); Shutdown(1); }
        }
    }
}
