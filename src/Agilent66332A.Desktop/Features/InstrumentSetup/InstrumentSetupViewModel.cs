using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Agilent66332A.Application.Sessions;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Desktop.Features.InstrumentSetup;

public sealed partial class InstrumentSetupViewModel(SessionCoordinator session, Func<Func<Task>, Task> execute) : ObservableObject
{
    private bool busy;
    private long generation;
    [ObservableProperty] private InstrumentSetupSnapshot? observed;
    [ObservableProperty] private DigitalPortFunction portFunction;
    [ObservableProperty] private InhibitMode inhibit;
    [ObservableProperty] private bool enableFaultIndicator;
    [ObservableProperty] private FaultIndicatorSource faultSource;
    [ObservableProperty] private int outputMask;
    [ObservableProperty] private bool relayClosed;
    [ObservableProperty] private RelayPolarity polarity;
    [ObservableProperty] private bool displayEnabled = true;
    [ObservableProperty] private PanelDisplayMode displayMode;
    [ObservableProperty] private string displayText = "";
    [ObservableProperty] private bool wiringConfirmed;
    [ObservableProperty] private bool lockoutConfirmed;
    [ObservableProperty] private string result = "";

    public IReadOnlyList<DigitalPortFunction> PortFunctions { get; } = Enum.GetValues<DigitalPortFunction>();
    public IReadOnlyList<InhibitMode> InhibitModes { get; } = Enum.GetValues<InhibitMode>();
    public IReadOnlyList<FaultIndicatorSource> FaultSources { get; } = Enum.GetValues<FaultIndicatorSource>();
    public IReadOnlyList<int> OutputMasks { get; } = Enumerable.Range(0, 8).ToArray();
    public IReadOnlyList<RelayPolarity> RelayPolarities { get; } = Enum.GetValues<RelayPolarity>();
    public IReadOnlyList<PanelDisplayMode> DisplayModes { get; } = Enum.GetValues<PanelDisplayMode>();
    public string IdentityText => Observed is { } state
        ? $"{state.Identity.Manufacturer} {state.Identity.Model}  /  Firmware {state.Identity.Firmware}  /  SCPI {state.ScpiVersion}"
        : "Read the connected instrument to see its installed options and settings.";
    public string HardwareText => Observed is { } state
        ? $"Rear output mode {state.OutputBandwidth}   ·   Options {(state.Options.Count == 0 ? "None reported" : string.Join(", ", state.Options))}"
        : "";
    public string PortReadback => Observed?.DigitalReading is { } port
        ? $"Read value {port.RawValue}   ·   OUT 0 {(port.Output0 ? "high" : "low")}   ·   OUT 1 {(port.Output1 ? "high" : "low")}   ·   Pin 3 {(port.Pin3High ? "high" : "low")}"
        : Observed is { } state ? $"Readback {state.Port.Function}   ·   Inhibit {state.Port.Inhibit}   ·   Fault source {state.Port.FaultSource}" : "";
    public string RelayReadback => Observed?.Relay is { } relay
        ? $"Readback {(relay.Closed ? "Closed" : "Open")}   ·   {relay.Polarity} polarity"
        : Observed is null ? "" : "Option 760 relay is absent.";
    public string DisplayReadback => Observed?.Display is { } display
        ? $"Readback {(display.Enabled ? "On" : "Off")}   ·   {display.Mode} mode" : "";

    private bool CanRead() => !busy && !session.IsCompatibilityMode && session.CanUseFeature<IInstrumentSetup>(false);
    private bool Has(QualifiedFeature feature) => CanRead() && session.CanUseFeature<IInstrumentSetup>(true) &&
        session.Snapshot?.Capabilities.QualifiedFeatures.HasFlag(feature) == true;
    private bool CanPort() => Observed is not null && WiringConfirmed && Has(QualifiedFeature.DigitalIo);
    private bool CanWritePins() => CanPort() && Observed!.Port.Function == DigitalPortFunction.DigitalIo;
    private bool CanRelay() => Observed?.Relay is not null && WiringConfirmed && Has(QualifiedFeature.Relay);
    private bool CanDisplay() => Observed is not null && Has(QualifiedFeature.Service);
    private bool CanPanelControl(PanelControlMode mode) => Has(QualifiedFeature.Service) && (mode != PanelControlMode.RemoteLockout || LockoutConfirmed);

    [RelayCommand(CanExecute = nameof(CanRead))]
    private Task ReadAsync() => execute(async () => Load(await Use((client, token) => client.ReadInstrumentSetupAsync(token), false)));

    [RelayCommand(CanExecute = nameof(CanPort))]
    private Task ApplyPortAsync() => execute(async () =>
    {
        var requested = new DigitalPortSettings(PortFunction, Inhibit, EnableFaultIndicator, FaultSource);
        Load(await Use((client, token) => client.ConfigureDigitalPortAsync(requested, token), true));
        WiringConfirmed = false;
        Result = "Digital port settings verified.";
    });

    [RelayCommand(CanExecute = nameof(CanWritePins))]
    private Task WritePinsAsync() => execute(async () =>
    {
        int mask = OutputMask;
        var reading = await Use((client, token) => client.WriteDigitalPortAsync(mask, token), true);
        Observed = Observed! with { DigitalReading = reading };
        WiringConfirmed = false;
        Result = "Digital outputs 0 and 1 verified. Pin 3 shows the observed level.";
    });

    [RelayCommand(CanExecute = nameof(CanRelay))]
    private Task ApplyRelayAsync() => execute(async () =>
    {
        var requested = new RelaySettings(RelayClosed, Polarity);
        Load(await Use((client, token) => client.ConfigureRelayAsync(requested, token), true));
        WiringConfirmed = false;
        Result = "Relay state and polarity verified with the supply output off.";
    });

    [RelayCommand(CanExecute = nameof(CanRelay))]
    private Task EnableWithoutRelayAsync() => SwitchWithoutRelayAsync(true);

    [RelayCommand(CanExecute = nameof(CanRelay))]
    private Task DisableWithoutRelayAsync() => SwitchWithoutRelayAsync(false);

    private Task SwitchWithoutRelayAsync(bool enabled) => execute(async () =>
    {
        await Use((client, token) => client.SetOutputWithoutRelayAsync(enabled, token), true);
        WiringConfirmed = false;
        Result = enabled ? "Output enabled and relay state verified." : "Output disabled and relay state verified.";
    });

    [RelayCommand(CanExecute = nameof(CanDisplay))]
    private Task ApplyDisplayAsync() => execute(async () =>
    {
        var requested = new PanelDisplaySettings(DisplayEnabled, DisplayMode, DisplayText);
        Load(await Use((client, token) => client.ConfigurePanelDisplayAsync(requested, token), true));
        Result = "Panel display settings verified.";
    });

    [RelayCommand(CanExecute = nameof(CanPanelControl))]
    private Task SetPanelControlAsync(PanelControlMode mode) => execute(async () =>
    {
        await Use(async (client, token) => { await client.SetPanelControlAsync(mode, token); return true; }, true);
        LockoutConfirmed = false;
        Result = $"Instrument accepted the {mode} panel command.";
    });

    private Task<TResult> Use<TResult>(Func<IInstrumentSetup, CancellationToken, Task<TResult>> action, bool writes) =>
        session.UseFeatureAsync(action, writes);

    private void Load(InstrumentSetupSnapshot state)
    {
        Observed = state;
        PortFunction = state.Port.Function;
        Inhibit = state.Port.Inhibit;
        EnableFaultIndicator = state.Port.FaultIndicatorEnabled;
        FaultSource = state.Port.FaultSource;
        OutputMask = state.DigitalReading?.RawValue ?? 0;
        RelayClosed = state.Relay?.Closed == true;
        Polarity = state.Relay?.Polarity ?? RelayPolarity.Normal;
        DisplayEnabled = state.Display.Enabled;
        DisplayMode = state.Display.Mode;
        DisplayText = state.Display.Text;
    }

    public void Refresh(bool isBusy)
    {
        busy = isBusy;
        long connected = session.State == ConnectionState.Ready && !session.IsCompatibilityMode ? session.Snapshot?.Generation ?? 0 : 0;
        if (connected != generation)
        {
            generation = connected;
            Observed = null;
            WiringConfirmed = LockoutConfirmed = false;
            Result = "";
        }
        NotifyCommands();
    }

    partial void OnObservedChanged(InstrumentSetupSnapshot? value)
    {
        OnPropertyChanged(nameof(IdentityText)); OnPropertyChanged(nameof(HardwareText));
        OnPropertyChanged(nameof(PortReadback)); OnPropertyChanged(nameof(RelayReadback)); OnPropertyChanged(nameof(DisplayReadback));
        NotifyCommands();
    }

    partial void OnWiringConfirmedChanged(bool value) => NotifyCommands();
    partial void OnLockoutConfirmedChanged(bool value) => SetPanelControlCommand.NotifyCanExecuteChanged();
    private void NotifyCommands()
    {
        ReadCommand.NotifyCanExecuteChanged(); ApplyPortCommand.NotifyCanExecuteChanged(); WritePinsCommand.NotifyCanExecuteChanged();
        ApplyRelayCommand.NotifyCanExecuteChanged(); EnableWithoutRelayCommand.NotifyCanExecuteChanged(); DisableWithoutRelayCommand.NotifyCanExecuteChanged();
        ApplyDisplayCommand.NotifyCanExecuteChanged(); SetPanelControlCommand.NotifyCanExecuteChanged();
    }
}
