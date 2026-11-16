using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Desktop.Features.Service;

public sealed partial class ServiceViewModel
{
    [ObservableProperty] private string compatibilityFaultMask = "0";
    [ObservableProperty] private bool compatibilityServiceRequests;
    [ObservableProperty] private bool compatibilityPowerOnRequest;
    [ObservableProperty] private bool compatibilityRelayAtPowerOn;
    public bool IsCompatibilityActive => session.IsCompatibilityMode;
    public string CompatibilityRelayEvidence => Observed?.CompatibilityRelayPresent switch
    {
        true => "Option 760 was confirmed in SCPI during this connection.",
        false => "Option 760 is absent.",
        _ => "Start in SCPI to confirm Option 760 before changing the legacy startup relay."
    };

    private bool CanUseIsolatedLegacy() => CanAcknowledgeLegacy() && FixtureIsolated;
    private bool CanWriteLegacyRelay() => CanUseIsolatedLegacy() && Observed?.CompatibilityRelayPresent == true &&
        IsQualified(QualifiedFeature.Relay);

    [RelayCommand(CanExecute = nameof(CanReadLegacy))]
    private Task ReadLegacyErrorAsync() => execute(async () =>
    {
        int code = await Use((client, token) => client.ReadCompatibilityErrorAsync(token), false);
        Result = code == 0 ? "The compatibility error register was clear." : $"Read and cleared compatibility error {code}.";
    });

    [RelayCommand(CanExecute = nameof(CanAcknowledgeLegacy))]
    private Task ApplyLegacyMaskAsync()
    {
        string draft = CompatibilityFaultMask;
        return LegacySettingAsync((client, token) =>
        {
            if (!int.TryParse(draft.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out int mask))
                throw new ArgumentException("Enter the Compatibility fault mask as a decimal number.");
            return client.SetCompatibilityFaultMaskAsync(mask, token);
        }, "Compatibility fault mask");
    }

    [RelayCommand(CanExecute = nameof(CanAcknowledgeLegacy))]
    private Task ApplyLegacyRequestsAsync()
    {
        bool enabled = CompatibilityServiceRequests;
        return LegacySettingAsync((client, token) => client.SetCompatibilityServiceRequestsAsync(enabled, token), "Compatibility service requests");
    }

    [RelayCommand(CanExecute = nameof(CanAcknowledgeLegacy))]
    private Task ApplyLegacyPowerOnRequestAsync()
    {
        bool enabled = CompatibilityPowerOnRequest;
        return LegacySettingAsync((client, token) => client.SetCompatibilityPowerOnRequestAsync(enabled, token), "Compatibility power-on request");
    }

    [RelayCommand(CanExecute = nameof(CanWriteLegacyRelay))]
    private Task ApplyLegacyStartupRelayAsync()
    {
        bool closed = CompatibilityRelayAtPowerOn;
        bool isolated = FixtureIsolated;
        return LegacySettingAsync((client, token) => client.SetCompatibilityRelayAtPowerOnAsync(closed, isolated, token), "Compatibility startup relay");
    }

    [RelayCommand(CanExecute = nameof(CanUseIsolatedLegacy))]
    private Task SelfTestLegacyAsync() => execute(async () =>
    {
        bool isolated = FixtureIsolated;
        var test = await Use((client, token) => client.RunCompatibilitySelfTestAsync(isolated, token), true);
        Result = test.Passed ? "Compatibility self-test passed. Off was requested. Check the output at the instrument."
            : $"Compatibility self-test returned {test.TestCode} with error {test.ErrorCode}. Check the output at the instrument.";
        FixtureIsolated = DestructiveActionConfirmed = false;
    });

    [RelayCommand(CanExecute = nameof(CanUseIsolatedLegacy))]
    private Task ClearLegacyAsync() => execute(async () =>
    {
        bool isolated = FixtureIsolated;
        var clear = await Use((client, token) => client.ClearCompatibilityAsync(isolated, token), true);
        Result = clear.Accepted ? "Compatibility clear accepted. Off was requested. Check the output at the instrument."
            : $"Compatibility clear reported error {clear.ErrorCode}. Check the output at the instrument.";
        FixtureIsolated = DestructiveActionConfirmed = false;
    });

    private Task LegacySettingAsync(Func<IInstrumentService, CancellationToken, Task<CompatibilityCommandResult>> action, string label) => execute(async () =>
    {
        var result = await Use(action, true);
        Result = result.Accepted ? $"{label} accepted. The device provides no readback for this setting."
            : !result.CommandSent ? $"Prior error {result.ErrorCode} was recorded. Review it before retrying."
            : result.ErrorCode == 2 ? "The instrument accepts one Compatibility power-on request per mains power cycle."
            : $"{label} reported error {result.ErrorCode}.";
        DestructiveActionConfirmed = false;
    });

    private void NotifyCompatibilityActions()
    {
        OnPropertyChanged(nameof(IsCompatibilityActive)); OnPropertyChanged(nameof(CompatibilityRelayEvidence));
        ReadLegacyErrorCommand.NotifyCanExecuteChanged(); ApplyLegacyMaskCommand.NotifyCanExecuteChanged();
        ApplyLegacyRequestsCommand.NotifyCanExecuteChanged(); ApplyLegacyPowerOnRequestCommand.NotifyCanExecuteChanged();
        ApplyLegacyStartupRelayCommand.NotifyCanExecuteChanged(); SelfTestLegacyCommand.NotifyCanExecuteChanged(); ClearLegacyCommand.NotifyCanExecuteChanged();
    }
}
