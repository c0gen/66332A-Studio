using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Agilent66332A.Application.Sessions;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Core.Policies;
using Agilent66332A.Desktop.Features.Capture;
using Agilent66332A.Desktop.Features.Diagnostics;
using Agilent66332A.Desktop.Features.InstrumentSetup;
using Agilent66332A.Desktop.Features.Monitor;
using Agilent66332A.Desktop.Features.Recordings;
using Agilent66332A.Desktop.Features.Sequences;
using Agilent66332A.Desktop.Features.Service;
using Agilent66332A.Instruments.Devices;
using Agilent66332A.Instruments.Simulation;
using Agilent66332A.Storage.Preferences;

namespace Agilent66332A.Desktop.Shell;

public sealed partial class ShellViewModel : ObservableObject
{
    private readonly IMonotonicClock clock;
    private readonly PreferenceRepository preferences;
    private readonly Action<string> setRecordingRoot;
    private readonly Dispatcher dispatcher = System.Windows.Application.Current.Dispatcher;
    private readonly DispatcherTimer timer;
    private SimulatedTransport? simulator;
    private MeasurementPair? lastSample;
    private double previousSampleSeconds;
    private double achievedRate;
    private long connectedGeneration;
    public SessionCoordinator Session { get; }
    public ManualViewModel Manual { get; }
    public SequenceViewModel Sequences { get; }
    public RecordingsViewModel Recordings { get; }
    public DiagnosticsViewModel Diagnostics { get; }
    public CaptureViewModel Capture { get; }
    public InstrumentSetupViewModel InstrumentSetup { get; }
    public ServiceViewModel Service { get; }
    public ObservableCollection<StudioEvent> TimelineEvents { get; } = [];
    public double[] PollIntervals { get; } = [1, 2, 5, 10];
    public int[] PlotRanges { get; } = [1, 5, 15, 60];
    [ObservableProperty] private double pollSeconds = 1;
    [ObservableProperty] private int plotMinutes = 5;
    [ObservableProperty] private bool followLive = true;
    [ObservableProperty] private bool showVoltage = true;
    [ObservableProperty] private bool showCurrent = true;
    [ObservableProperty] private bool showPower;
    [ObservableProperty] private bool freezeCursor;
    [ObservableProperty] private string recordingDirectory;
    [ObservableProperty] private bool busy;
    [ObservableProperty] private string message = "Select a serial port to connect.";
    [ObservableProperty] private SimulatorScenario scenario;
    [ObservableProperty] private bool closeUnverified;
    public event Action? PlotChanged;

    public ShellViewModel(SessionCoordinator session, IMonotonicClock clock, PreferenceRepository preferences, StudioPreferences saved, Action<string> setRecordingRoot)
    {
        Session = session; this.clock = clock; this.preferences = preferences; this.setRecordingRoot = setRecordingRoot;
        InitializeConnectionPreferences(saved.Profile);
        pollSeconds = saved.PollSeconds; plotMinutes = saved.PlotMinutes; recordingDirectory = saved.RecordingDirectory;
        session.PollSeconds = pollSeconds;
        Manual = new(session, preferences, ExecuteAsync);
        Sequences = new(session, clock, ExecuteAsync);
        Recordings = new(() => RecordingDirectory, ExecuteAsync);
        Diagnostics = new(session, ExecuteAsync);
        Capture = new(session, ExecuteAsync, () => RecordingDirectory, EmergencyOffAsync);
        InstrumentSetup = new(session, ExecuteAsync);
        Service = new(session, ExecuteAsync);
        session.Changed += () => Dispatch(Refresh);
        session.Event += entry => Dispatch(() =>
        {
            Diagnostics.Add(entry);
            Message = entry.Type switch
            {
                "connected" or "shutdown" => "",
                "verified" => "Command verified.",
                "recordingStarted" => "Recording started.",
                "failure" when Session.LastShutdown is { } shutdown => shutdown.Reason,
                _ => entry.Message
            };
            TimelineEvents.Add(entry); while (TimelineEvents.Count > 512) TimelineEvents.RemoveAt(0);
            PlotChanged?.Invoke();
        });
        session.Trace += line => Dispatch(() => Diagnostics.AddTrace(line));
        session.Progress += p => Dispatch(() => { Sequences.Progress = p; Sequences.Tick(); });
        session.Sample += pair => Dispatch(() =>
        {
            if (pair.Generation != Session.Snapshot?.Generation || Session.State != ConnectionState.Ready) return;
            if (previousSampleSeconds != 0 && pair.OldestMonotonicSeconds > previousSampleSeconds) achievedRate = 1 / (pair.OldestMonotonicSeconds - previousSampleSeconds);
            previousSampleSeconds = pair.OldestMonotonicSeconds; lastSample = pair; PlotChanged?.Invoke(); Refresh();
        });
        timer = new(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background, (_, _) => { RefreshTelemetry(); Sequences.Tick(); }, dispatcher);
        timer.Start();
        portTimer = new(TimeSpan.FromSeconds(2), DispatcherPriority.Background, (_, _) => PollPorts(), dispatcher);
        portTimer.Start();
        PollPorts();
    }
    private void Dispatch(Action action) { if (dispatcher.CheckAccess()) action(); else dispatcher.InvokeAsync(action); }
    public ConnectionState ConnectionStatus => Session.State;
    public string ConnectionLabel => ConnectionStatus switch
    {
        ConnectionState.Connecting => $"Connecting {Session.SourceKind.ToLowerInvariant()}",
        ConnectionState.Ready when Session.IsCompatibilityMode => "Compatibility mode",
        ConnectionState.Ready => Session.SourceKind == "Simulator" ? "Simulator ready" :
            Session.Snapshot?.Capabilities.HardwareValidated == true ? "Hardware control ready" : "Hardware monitoring",
        ConnectionState.Lost => $"{Session.SourceKind} connection lost",
        _ => "Disconnected"
    };
    public string IdentityLabel => Session.SourceKind == "Hardware" && Session.Snapshot is { } snapshot
        ? $"{snapshot.Identity.Manufacturer} {snapshot.Identity.Model} / {snapshot.Identity.SerialNumber}"
        : "";
    private bool Available => Session.State == ConnectionState.Ready && Session.CanMeasureDc && !Session.IsFeatureActive &&
        lastSample is not null && !SettingsPolicy.IsStale(clock.Seconds, lastSample, PollSeconds);
    public string VoltageValue => Available ? lastSample!.Voltage.Value?.ToString("F4", CultureInfo.CurrentCulture) ?? "—" : "—";
    public string CurrentValue => Available ? lastSample!.Current.Value?.ToString("F5", CultureInfo.CurrentCulture) ?? "—" : "—";
    public string PowerValue => Available ? lastSample!.EstimatedPowerW?.ToString("F4", CultureInfo.CurrentCulture) ?? "—" : "—";
    public string VoltageDetail => Session.Snapshot is { } s ? $"Setpoint {s.Settings.VoltageV:G} V" : "Setpoint —";
    public string CurrentDetail => Session.Snapshot is { } s ? $"Limit {s.Settings.CurrentLimitA:G} A" : "Limit —";
    public OutputState OutputStatus => Session.Snapshot?.Output ?? OutputState.Unknown;
    public string RegulationMode => Session.Snapshot?.Status.RegulationMode ?? "";
    public bool HasFault => Session.Snapshot?.Status.HasFault == true;
    public bool IsSimulator => Session.SourceKind == "Simulator" && Session.State != ConnectionState.Disconnected;
    public bool IsFresh => Available;
    public string OutputLabel => OutputStatus switch
    {
        OutputState.On => $"Output on / {Session.Snapshot?.Status.RegulationMode}",
        OutputState.Off => "Output off",
        _ => "Output unknown"
    };
    public string AgeLabel
    {
        get
        {
            if (Session.IsFeatureActive) return "Live polling paused for instrument operation";
            if (Session.IsCompatibilityMode) return "Use Service for Compatibility readings";
            if (Session.State == ConnectionState.Ready && !Session.CanMeasureDc) return "Live polling paused for the current sweep settings";
            if (lastSample is null) return "Waiting for measurements";
            double age = Math.Max(0, clock.Seconds - lastSample.OldestMonotonicSeconds);
            return Available ? $"Updated {age:F1} s ago" : $"Stale reading from {age:F1} s ago";
        }
    }
    public string LastKnown => !Available && lastSample is { } p ? $"Last known {p.Voltage.Value:G6} V / {p.Current.Value:G6} A" : "";
    public string Footer => string.Join("  •  ", new[]
    {
        Available && achievedRate > 0 ? $"{achievedRate:F2} updates/s" : "",
        Session.Snapshot?.Status.HasFault == true ? Session.Snapshot.Status.FaultFlags
            : Session.Snapshot?.Status.MeasurementOverload == true ? "Current measurement overload"
            : Session.State == ConnectionState.Ready && !Session.IsCompatibilityMode ? "No reported faults" : "",
        Session.LastShutdown?.Outcome switch
        {
            ShutdownOutcome.VerifiedOff => "Last shutdown verified",
            ShutdownOutcome.Unverified => Session.SourceKind == "Hardware" ? "Shutdown unverified. Check the supply." : "Shutdown unverified",
            _ => ""
        }
    }.Where(text => text.Length > 0));
    public bool IsRecordingManually => Session.IsRecording;
    public bool IsRecordingActive => Session.IsRunning || IsRecordingManually;
    public string RecordingLabel => Session.IsRunning ? "Recording sequence" : "Recording";
    public bool CanOff => Session.Snapshot is not null && Session.State is not ConnectionState.Disconnected;
    public bool ShowOutputOn => ConnectionStatus == ConnectionState.Ready && OutputStatus == OutputState.Off &&
        !Busy && !Session.IsRunning && !Session.IsFeatureActive && !Session.IsShuttingDown;
    public bool CanConnect => !Busy && !Session.IsRunning && Session.State is ConnectionState.Disconnected or ConnectionState.Lost;
    private void RefreshTelemetry()
    {
        foreach (string property in new[] { nameof(VoltageValue), nameof(CurrentValue), nameof(PowerValue), nameof(AgeLabel), nameof(IsFresh), nameof(LastKnown), nameof(Footer) }) OnPropertyChanged(property);
    }
    private void Refresh()
    {
        if (Session.Snapshot is { } s && s.Generation != connectedGeneration)
        { connectedGeneration = s.Generation; Manual.RestoreCommand.Execute(null); }
        foreach (string property in new[] { nameof(ConnectionStatus), nameof(ConnectionLabel), nameof(IdentityLabel),
            nameof(ProfileLabel), nameof(VoltageDetail), nameof(CurrentDetail), nameof(OutputStatus), nameof(OutputLabel),
            nameof(RegulationMode), nameof(HasFault), nameof(IsSimulator),
            nameof(IsRecordingActive), nameof(IsRecordingManually), nameof(RecordingLabel),
            nameof(CanOff), nameof(ShowOutputOn), nameof(CanConnect) }) OnPropertyChanged(property);
        Manual.Refresh(Busy); Sequences.Refresh(Busy);
        Capture.Refresh(Busy); InstrumentSetup.Refresh(Busy); Service.Refresh(Busy); Diagnostics.Refresh(Busy);
        RefreshConnectionDetails();
        ConnectHardwareCommand.NotifyCanExecuteChanged(); RefreshPortsCommand.NotifyCanExecuteChanged();
        StartRecordingCommand.NotifyCanExecuteChanged(); StopRecordingCommand.NotifyCanExecuteChanged();
        RefreshTelemetry();
    }
    public async Task ExecuteAsync(Func<Task> action)
    {
        if (Busy) return;
        Busy = true;
        try { await action(); }
        catch (Exception e) { Message = e.Message; Diagnostics.Add(new(clock.UtcNow, clock.Seconds, "error", e.Message)); }
        finally { Busy = false; Refresh(); }
    }
    public Task ConnectSimulatorAsync() => ExecuteAsync(async () =>
    {
        simulator = new() { Scenario = Scenario };
        connectedGeneration = 0; lastSample = null; previousSampleSeconds = 0;
        await Session.ConnectAsync(new AgilentClient(simulator, clock, true, "Simulator"), PreferenceProfile);
        Manual.RestoreCommand.Execute(null); await Manual.ReloadPresetsAsync(); PlotChanged?.Invoke();
    });
    [RelayCommand] private Task DisconnectAsync() => ExecuteAsync(() => Session.DisconnectAsync());
    [RelayCommand] private Task OutputOffAsync() => EmergencyOffAsync("Output Off / Stop");
    public async Task EmergencyOffAsync(string reason)
    {
        Message = "Verifying output off…";
        try
        {
            var result = await Session.OutputOffAsync(reason);
            Message = result.Outcome == ShutdownOutcome.VerifiedOff
                ? $"Output off verified. {result.Error}".TrimEnd() : $"Output off unverified. {result.Error}".TrimEnd();
        }
        catch (Exception e) { Message = "Output off unverified. " + e.Message; }
        Refresh();
    }
    private bool CanRecord() => !Busy && Session.State == ConnectionState.Ready && Session.CanMeasureDc && !Session.IsRunning && !Session.IsRecording;
    private bool CanStopRecord() => !Busy && Session.IsRecording;
    [RelayCommand(CanExecute = nameof(CanRecord))] private Task StartRecordingAsync() => ExecuteAsync(() => Session.StartManualRecordingAsync("Manual bench recording"));
    [RelayCommand(CanExecute = nameof(CanStopRecord))] private Task StopRecordingAsync() => ExecuteAsync(() => Session.StopManualRecordingAsync());
    [RelayCommand] private void ResetView() { FollowLive = true; PlotChanged?.Invoke(); }
    public Task SavePreferencesAsync() => preferences.SaveAsync(new(1, PreferenceProfile, PollSeconds, PlotMinutes, RecordingDirectory));
    partial void OnBusyChanged(bool value) => Refresh();
    partial void OnPollSecondsChanged(double value) { Session.PollSeconds = value; }
    partial void OnPlotMinutesChanged(int value) => PlotChanged?.Invoke();
    partial void OnShowVoltageChanged(bool value) => PlotChanged?.Invoke();
    partial void OnShowCurrentChanged(bool value) => PlotChanged?.Invoke();
    partial void OnShowPowerChanged(bool value) => PlotChanged?.Invoke();
    partial void OnScenarioChanged(SimulatorScenario value) { if (simulator is not null) simulator.Scenario = value; }
    partial void OnRecordingDirectoryChanged(string value) => setRecordingRoot(value);
    public void StopTimer()
    {
        timer.Stop();
        portTimer.Stop();
    }
}
