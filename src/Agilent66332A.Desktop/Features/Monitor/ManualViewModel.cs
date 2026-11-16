using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Agilent66332A.Application.Sessions;
using Agilent66332A.Core.Models;
using Agilent66332A.Core.Policies;
using Agilent66332A.Desktop.Controls;
using Agilent66332A.Storage.Preferences;

namespace Agilent66332A.Desktop.Features.Monitor;

public sealed partial class ManualViewModel : ObservableObject
{
    private readonly SessionCoordinator session;
    private readonly PreferenceRepository repository;
    private readonly Func<Func<Task>, Task> execute;
    private bool busy;
    [ObservableProperty] private string voltage = "0";
    [ObservableProperty] private string current = "0.01";
    [ObservableProperty] private string ovp = "22";
    [ObservableProperty] private string ocpDelay = "0.08";
    [ObservableProperty] private bool ocpEnabled;
    [ObservableProperty] private string presetName = "Bench setup";
    [ObservableProperty] private SettingsPreset? selectedPreset;
    public ObservableCollection<SettingsPreset> Presets { get; } = [];
    public ManualViewModel(SessionCoordinator session, PreferenceRepository repository, Func<Func<Task>, Task> execute)
    { this.session = session; this.repository = repository; this.execute = execute; }
    public RequestedSettings Draft => new(NumericUnitEditor.Parse(Voltage), NumericUnitEditor.Parse(Current));
    public ProtectionSettings ProtectionDraft => new(NumericUnitEditor.Parse(Ovp), OcpEnabled, NumericUnitEditor.Parse(OcpDelay));
    public string Validation
    {
        get
        {
            try { if (session.Snapshot is { } state) SettingsPolicy.Validate(Draft, state); else return "Connect to inspect settings and bounds."; return ""; }
            catch (ArgumentException e) { return e.Message; }
        }
    }
    public bool IsDirty
    {
        get
        {
            try { return session.Snapshot is { } s && (Draft.VoltageV != s.Settings.VoltageV || Draft.CurrentLimitA != s.Settings.CurrentLimitA); }
            catch { return true; }
        }
    }
    public bool CanEdit => !busy && !session.IsRunning;
    public bool ProtectionDirty
    {
        get { try { return session.Snapshot is { } s && ProtectionDraft != s.Protections; } catch { return true; } }
    }
    public string DraftStatus => session.Snapshot is null ? ""
        : Validation.Length > 0 ? Validation
        : IsDirty ? "Pending changes" : "";
    public string Bounds => session.Snapshot is { } s
        ? $"Range {s.Capabilities.Voltage.Minimum:G}–{s.Capabilities.Voltage.Maximum:G} V / {s.Capabilities.Current.Minimum:G}–{s.Capabilities.Current.Maximum:G} A"
        : "";
    public string ApplyLabel => session.Snapshot?.Output == OutputState.Off ? "Apply & turn on" : "Apply changes";
    public string OutputOnToolTip => session.Snapshot switch
    {
        _ when !session.CanConfigure => "See Connection settings for the verification status.",
        _ when IsDirty => "Apply or discard edits.",
        _ when ProtectionDirty => "Apply or discard protection edits.",
        { Status.HasFault: true } => "Clear the protection fault first.",
        _ => "Enable output at the verified setpoints."
    };
    private bool CanApply() => CanWrite() && !session.IsShuttingDown && Validation.Length == 0 && session.Snapshot switch
    {
        { Output: OutputState.Off, Status.HasFault: false } => !ProtectionDirty,
        { Output: OutputState.On } => IsDirty,
        _ => false
    };
    private bool CanOn() => !busy && session.CanConfigure && !session.IsShuttingDown && !IsDirty && !ProtectionDirty &&
        session.Snapshot is { Output: OutputState.Off, Status.HasFault: false };
    private bool CanWrite() => !busy && session.CanConfigure;
    [RelayCommand(CanExecute = nameof(CanApply))]
    private Task ApplyAsync() => execute(async () =>
    {
        if (session.Snapshot?.Output == OutputState.Off)
            await session.ApplyAndEnableAsync(Draft);
        else
            await session.ApplyAsync(Draft);
        Restore();
    });
    [RelayCommand(CanExecute = nameof(CanOn))] private Task OutputOnAsync() => execute(() => session.OutputOnAsync());
    [RelayCommand] private void Restore()
    {
        if (session.Snapshot is not { } s) return;
        Voltage = s.Settings.VoltageV.ToString("G", CultureInfo.CurrentCulture);
        Current = s.Settings.CurrentLimitA.ToString("G", CultureInfo.CurrentCulture);
        Ovp = s.Protections.OvpV.ToString("G", CultureInfo.CurrentCulture);
        OcpDelay = s.Protections.OcpDelaySeconds.ToString("G", CultureInfo.CurrentCulture);
        OcpEnabled = s.Protections.OcpEnabled;
        Refresh(busy);
    }
    [RelayCommand(CanExecute = nameof(CanWrite))] private Task ApplyProtectionsAsync() => execute(async () => { await session.ProtectionsAsync(ProtectionDraft); Restore(); });
    [RelayCommand(CanExecute = nameof(CanWrite))] private Task ClearFaultAsync() => execute(() => session.ClearFaultAsync());
    [RelayCommand] private Task SavePresetAsync() => execute(async () =>
    {
        if (session.Snapshot is not { } state) throw new InvalidOperationException("Connect before creating a device-bound preset.");
        SettingsPolicy.Validate(Draft, state); SettingsPolicy.ValidateProtections(ProtectionDraft, state);
        await repository.SavePresetAsync(new(1, "66332A", PresetName, Draft, ProtectionDraft));
        await ReloadPresetsAsync();
    });
    [RelayCommand] private void LoadPreset()
    {
        if (SelectedPreset is not { } p) return;
        Voltage = p.Settings.VoltageV.ToString("G", CultureInfo.CurrentCulture); Current = p.Settings.CurrentLimitA.ToString("G", CultureInfo.CurrentCulture);
        Ovp = p.Protections.OvpV.ToString("G", CultureInfo.CurrentCulture); OcpEnabled = p.Protections.OcpEnabled; OcpDelay = p.Protections.OcpDelaySeconds.ToString("G", CultureInfo.CurrentCulture);
        Refresh(busy);
    }
    public async Task ReloadPresetsAsync()
    {
        var presets = await repository.PresetsAsync();
        Presets.Clear(); foreach (var p in presets) Presets.Add(p);
    }
    partial void OnVoltageChanged(string value) => Refresh(busy);
    partial void OnCurrentChanged(string value) => Refresh(busy);
    partial void OnOvpChanged(string value) => Refresh(busy);
    partial void OnOcpDelayChanged(string value) => Refresh(busy);
    partial void OnOcpEnabledChanged(bool value) => Refresh(busy);
    public void Refresh(bool isBusy)
    {
        busy = isBusy;
        OnPropertyChanged(nameof(ApplyLabel));
        OnPropertyChanged(nameof(IsDirty)); OnPropertyChanged(nameof(ProtectionDirty)); OnPropertyChanged(nameof(CanEdit)); OnPropertyChanged(nameof(Validation)); OnPropertyChanged(nameof(DraftStatus)); OnPropertyChanged(nameof(Bounds)); OnPropertyChanged(nameof(OutputOnToolTip));
        ApplyCommand.NotifyCanExecuteChanged(); OutputOnCommand.NotifyCanExecuteChanged(); ApplyProtectionsCommand.NotifyCanExecuteChanged(); ClearFaultCommand.NotifyCanExecuteChanged();
    }
}
