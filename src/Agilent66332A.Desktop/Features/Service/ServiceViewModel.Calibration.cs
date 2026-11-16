using System.Globalization;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Desktop.Features.Service;

public sealed partial class ServiceViewModel
{
    private readonly Dispatcher dispatcher = System.Windows.Application.Current.Dispatcher;
    private TaskCompletionSource<CalibrationAnswer>? promptCompletion;
    [ObservableProperty] private bool calibrationWiringConfirmed;
    [ObservableProperty] private CalibrationChoice? selectedProcedure;
    [ObservableProperty] private CalibrationPrompt? pendingPrompt;
    [ObservableProperty] private string referenceReading = "";
    [ObservableProperty] private string promptMessage = "";
    public IReadOnlyList<CalibrationChoice> CalibrationProcedures { get; } =
    [
        new(CalibrationProcedure.Voltage, "Voltage and voltage readback"), new(CalibrationProcedure.Overvoltage, "Overvoltage protection"),
        new(CalibrationProcedure.PositiveCurrent, "Positive current and high range"), new(CalibrationProcedure.NegativeCurrent, "Negative current"),
        new(CalibrationProcedure.LowRangeCurrent, "Low current range"), new(CalibrationProcedure.AcCurrent, "AC current detector")
    ];
    public bool CanStartCalibration => SelectedProcedure is not null && CanChangePasscode;
    public bool CanChangePasscode => CalibrationWiringConfirmed && !session.IsCompatibilityMode && IsQualified(QualifiedFeature.Calibration);
    public bool IsPromptVisible => PendingPrompt is not null;
    public bool IsReferencePrompt => PendingPrompt?.Kind == CalibrationPromptKind.ReferenceReading;
    public string ProceedLabel => PendingPrompt?.Kind switch
    {
        CalibrationPromptKind.Save => "Save constants", CalibrationPromptKind.ReferenceReading => "Enter reference", _ => "Continue"
    };
    public string CancelLabel => PendingPrompt?.Kind == CalibrationPromptKind.Save ? "Discard constants" : "Cancel calibration";

    public Task StartCalibrationAsync(string password) => execute(async () =>
    {
        CalibrationProcedure procedure = SelectedProcedure?.Procedure ?? throw new InvalidOperationException("Select a calibration procedure.");
        decimal? current = ParsePasscode(password, required: false);
        CalibrationResult result = await Use((client, token) => client.RunCalibrationAsync(procedure, current, PromptAsync, token), true);
        Result = result.Saved ? "Calibration constants saved. Output off and calibration mode exited."
            : "Calibration changes discarded. Output off and calibration mode exited.";
        Observed = null;
        CalibrationWiringConfirmed = false;
        ClearStartupReview();
    });

    public Task ChangePasscodeAsync(string currentPassword, string newPassword) => execute(async () =>
    {
        decimal? current = ParsePasscode(currentPassword, required: false);
        decimal updated = ParsePasscode(newPassword, required: true)!.Value;
        await Use(async (client, token) => { await client.ChangeCalibrationPasscodeAsync(current, updated, token); return true; }, true);
        CalibrationWiringConfirmed = false;
        Result = "Instrument accepted the passcode change and exited calibration mode.";
    });

    private async Task<CalibrationAnswer> PromptAsync(CalibrationPrompt prompt, CancellationToken token)
    {
        var completion = new TaskCompletionSource<CalibrationAnswer>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = token.Register(() => completion.TrySetCanceled(token));
        await dispatcher.InvokeAsync(() =>
        {
            if (token.IsCancellationRequested) return;
            promptCompletion = completion;
            ReferenceReading = PromptMessage = "";
            PendingPrompt = prompt;
        });
        try { return await completion.Task.ConfigureAwait(false); }
        finally
        {
            await dispatcher.InvokeAsync(() =>
            {
                if (promptCompletion != completion) return;
                promptCompletion = null;
                PendingPrompt = null;
            });
        }
    }

    [RelayCommand]
    private void ProceedCalibration()
    {
        if (PendingPrompt is null) return;
        if (!IsReferencePrompt)
        {
            promptCompletion?.TrySetResult(new(true));
            return;
        }
        if ((!double.TryParse(ReferenceReading, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
            !double.TryParse(ReferenceReading, NumberStyles.Float, CultureInfo.InvariantCulture, out value)) || !double.IsFinite(value))
        {
            PromptMessage = "Enter a finite reference reading using the displayed unit.";
            return;
        }
        promptCompletion?.TrySetResult(new(true, value));
    }

    [RelayCommand]
    private void CancelCalibration() => promptCompletion?.TrySetResult(new(false));

    partial void OnSelectedProcedureChanged(CalibrationChoice? value) => NotifyCalibrationActions();
    partial void OnCalibrationWiringConfirmedChanged(bool value) => NotifyCalibrationActions();

    partial void OnPendingPromptChanged(CalibrationPrompt? value)
    {
        OnPropertyChanged(nameof(IsPromptVisible)); OnPropertyChanged(nameof(IsReferencePrompt));
        OnPropertyChanged(nameof(ProceedLabel)); OnPropertyChanged(nameof(CancelLabel));
    }

    private void NotifyCalibrationActions()
    {
        OnPropertyChanged(nameof(CanStartCalibration));
        OnPropertyChanged(nameof(CanChangePasscode));
    }

    private static decimal? ParsePasscode(string input, bool required)
    {
        if (!required && string.IsNullOrWhiteSpace(input)) return null;
        if (!decimal.TryParse(input, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            throw new ArgumentException("Enter a numeric calibration passcode using a period for the decimal point.");
        return value;
    }

    public sealed record CalibrationChoice(CalibrationProcedure Procedure, string Name);
}
