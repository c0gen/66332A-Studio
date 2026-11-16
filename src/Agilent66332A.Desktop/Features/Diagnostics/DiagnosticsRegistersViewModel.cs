using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Agilent66332A.Application.Sessions;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Desktop.Features.Diagnostics;

public sealed partial class DiagnosticsRegistersViewModel(SessionCoordinator session, Func<Func<Task>, Task> execute) : ObservableObject
{
    private static readonly (int Bit, string Caption)[] Operating =
        [(1, "Computing calibration"), (32, "Waiting for trigger"), (256, "CV"), (1024, "CC+"), (2048, "CC−")];
    private static readonly (int Bit, string Caption)[] Questionable =
        [(1, "OVP"), (2, "OCP"), (4, "Fuse"), (16, "Overtemperature"), (512, "Inhibit"), (1024, "Unregulated"), (16384, "Measurement overload")];
    private static readonly (int Bit, string Caption)[] Standard =
        [(1, "Complete"), (4, "Query error"), (8, "Device error"), (16, "Execution error"), (32, "Command error"), (128, "Power on")];
    private static readonly (int Bit, string Caption)[] StatusByte =
        [(8, "Questionable summary"), (16, "Message available"), (32, "Standard event summary"), (64, "Master status summary"), (128, "Operation summary")];

    private bool busy;
    private long generation;
    private StatusRegisterSnapshot? observed;
    [ObservableProperty] private string summary = "Read registers to inspect conditions, masks, and the status byte.";
    [ObservableProperty] private string lastAction = "";
    [ObservableProperty] private string operationEnable = "0";
    [ObservableProperty] private string operationPositive = "0";
    [ObservableProperty] private string operationNegative = "0";
    [ObservableProperty] private string questionableEnable = "0";
    [ObservableProperty] private string questionablePositive = "0";
    [ObservableProperty] private string questionableNegative = "0";
    [ObservableProperty] private string standardEnable = "0";
    [ObservableProperty] private string serviceEnable = "0";
    [ObservableProperty] private bool clearEnablesAtPowerOn = true;
    [ObservableProperty] private bool acknowledgeEffects;
    [ObservableProperty] private StatusEventRegister selectedRegister = StatusEventRegister.Questionable;
    public IReadOnlyList<StatusEventRegister> EventRegisters { get; } = Enum.GetValues<StatusEventRegister>();
    public ObservableCollection<string> ErrorEntries { get; } = [];

    private bool CanRead() => !busy && !session.IsCompatibilityMode && session.CanUseFeature<IInstrumentDiagnostics>(false);
    private bool CanAcknowledge() => CanRead() && AcknowledgeEffects && session.CanUseFeature<IInstrumentDiagnostics>(true) &&
        session.Snapshot?.Capabilities.QualifiedFeatures.HasFlag(QualifiedFeature.Diagnostics) == true;
    private bool CanApply() => observed is not null && CanAcknowledge();

    [RelayCommand(CanExecute = nameof(CanRead))]
    private Task ReadRegistersAsync() => execute(async () => Load(await Use((client, token) => client.ReadDiagnosticsAsync(token), false)));

    [RelayCommand(CanExecute = nameof(CanApply))]
    private Task ApplyMasksAsync() => execute(async () =>
    {
        var settings = new InstrumentStatusSettings(new(ReadMask(OperationEnable), ReadMask(OperationPositive), ReadMask(OperationNegative)),
            new(ReadMask(QuestionableEnable), ReadMask(QuestionablePositive), ReadMask(QuestionableNegative)),
            ReadMask(StandardEnable), ReadMask(ServiceEnable), ClearEnablesAtPowerOn);
        Load(await Use((client, token) => client.ConfigureStatusAsync(settings, token), true));
        AcknowledgeEffects = false;
        LastAction = "Masks and startup policy verified.";
    });

    [RelayCommand(CanExecute = nameof(CanAcknowledge))]
    private Task PresetRegistersAsync() => ChangeStatusAsync((client, token) => client.PresetStatusAsync(token), "Transition and enable masks restored to their preset values.");

    [RelayCommand(CanExecute = nameof(CanAcknowledge))]
    private Task ClearRegistersAsync() => ChangeStatusAsync((client, token) => client.ClearStatusAsync(token), "Device event registers and error queue cleared.");

    [RelayCommand(CanExecute = nameof(CanAcknowledge))]
    private Task AcknowledgeEventAsync() => execute(async () =>
    {
        var register = SelectedRegister;
        var read = await Use((client, token) => client.ReadAndAcknowledgeStatusEventAsync(register, token), true);
        var definitions = register switch { StatusEventRegister.Operation => Operating, StatusEventRegister.Questionable => Questionable, _ => Standard };
        LastAction = $"{read.ObservedUtc.LocalDateTime:HH:mm:ss}  {register} events acknowledged  {Describe(read.Value, definitions)}";
        AcknowledgeEffects = false;
    });

    [RelayCommand(CanExecute = nameof(CanRead))]
    private Task ReadErrorsAsync() => execute(async () =>
    {
        var batch = await Use((client, token) => client.DrainDeviceErrorsAsync(token), false);
        ErrorEntries.Clear();
        foreach (var error in batch.Errors) ErrorEntries.Add($"{error.ObservedUtc.LocalDateTime:HH:mm:ss}   {error.Code}  {error.Message}");
        LastAction = batch.Errors.Count == 0 ? "The instrument error queue is empty."
            : batch.LimitReached ? $"Read {batch.Errors.Count} errors. Read again to check for remaining entries."
            : $"Read and removed {batch.Errors.Count} device errors.";
    });

    private Task ChangeStatusAsync(Func<IInstrumentDiagnostics, CancellationToken, Task<StatusRegisterSnapshot>> action, string result) => execute(async () =>
    {
        Load(await Use(action, true));
        AcknowledgeEffects = false;
        LastAction = result;
    });

    private Task<TResult> Use<TResult>(Func<IInstrumentDiagnostics, CancellationToken, Task<TResult>> action, bool writes) =>
        session.UseFeatureAsync(action, writes);

    private void Load(StatusRegisterSnapshot state)
    {
        observed = state;
        Summary = $"{state.ObservedUtc.LocalDateTime:HH:mm:ss}  |  Operation {Describe(state.OperationCondition, Operating)}\n" +
            $"Questionable {Describe(state.QuestionableCondition, Questionable)}   |   Status byte {Describe(state.StatusByte, StatusByte)}";
        OperationEnable = Mask(state.Settings.Operation.Enable);
        OperationPositive = Mask(state.Settings.Operation.PositiveTransitions);
        OperationNegative = Mask(state.Settings.Operation.NegativeTransitions);
        QuestionableEnable = Mask(state.Settings.Questionable.Enable);
        QuestionablePositive = Mask(state.Settings.Questionable.PositiveTransitions);
        QuestionableNegative = Mask(state.Settings.Questionable.NegativeTransitions);
        StandardEnable = Mask(state.Settings.StandardEventEnable);
        ServiceEnable = Mask(state.Settings.ServiceRequestEnable);
        ClearEnablesAtPowerOn = state.Settings.ClearEnablesAtPowerOn;
        NotifyCommands();
    }

    public void Refresh(bool isBusy)
    {
        busy = isBusy;
        long connected = session.State == ConnectionState.Ready && !session.IsCompatibilityMode ? session.Snapshot?.Generation ?? 0 : 0;
        if (connected != generation)
        {
            generation = connected;
            observed = null;
            AcknowledgeEffects = false;
            Summary = "Read registers to inspect conditions, masks, and the status byte.";
        }
        NotifyCommands();
    }

    partial void OnAcknowledgeEffectsChanged(bool value) => NotifyCommands();

    private void NotifyCommands()
    {
        ReadRegistersCommand.NotifyCanExecuteChanged(); ApplyMasksCommand.NotifyCanExecuteChanged();
        PresetRegistersCommand.NotifyCanExecuteChanged(); ClearRegistersCommand.NotifyCanExecuteChanged();
        AcknowledgeEventCommand.NotifyCanExecuteChanged(); ReadErrorsCommand.NotifyCanExecuteChanged();
    }

    private static string Describe(int value, (int Bit, string Caption)[] definitions)
    {
        var parts = definitions.Where(entry => (entry.Bit & value) != 0).Select(entry => entry.Caption).ToList();
        int unknown = value & ~definitions.Aggregate(0, (mask, flag) => mask | flag.Bit);
        if (unknown != 0) parts.Add($"Unknown bits {Mask(unknown)}");
        return parts.Count == 0 ? Mask(value) : $"{Mask(value)} ({string.Join(", ", parts)})";
    }

    private static string Mask(int value) => $"0x{value:X}";

    private static int ReadMask(string input)
    {
        string value = input.Trim();
        bool hex = value.StartsWith("0x", StringComparison.OrdinalIgnoreCase);
        if (!int.TryParse(hex ? value[2..] : value, hex ? NumberStyles.AllowHexSpecifier : NumberStyles.None,
            CultureInfo.InvariantCulture, out int mask) || mask < 0)
            throw new ArgumentException("Enter status masks as decimal or 0x-prefixed hexadecimal values.");
        return mask;
    }
}
