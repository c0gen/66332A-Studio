using System.IO;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Agilent66332A.Core.Models;
using Agilent66332A.Desktop.Features.Connection;
using Agilent66332A.Instruments.Devices;
using Agilent66332A.Instruments.Serial;
using Agilent66332A.Storage.Preferences;

namespace Agilent66332A.Desktop.Shell;

public sealed partial class ShellViewModel
{
    private static readonly StringComparer PortComparer = StringComparer.OrdinalIgnoreCase;
    private readonly DispatcherTimer portTimer;
    private string preferredPort = new ConnectionProfile().PortName;
    private bool hasSelectedPort;

    public IReadOnlyList<int> BaudRates => ConnectionProfile.SupportedBaudRates;
    public IReadOnlyList<SerialParity> Parities { get; } = Enum.GetValues<SerialParity>();
    public KeyValuePair<string, SerialFlowControl>[] FlowControls { get; } =
    [
        new("None", SerialFlowControl.None), new("XON/XOFF", SerialFlowControl.XonXoff),
        new("RTS/CTS", SerialFlowControl.RtsCts), new("DTR/DSR", SerialFlowControl.DtrDsr)
    ];

    [ObservableProperty] private IReadOnlyList<SerialPortOption> ports = [];
    [ObservableProperty] private SerialPortOption? selectedPort;
    [ObservableProperty] private string portDiscoveryStatus = "Looking for serial ports…";
    [ObservableProperty] private int baudRate = 9600;
    [ObservableProperty] private SerialParity parity;
    [ObservableProperty] private SerialFlowControl flowControl;
    [ObservableProperty] private bool dtrEnabled = true;
    [ObservableProperty] private bool rtsEnabled = true;
    [ObservableProperty] private string cableId = "";
    [ObservableProperty] private string chassisLabel = "";

    private ConnectionProfile SelectedProfile => new(SelectedPort?.PortName ?? preferredPort, BaudRate, DtrEnabled, RtsEnabled,
        Parity, FlowControl, NormalizeLabel(CableId), SelectedPort?.Adapter, NormalizeLabel(ChassisLabel));
    private ConnectionProfile PreferenceProfile => SelectedProfile with { Adapter = null, CableId = null, ChassisLabel = null };
    private static string? NormalizeLabel(string label) => string.IsNullOrWhiteSpace(label) ? null : label.Trim();
    public string ProfileLabel => Session.State is ConnectionState.Connecting or ConnectionState.Ready
        ? Session.Profile.ToString()
        : SelectedPort is null ? $"No serial port selected / {BaudRate} baud" : SelectedProfile.ToString();
    public bool ManualDtr => FlowControl != SerialFlowControl.DtrDsr;
    public bool ManualRts => FlowControl != SerialFlowControl.RtsCts;
    public string QualificationPath => preferences.HardwareQualificationPath;
    public string AdapterDetails => (Session.State == ConnectionState.Ready && Session.SourceKind == "Hardware"
        ? Session.Profile.Adapter : SelectedPort?.Adapter) is { } adapter
            ? $"{adapter.InstanceId}\nDriver key {adapter.DriverKey}\n{adapter.DriverInf} · version {adapter.DriverVersion}"
            : "Windows adapter and driver identity unavailable.";
    public string InstrumentIdentityDetails => Session.State == ConnectionState.Ready && Session.SourceKind == "Hardware" && Session.Snapshot is { } snapshot
        ? snapshot.Identity.Raw : "Connect to read the instrument serial number and firmware.";

    private void InitializeConnectionPreferences(ConnectionProfile profile)
    {
        preferredPort = profile.PortName;
        BaudRate = profile.BaudRate;
        Parity = profile.Parity;
        FlowControl = profile.FlowControl;
        DtrEnabled = profile.DtrEnabled;
        RtsEnabled = profile.RtsEnabled;
    }

    private void RefreshConnectionDetails()
    {
        OnPropertyChanged(nameof(AdapterDetails));
        OnPropertyChanged(nameof(InstrumentIdentityDetails));
    }

    private bool CanConnectHardware() => CanConnect && SelectedPort is not null;

    [RelayCommand(CanExecute = nameof(CanConnectHardware))]
    private Task ConnectHardwareAsync() => ExecuteAsync(async () =>
    {
        var requested = SelectedProfile;
        // Require fresh labels when matching a saved bench qualification.
        CableId = "";
        ChassisLabel = "";
        var (evidence, warning) = await HardwareQualificationFile.LoadAsync(QualificationPath);
        var available = await Task.Run(WindowsSerialPortDiscovery.GetPorts);
        var current = available.FirstOrDefault(port => PortComparer.Equals(port.PortName, requested.PortName));
        if (current is null || requested.Adapter is { } selected && !selected.Matches(current.Adapter))
            throw new IOException("The selected serial adapter changed. Refresh ports and select it again.");
        var profile = requested with { Adapter = current.Adapter };
        simulator = null;
        connectedGeneration = 0;
        lastSample = null;
        previousSampleSeconds = 0;
        var qualification = evidence is null ? null : new CurrentAdapterQualification(evidence);
        var client = new AgilentClient(new SerialPortTransport(), clock, qualification: qualification,
            verifyBasicControlOnConnect: true);
        await Session.ConnectAsync(client, profile);
        if (client.LiveControlStatus is { } liveStatus)
        {
            Message = liveStatus;
            return;
        }
        Message = Session.Snapshot switch
        {
            { Capabilities.HardwareValidated: true } => $"Bench qualification matched on {profile.PortName}.",
            _ when Session.IsCompatibilityMode => $"Monitoring on {profile.PortName}. Live verification requires SCPI mode.",
            _ => warning ?? $"Monitoring on {profile.PortName}."
        };
    });

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task RefreshPortsAsync()
    {
        try
        {
            var available = await Task.Run(WindowsSerialPortDiscovery.GetPorts);
            if (!CanConnect) return;

            if (!Ports.SequenceEqual(available))
            {
                var selection = available.FirstOrDefault(port => PortComparer.Equals(port.PortName, preferredPort));
                if (selection is null && !hasSelectedPort && available.Length == 1) selection = available[0];
                SelectedPort = null;
                Ports = available;
                SelectedPort = selection;
            }

            PortDiscoveryStatus = available.Length == 0 ? "No serial ports detected" : "";
        }
        catch (Exception e)
        {
            PortDiscoveryStatus = $"Port discovery failed: {e.Message}";
        }
    }

    private void PollPorts()
    {
        if (RefreshPortsCommand.CanExecute(null)) RefreshPortsCommand.Execute(null);
    }

    partial void OnSelectedPortChanged(SerialPortOption? value)
    {
        if (value is not null)
        {
            preferredPort = value.PortName;
            hasSelectedPort = true;
        }
        OnPropertyChanged(nameof(ProfileLabel));
        RefreshConnectionDetails();
        ConnectHardwareCommand.NotifyCanExecuteChanged();
    }

    partial void OnBaudRateChanged(int value) => OnPropertyChanged(nameof(ProfileLabel));
    partial void OnParityChanged(SerialParity value) => OnPropertyChanged(nameof(ProfileLabel));
    partial void OnDtrEnabledChanged(bool value) => OnPropertyChanged(nameof(ProfileLabel));
    partial void OnRtsEnabledChanged(bool value) => OnPropertyChanged(nameof(ProfileLabel));
    partial void OnFlowControlChanged(SerialFlowControl value)
    {
        OnPropertyChanged(nameof(ManualDtr));
        OnPropertyChanged(nameof(ManualRts));
        OnPropertyChanged(nameof(ProfileLabel));
    }
}
