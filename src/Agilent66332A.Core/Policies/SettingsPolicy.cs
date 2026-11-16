using Agilent66332A.Core.Models;

namespace Agilent66332A.Core.Policies;

public static class SettingsPolicy
{
    public static void Validate(RequestedSettings target, DeviceSnapshot state)
    {
        if (!state.Capabilities.Voltage.Contains(target.VoltageV)) throw new ArgumentException("Voltage is outside the device bounds or is not finite.");
        if (!state.Capabilities.Current.Contains(target.CurrentLimitA)) throw new ArgumentException("Current limit is outside the device bounds or is not finite.");
        if (target.VoltageV >= state.Protections.OvpV) throw new ArgumentException("Voltage must be below the active OVP threshold.");
    }
    public static void ValidateProtections(ProtectionSettings target, DeviceSnapshot state)
    {
        if (!state.Capabilities.Ovp.Contains(target.OvpV) || target.OvpV <= state.Settings.VoltageV)
            throw new ArgumentException("OVP must be within device bounds and above the acknowledged voltage.");
        if (!state.Capabilities.OcpDelay.Contains(target.OcpDelaySeconds)) throw new ArgumentException("OCP delay is outside the device bounds.");
    }
    public static IReadOnlyList<(string Field, double Value)> Order(RequestedSettings target, AcknowledgedSettings current)
    {
        var result = new List<(string, double)>();
        if (target.VoltageV < current.VoltageV) result.Add(("VOLT", target.VoltageV));
        if (target.CurrentLimitA < current.CurrentLimitA) result.Add(("CURR", target.CurrentLimitA));
        if (target.CurrentLimitA > current.CurrentLimitA) result.Add(("CURR", target.CurrentLimitA));
        if (target.VoltageV > current.VoltageV) result.Add(("VOLT", target.VoltageV));
        return result;
    }
    public static bool IsStale(double now, MeasurementPair pair, double pollSeconds) =>
        pair.Quality != SampleQuality.Valid || now - pair.OldestMonotonicSeconds > Math.Max(3, 3 * pollSeconds);
}
