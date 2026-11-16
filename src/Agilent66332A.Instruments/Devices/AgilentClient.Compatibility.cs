using System.Globalization;
using Agilent66332A.Core.Models;
using Agilent66332A.Instruments.Scpi;

namespace Agilent66332A.Instruments.Devices;

public partial class AgilentClient
{
    private (long Generation, bool Present)? compatibilityRelayEvidence;

    private bool? CompatibilityRelayPresent => compatibilityRelayEvidence is { } option && option.Generation == snapshot?.Generation ? option.Present : null;

    public Task<int> ReadCompatibilityErrorAsync(CancellationToken token = default) => Queue(t =>
    {
        EnsureCompatibility();
        return Guard(() => ReadLegacyError(t));
    }, token);

    public Task<CompatibilityCommandResult> SetCompatibilityFaultMaskAsync(int mask, CancellationToken token = default)
    {
        if ((mask & ~0xFDF) != 0) throw new ArgumentOutOfRangeException(nameof(mask), "Use a 12-bit Compatibility mask with reserved bit 5 clear.");
        return RunCompatibilityCommandAsync("UNMASK " + mask.ToString(CultureInfo.InvariantCulture), token);
    }

    public Task<CompatibilityCommandResult> SetCompatibilityServiceRequestsAsync(bool enabled, CancellationToken token = default) =>
        RunCompatibilityCommandAsync("SRQ " + BooleanHeader(enabled), token);

    public Task<CompatibilityCommandResult> SetCompatibilityPowerOnRequestAsync(bool enabled, CancellationToken token = default) =>
        RunCompatibilityCommandAsync("PON " + BooleanHeader(enabled), token);

    public Task<CompatibilityCommandResult> SetCompatibilityRelayAtPowerOnAsync(bool closeRelay, bool fixtureIsolated, CancellationToken token = default)
    {
        RequireCompatibilityIsolation(fixtureIsolated);
        return Queue(t =>
        {
            EnsureQualifiedCompatibility();
            if (!IsQualified(QualifiedFeature.Relay) || CompatibilityRelayPresent != true)
                throw new NotSupportedException("Confirm qualified Option 760 over SCPI in this session before setting the legacy startup relay.");
            return Guard(() => RunCompatibilityCommand("RLYPON " + BooleanHeader(closeRelay), t));
        }, token);
    }

    public Task<CompatibilitySelfTestResult> RunCompatibilitySelfTestAsync(bool fixtureIsolated, CancellationToken token = default)
    {
        RequireCompatibilityIsolation(fixtureIsolated);
        return Queue(t =>
        {
            EnsureQualifiedCompatibility();
            return Guard(() =>
            {
                channel.Write("OUT 0", t);
                int code = ScpiParser.Register(channel.Query("TEST?", t, 20));
                channel.Write("OUT 0", t);
                return new CompatibilitySelfTestResult(code, ReadLegacyError(t), clock.UtcNow);
            });
        }, token);
    }

    public Task<CompatibilityCommandResult> ClearCompatibilityAsync(bool fixtureIsolated, CancellationToken token = default)
    {
        RequireCompatibilityIsolation(fixtureIsolated);
        return Queue(t =>
        {
            EnsureQualifiedCompatibility();
            return Guard(() =>
            {
                channel.Write("OUT 0", t);
                channel.Write("CLR", t);
                channel.Write("OUT 0", t);
                int clearError = ReadLegacyError(t);
                channel.Write("OUT 0", t); // CLR may discard bytes that were buffered during its reset.
                int offError = ReadLegacyError(t);
                return new CompatibilityCommandResult(clearError != 0 ? clearError : offError, clock.UtcNow);
            });
        }, token);
    }

    private Task<CompatibilityCommandResult> RunCompatibilityCommandAsync(string command, CancellationToken token) => Queue(t =>
    {
        EnsureQualifiedCompatibility();
        return Guard(() => RunCompatibilityCommand(command, t));
    }, token);

    private CompatibilityCommandResult RunCompatibilityCommand(string command, CancellationToken token)
    {
        int previousError = ReadLegacyError(token);
        if (previousError != 0) return new(previousError, clock.UtcNow, CommandSent: false);
        channel.Write(command, token);
        return new(ReadLegacyError(token), clock.UtcNow);
    }

    private void EnsureQualifiedCompatibility()
    {
        EnsureCompatibility();
        if (!IsQualified(QualifiedFeature.Compatibility)) throw new InvalidOperationException("Legacy writes require the matched, validated instrument and serial profile.");
    }

    private static void RequireCompatibilityIsolation(bool fixtureIsolated)
    {
        if (!fixtureIsolated) throw new InvalidOperationException("Disconnect the load and isolate external equipment first.");
    }
}
