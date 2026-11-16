using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Agilent66332A.Application.Sessions;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Storage.Recordings;

namespace Agilent66332A.Desktop.Features.Capture;

public sealed partial class CaptureViewModel : ObservableObject
{
    private readonly SessionCoordinator session;
    private readonly Func<Func<Task>, Task> execute;
    private readonly Func<string> recordingRoot;
    private readonly Func<string, Task> requestOff;
    private bool busy, active;
    private int fireRequested;
    [ObservableProperty] private MeasurementSignal signal;
    [ObservableProperty] private CurrentMeasurementRange range = CurrentMeasurementRange.High;
    [ObservableProperty] private CurrentDetector detector = CurrentDetector.AcDc;
    [ObservableProperty] private MeasurementWindow window = MeasurementWindow.Hanning;
    [ObservableProperty] private AcquisitionSource source;
    [ObservableProperty] private CaptureStart start;
    [ObservableProperty] private string points = "2048";
    [ObservableProperty] private string intervalMicroseconds = CaptureInput.Text(15.6);
    [ObservableProperty] private string offset = "0";
    [ObservableProperty] private string waitSeconds = "30";
    [ObservableProperty] private bool acknowledgeFaultOutputEvents;
    [ObservableProperty] private bool readyToFire;
    [ObservableProperty] private WaveformCapture? result;
    [ObservableProperty] private string progressText = "No capture yet";
    [ObservableProperty] private string exportLocation = "";

    public CaptureViewModel(SessionCoordinator session, Func<Func<Task>, Task> execute, Func<string> recordingRoot, Func<string, Task> requestOff)
    {
        this.session = session;
        this.execute = execute;
        this.recordingRoot = recordingRoot;
        this.requestOff = requestOff;
        Transient = new(session, execute, requestOff);
    }

    public SignalTriggerDraft VoltageTrigger { get; } = new();
    public SignalTriggerDraft CurrentTrigger { get; } = new();
    public TransientViewModel Transient { get; }
    public IReadOnlyList<CaptureChoice<MeasurementSignal>> Signals { get; } = [new("Voltage", MeasurementSignal.Voltage), new("Current", MeasurementSignal.Current)];
    public IReadOnlyList<CaptureChoice<CurrentMeasurementRange>> Ranges { get; } = [new("High", CurrentMeasurementRange.High), new("Low", CurrentMeasurementRange.Low)];
    public IReadOnlyList<CaptureChoice<CurrentDetector>> Detectors { get; } = [new("AC+DC", CurrentDetector.AcDc), new("DC", CurrentDetector.Dc)];
    public IReadOnlyList<CaptureChoice<MeasurementWindow>> Windows { get; } = [new("Hanning", MeasurementWindow.Hanning), new("Rectangular", MeasurementWindow.Rectangular)];
    public IReadOnlyList<CaptureChoice<AcquisitionSource>> Sources { get; } = [new("Internal", AcquisitionSource.Internal), new("Bus", AcquisitionSource.Bus)];
    public IReadOnlyList<CaptureChoice<CaptureStart>> StartModes { get; } =
    [new("Measure now", CaptureStart.MeasureNow), new("Arm on signal edge", CaptureStart.Internal),
     new("Arm for immediate trigger", CaptureStart.ImmediateTrigger), new("Arm for serial bus trigger", CaptureStart.SerialTrigger)];
    public bool DetectorEditable => Range == CurrentMeasurementRange.High;
    public bool IsEditable => !active && !busy && !session.IsRunning;
    public string Availability => session.State != ConnectionState.Ready ? "Connect a 66332A or start the simulator." :
        session.IsCompatibilityMode ? "Switch to SCPI to use waveform capture." :
        !Has(QualifiedFeature.Acquisition) ? "Capture controls require qualification for this serial profile." :
        !Has(StartFeature) ? "This trigger method has not been verified on this serial profile." : "";
    private QualifiedFeature StartFeature => Start switch
    {
        CaptureStart.ImmediateTrigger => QualifiedFeature.AcquisitionImmediate,
        CaptureStart.SerialTrigger => QualifiedFeature.AcquisitionBus,
        _ => QualifiedFeature.Acquisition
    };
    private bool Has(QualifiedFeature feature) => (session.Snapshot?.Capabilities.QualifiedFeatures & feature) == feature;
    private bool CanRead() => IsEditable && !session.IsCompatibilityMode && session.CanUseFeature<IAcquisitionClient>(false);
    private bool CanApply() => CanRead() && session.CanUseFeature<IAcquisitionClient>(true) && Has(QualifiedFeature.Acquisition);
    private bool CanStart() => CanApply() && Has(StartFeature);
    private bool CanStop() => active;
    private bool CanFire() => active && ReadyToFire;
    private bool CanExport() => !active && !busy && Result is not null;

    public void Refresh(bool shellBusy)
    {
        busy = shellBusy;
        OnPropertyChanged(nameof(Availability));
        OnPropertyChanged(nameof(IsEditable));
        ReadSettingsCommand.NotifyCanExecuteChanged();
        ApplySettingsCommand.NotifyCanExecuteChanged();
        StartCaptureCommand.NotifyCanExecuteChanged();
        StopCommand.NotifyCanExecuteChanged();
        FireCommand.NotifyCanExecuteChanged();
        ExportCommand.NotifyCanExecuteChanged();
        Transient.Refresh(shellBusy);
    }

    partial void OnStartChanged(CaptureStart value) => Refresh(busy);
    partial void OnRangeChanged(CurrentMeasurementRange value) => OnPropertyChanged(nameof(DetectorEditable));
    partial void OnReadyToFireChanged(bool value) => FireCommand.NotifyCanExecuteChanged();
    partial void OnResultChanged(WaveformCapture? value) => ExportCommand.NotifyCanExecuteChanged();

    private AcquisitionSettings BuildSettings() => new(Signal, Range, Detector, Window,
        new(CaptureInput.Integer(Points, "samples"), CaptureInput.Number(IntervalMicroseconds, "sample interval") / 1_000_000,
            CaptureInput.Integer(Offset, "trigger offset")), Source, VoltageTrigger.Build(), CurrentTrigger.Build());

    private void Load(AcquisitionSettings settings)
    {
        Signal = settings.Signal;
        Range = settings.Range;
        Detector = settings.Detector;
        Window = settings.Window;
        Source = settings.Source;
        Points = CaptureInput.Text(settings.Sweep.Points);
        IntervalMicroseconds = CaptureInput.Text(Math.Round(settings.Sweep.IntervalSeconds * 1_000_000, 8));
        Offset = CaptureInput.Text(settings.Sweep.OffsetPoints);
        VoltageTrigger.Load(settings.VoltageTrigger);
        CurrentTrigger.Load(settings.CurrentTrigger);
    }

    [RelayCommand(CanExecute = nameof(CanRead))]
    private Task ReadSettingsAsync() => execute(async () =>
    {
        Load(await session.UseFeatureAsync<IAcquisitionClient, AcquisitionSettings>((client, token) => client.ReadAcquisitionAsync(token), false));
        ProgressText = "Digitizer settings read from the supply";
    });

    [RelayCommand(CanExecute = nameof(CanApply))]
    private Task ApplySettingsAsync() => execute(async () =>
    {
        var target = BuildSettings();
        Load(await session.UseFeatureAsync<IAcquisitionClient, AcquisitionSettings>((client, token) => client.ApplyAcquisitionAsync(target, token), true));
        ProgressText = "Digitizer settings verified";
    });

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartCaptureAsync() => execute(async () =>
    {
        var request = new CaptureRequest(BuildSettings(), Start, CaptureInput.WaitLimit(WaitSeconds), AcknowledgeFaultOutputEvents);
        active = true;
        ExportLocation = "";
        Interlocked.Exchange(ref fireRequested, 0);
        Refresh(busy);
        var progress = new Progress<CaptureProgress>(UpdateProgress);
        try
        {
            Result = await session.UseFeatureAsync<IAcquisitionClient, WaveformCapture>((client, token) =>
                client.CaptureAsync(request, progress, () => Interlocked.Exchange(ref fireRequested, 0) == 1, token), true);
            Load(Result.Settings);
            ProgressText = $"Captured {Result.Samples.Count:N0} samples · {Result.Settings.Signal}";
        }
        catch (OperationCanceledException) { ProgressText = "Capture stopped"; }
        catch { ProgressText = "Capture did not complete"; throw; }
        finally
        {
            active = false;
            ReadyToFire = false;
            Refresh(busy);
        }
    });

    private void UpdateProgress(CaptureProgress progress)
    {
        if (!active) return;
        ReadyToFire = progress.CanFire && Volatile.Read(ref fireRequested) == 0;
        ProgressText = progress.Phase switch
        {
            CapturePhase.Configuring => "Verifying capture settings",
            CapturePhase.Waiting when ReadyToFire => "Armed · ready to fire",
            CapturePhase.Waiting => "Waiting for acquisition",
            CapturePhase.Downloading => $"Receiving samples · {progress.Received:N0} / {progress.Expected:N0}",
            CapturePhase.ReadingStatistics => "Reading measurement statistics",
            _ => "Capture in progress"
        };
    }

    [RelayCommand(CanExecute = nameof(CanFire))]
    private void Fire() { Interlocked.Exchange(ref fireRequested, 1); ReadyToFire = false; }
    [RelayCommand(CanExecute = nameof(CanStop))]
    private Task StopAsync() => requestOff("Stop capture");

    [RelayCommand(CanExecute = nameof(CanExport))]
    private Task ExportAsync() => execute(async () =>
    {
        if (Result is { } capture) ExportLocation = await WaveformExport.ExportAsync(recordingRoot(), capture);
    });
}
