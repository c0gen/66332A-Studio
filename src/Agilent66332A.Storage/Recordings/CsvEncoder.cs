using System.Globalization;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Storage.Recordings;

public static class CsvEncoder
{
    public const string Header = "schema_version,recording_id,sample_index,session_generation,source_kind,cycle_started_utc,cycle_completed_utc,elapsed_s,voltage_started_utc,voltage_completed_utc,current_started_utc,current_completed_utc,step_id,row_index,repeat_index,run_phase,requested_voltage_v,requested_current_limit_a,requested_output_enabled,ack_voltage_v,ack_current_limit_a,ack_output_enabled,settings_verified_utc,measured_voltage_v,measured_current_a,estimated_power_w,pair_skew_s,sample_quality,regulation_mode,operation_condition_raw,questionable_condition_raw,fault_flags,status_verified_utc";
    public static string Escape(object? value)
    {
        string text = value switch
        {
            null => "", DateTimeOffset utc => utc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            double d => d.ToString("G17", CultureInfo.InvariantCulture), bool b => b ? "true" : "false",
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture), _ => value.ToString() ?? ""
        };
        return text.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + text.Replace("\"", "\"\"") + "\"" : text;
    }
    public static string Row(string id, long index, string source, MeasurementPair pair, double elapsed, RunPosition? position)
    {
        var state = pair.Snapshot;
        object?[] cells = [1, id, index, pair.Generation, source, pair.Voltage.StartedUtc, pair.Current.CompletedUtc,
            elapsed, pair.Voltage.StartedUtc, pair.Voltage.CompletedUtc, pair.Current.StartedUtc, pair.Current.CompletedUtc,
            position?.StepId, position?.RowIndex, position?.RepeatIndex, position?.Phase, position?.Requested.VoltageV,
            position?.Requested.CurrentLimitA, position?.OutputEnabled, state.Settings.VoltageV, state.Settings.CurrentLimitA,
            state.Output.ToString().ToLowerInvariant(), state.Settings.VerifiedUtc, pair.Voltage.Value, pair.Current.Value,
            pair.EstimatedPowerW, pair.PairSkewSeconds, pair.Quality, state.Status.RegulationMode, state.Status.OperationCondition,
            state.Status.QuestionableCondition, state.Status.FaultFlags, state.Status.VerifiedUtc];
        return string.Join(',', cells.Select(Escape));
    }
}
