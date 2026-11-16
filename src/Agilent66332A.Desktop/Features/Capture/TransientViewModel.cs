using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Agilent66332A.Application.Sessions;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Desktop.Features.Capture;

public sealed partial class TransientViewModel(SessionCoordinator session, Func<Func<Task>, Task> execute,
    Func<string, Task> requestOff) : ObservableObject
{
    private bool busy, active;
    private int fireRequested;
    [ObservableProperty] private string voltage = "0";
    [ObservableProperty] private string current = "0";
    [ObservableProperty] private string waitSeconds = "30";
    [ObservableProperty] private TransientTrigger trigger;
    [ObservableProperty] private bool continuous;
    [ObservableProperty] private bool readyToFire;
    [ObservableProperty] private string readback = "Read pending levels before arming";

    public IReadOnlyList<CaptureChoice<TransientTrigger>> Triggers { get; } =
        [new("Immediate trigger", TransientTrigger.Immediate), new("Serial bus trigger", TransientTrigger.Serial)];
    public bool IsEditable => !active && !busy && !session.IsRunning;
    public string Availability => session.State != ConnectionState.Ready ? "Connect a 66332A or start the simulator." :
        session.IsCompatibilityMode ? "Switch to SCPI to use output triggers." :
        !Has(TriggerFeature) ? "This trigger method has not been verified on this serial profile." : "";

    private QualifiedFeature TriggerFeature => Trigger == TransientTrigger.Serial ? QualifiedFeature.TransientBus : QualifiedFeature.TransientImmediate;
    private bool Has(QualifiedFeature feature) => (session.Snapshot?.Capabilities.QualifiedFeatures & feature) == feature;
    private bool CanRead() => IsEditable && !session.IsCompatibilityMode && session.CanUseFeature<ITransientClient>(false);
    private bool CanArm() => CanRead() && Has(TriggerFeature) && session.CanUseFeature<ITransientClient>(true);
    private bool CanAbort() => active || CanRead() && session.CanUseFeature<ITransientClient>(true) &&
        (Has(QualifiedFeature.Acquisition) || Has(QualifiedFeature.TransientImmediate) || Has(QualifiedFeature.TransientBus));
    private bool CanFire() => active && ReadyToFire;
    public bool IsActive => active;

    public void Refresh(bool shellBusy)
    {
        busy = shellBusy;
        OnPropertyChanged(nameof(IsEditable));
        OnPropertyChanged(nameof(IsActive));
        OnPropertyChanged(nameof(Availability));
        ReadLevelsCommand.NotifyCanExecuteChanged();
        ArmCommand.NotifyCanExecuteChanged();
        AbortCommand.NotifyCanExecuteChanged();
        FireCommand.NotifyCanExecuteChanged();
    }

    partial void OnReadyToFireChanged(bool value) => FireCommand.NotifyCanExecuteChanged();
    partial void OnTriggerChanged(TransientTrigger value) => Refresh(busy);

    [RelayCommand(CanExecute = nameof(CanRead))]
    private Task ReadLevelsAsync() => execute(async () =>
    {
        var levels = await session.UseFeatureAsync<ITransientClient, TransientSettings>((client, token) => client.ReadTransientAsync(token), false);
        LoadLevels(levels);
    });

    [RelayCommand(CanExecute = nameof(CanArm))]
    private Task ArmAsync() => execute(async () =>
    {
        var request = new TransientRequest(new(CaptureInput.Number(Voltage, "pending voltage"), CaptureInput.Number(Current, "pending current limit")),
            Trigger, Continuous, CaptureInput.WaitLimit(WaitSeconds));
        Interlocked.Exchange(ref fireRequested, 0);
        Readback = "Verifying and arming pending levels";
        active = true;
        Refresh(busy);
        var progress = new Progress<TransientProgress>(UpdateProgress);
        try
        {
            var verified = await session.UseFeatureAsync<ITransientClient, DeviceSnapshot>((client, token) =>
                client.RunTransientAsync(request, () => Interlocked.Exchange(ref fireRequested, 0) == 1, progress, token), true);
            Readback = $"Fired · {verified.Settings.VoltageV:G} V / {verified.Settings.CurrentLimitA:G} A verified";
        }
        catch (OperationCanceledException) { Readback = "Output trigger stopped"; }
        catch { Readback = "Output trigger did not complete"; throw; }
        finally { active = false; ReadyToFire = false; Refresh(busy); }
    });

    [RelayCommand(CanExecute = nameof(CanAbort))]
    private Task AbortAsync() => active ? requestOff("Stop output trigger") : execute(async () =>
    {
        LoadLevels(await session.UseFeatureAsync<ITransientClient, TransientSettings>((client, token) => client.AbortTriggersAsync(token), true));
    });

    [RelayCommand(CanExecute = nameof(CanFire))]
    private void Fire() { Interlocked.Exchange(ref fireRequested, 1); ReadyToFire = false; }

    private void UpdateProgress(TransientProgress progress)
    {
        if (!active) return;
        ReadyToFire = progress.CanFire;
        Readback = progress.LastReadback is { } settings
            ? $"Fired {progress.Fired} · {settings.VoltageV:G} V / {settings.CurrentLimitA:G} A verified"
            : "Armed · ready to fire";
    }

    private void LoadLevels(TransientSettings levels)
    {
        Voltage = CaptureInput.Text(levels.Pending.VoltageV);
        Current = CaptureInput.Text(levels.Pending.CurrentLimitA);
        Readback = levels.WaitingForTrigger ? "The supply reports a waiting trigger" : "Pending levels read from the supply";
    }
}
