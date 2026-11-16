using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Agilent66332A.Application.Sessions;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Desktop.Features.Service;

public sealed partial class ServiceViewModel(SessionCoordinator session, Func<Func<Task>, Task> execute) : ObservableObject
{
    private bool busy;
    private long generation;
    private DeviceSnapshot? reviewedStartup;
    [ObservableProperty] private InstrumentServiceSnapshot? observed;
    [ObservableProperty] private int selectedSlot;
    [ObservableProperty] private PowerOnMode selectedPowerOn;
    [ObservableProperty] private bool fixtureIsolated;
    [ObservableProperty] private bool destructiveActionConfirmed;
    [ObservableProperty] private string startupReview = "";
    [ObservableProperty] private string result = "";
    [ObservableProperty] private string legacyReading = "";

    public IReadOnlyList<int> MemorySlots { get; } = [0, 1, 2, 3];
    public IReadOnlyList<PowerOnMode> PowerOnModes { get; } = Enum.GetValues<PowerOnMode>();
    public string ServiceStatus => session.State switch
    {
        not ConnectionState.Ready => "Connect to inspect instrument service settings.",
        _ when session.IsCompatibilityMode => "Compatibility is active. The device has no verified output-enable readback in this mode.",
        _ when Observed is { } status => $"SCPI   ·   Power-on policy {PolicyName(status.PowerOn)}   ·   Calibration mode {(status.CalibrationEnabled == true ? "on" : "off")}",
        _ => "Read service settings to see the instrument’s current power-on policy and calibration state."
    };
    private bool CanRead() => !busy && session.CanUseFeature<IInstrumentService>(false);
    private bool IsQualified(QualifiedFeature feature) => CanRead() && session.CanUseFeature<IInstrumentService>(true) &&
        session.Snapshot?.Capabilities.QualifiedFeatures.HasFlag(feature) == true;
    private bool CanService() => !session.IsCompatibilityMode && IsQualified(QualifiedFeature.Service);
    private bool CanChangeService() => CanService() && DestructiveActionConfirmed;
    private bool CanRecall() => CanChangeService() && FixtureIsolated;
    private bool CanSetStartup() => CanChangeService() && (SelectedPowerOn == PowerOnMode.Reset || reviewedStartup is not null);
    private bool CanSwitchLanguage(ProgrammingLanguage target) => FixtureIsolated && DestructiveActionConfirmed &&
        IsQualified(QualifiedFeature.Compatibility) && (target == ProgrammingLanguage.Compatibility) != session.IsCompatibilityMode;
    private bool CanReadLegacy() => CanRead() && session.IsCompatibilityMode;
    private bool CanAcknowledgeLegacy() => session.IsCompatibilityMode && DestructiveActionConfirmed && IsQualified(QualifiedFeature.Compatibility);

    [RelayCommand(CanExecute = nameof(CanRead))]
    private Task ReadAsync() => execute(async () => Load(await Use((client, token) => client.ReadServiceAsync(token), false)));

    [RelayCommand(CanExecute = nameof(CanChangeService))]
    private Task SelfTestAsync() => execute(async () =>
    {
        var test = await Use((client, token) => client.RunSelfTestAsync(token), true);
        string details = string.Join(", ", test.Errors.Errors.Select(error => $"{error.Code} {error.Message}"));
        Result = test.Passed ? "Instrument self-test passed. Output verified off."
            : $"Instrument self-test returned {test.Code}. {details}".TrimEnd();
        DestructiveActionConfirmed = false;
    });

    [RelayCommand(CanExecute = nameof(CanChangeService))]
    private Task ResetInstrumentAsync() => execute(async () =>
    {
        await Use((client, token) => client.ResetInstrumentAsync(token), true);
        Observed = null;
        Result = "Instrument reset. Output verified off.";
        DestructiveActionConfirmed = false;
        ClearStartupReview();
    });

    [RelayCommand(CanExecute = nameof(CanChangeService))]
    private Task SaveSlotAsync() => execute(async () =>
    {
        int slot = SelectedSlot;
        await Use(async (client, token) => { await client.SaveDeviceStateAsync(slot, token); return true; }, true);
        Result = $"Instrument accepted Save to slot {slot} with output verified off.";
        DestructiveActionConfirmed = false;
    });

    [RelayCommand(CanExecute = nameof(CanRecall))]
    private Task RecallSlotAsync() => execute(async () =>
    {
        int slot = SelectedSlot;
        bool isolated = FixtureIsolated;
        await Use((client, token) => client.RecallDeviceStateAsync(slot, isolated, token), true);
        Observed = null;
        Result = $"Recalled slot {slot} and verified output off.";
        FixtureIsolated = DestructiveActionConfirmed = false;
        ClearStartupReview();
    });

    [RelayCommand(CanExecute = nameof(CanService))]
    private void ReviewStartup()
    {
        reviewedStartup = session.Snapshot;
        if (reviewedStartup is { } state)
            StartupReview = FormattableString.Invariant($"Slot 0 would store {state.Settings.VoltageV:G6} V, {state.Settings.CurrentLimitA:G6} A, OVP {state.Protections.OvpV:G6} V, OCP {(state.Protections.OcpEnabled ? "on" : "off")}, and all other live settings, with output off.");
        SetStartupCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanSetStartup))]
    private Task SetStartupAsync() => execute(async () =>
    {
        var policy = SelectedPowerOn;
        var review = reviewedStartup;
        Load(await Use((client, token) => client.ConfigurePowerOnAsync(policy, review, token), true));
        Result = policy == PowerOnMode.Reset ? "Reset at power on verified." : "Stored a verified-off state in slot 0 and selected it for power on.";
        ClearStartupReview();
        DestructiveActionConfirmed = false;
    });

    [RelayCommand(CanExecute = nameof(CanSwitchLanguage))]
    private Task SwitchLanguageAsync(ProgrammingLanguage target) => execute(async () =>
    {
        bool isolated = FixtureIsolated;
        Load(await Use((client, token) => client.SetProgrammingLanguageAsync(target, isolated, token), true));
        LegacyReading = "";
        Result = target == ProgrammingLanguage.Scpi ? "SCPI restored. Output verified off."
            : "Compatibility selected. The app sent Output Off. Check the output at the instrument.";
        FixtureIsolated = DestructiveActionConfirmed = false;
        ClearStartupReview();
    });

    [RelayCommand(CanExecute = nameof(CanReadLegacy))]
    private Task ReadLegacyAsync() => execute(async () =>
    {
        var reading = await Use((client, token) => client.ReadCompatibilityAsync(token), false);
        LegacyReading = FormattableString.Invariant($"{reading.ObservedUtc.LocalDateTime:HH:mm:ss}   {reading.VoltageV:G6} V   {reading.CurrentA:G6} A   Status 0x{reading.StatusRegister:X}");
    });

    [RelayCommand(CanExecute = nameof(CanAcknowledgeLegacy))]
    private Task AcknowledgeLegacyAsync() => execute(async () =>
    {
        var events = await Use((client, token) => client.AcknowledgeCompatibilityEventsAsync(token), true);
        Result = $"Acknowledged compatibility events 0x{events.AccumulatedStatus:X}, faults 0x{events.Fault:X}, error {events.ErrorCode}.";
        DestructiveActionConfirmed = false;
    });

    private Task<TResult> Use<TResult>(Func<IInstrumentService, CancellationToken, Task<TResult>> action, bool writes) =>
        session.UseFeatureAsync(action, writes);

    private void Load(InstrumentServiceSnapshot state)
    {
        Observed = state;
        if (state.PowerOn is { } mode) SelectedPowerOn = mode;
    }

    public void Refresh(bool isBusy)
    {
        busy = isBusy;
        long connected = session.State == ConnectionState.Ready ? session.Snapshot?.Generation ?? 0 : 0;
        if (generation != connected)
        {
            generation = connected;
            Observed = null;
            FixtureIsolated = DestructiveActionConfirmed = CalibrationWiringConfirmed = false;
            Result = LegacyReading = "";
            ClearStartupReview();
        }
        OnPropertyChanged(nameof(ServiceStatus));
        NotifyCommands();
    }

    partial void OnObservedChanged(InstrumentServiceSnapshot? value)
    {
        OnPropertyChanged(nameof(ServiceStatus));
        NotifyCompatibilityActions();
    }
    partial void OnSelectedPowerOnChanged(PowerOnMode value) => ClearStartupReview();
    partial void OnFixtureIsolatedChanged(bool value) => NotifyCommands();
    partial void OnDestructiveActionConfirmedChanged(bool value) => NotifyCommands();

    private void ClearStartupReview()
    {
        reviewedStartup = null;
        StartupReview = "";
        SetStartupCommand.NotifyCanExecuteChanged();
    }

    private void NotifyCommands()
    {
        ReadCommand.NotifyCanExecuteChanged(); SelfTestCommand.NotifyCanExecuteChanged(); ResetInstrumentCommand.NotifyCanExecuteChanged();
        SaveSlotCommand.NotifyCanExecuteChanged(); RecallSlotCommand.NotifyCanExecuteChanged(); ReviewStartupCommand.NotifyCanExecuteChanged();
        SetStartupCommand.NotifyCanExecuteChanged(); SwitchLanguageCommand.NotifyCanExecuteChanged(); ReadLegacyCommand.NotifyCanExecuteChanged();
        AcknowledgeLegacyCommand.NotifyCanExecuteChanged(); NotifyCompatibilityActions(); NotifyCalibrationActions();
    }

    private static string PolicyName(PowerOnMode? policy) => policy == PowerOnMode.RecallSlotZero ? "Recall slot 0" : "Reset";
}
