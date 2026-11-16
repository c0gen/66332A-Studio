using Agilent66332A.Core.Models;
using Agilent66332A.Core.Policies;

namespace Agilent66332A.Core.Sequences;

public sealed record SequenceStep
{
    public required string Id { get; init; }
    public required string Label { get; init; }
    public required double VoltageV { get; init; }
    public required double CurrentLimitA { get; init; }
    public required bool OutputEnabled { get; init; }
    public required double HoldSeconds { get; init; }
}
public sealed record SequenceDocument
{
    public required int SchemaVersion { get; init; }
    public required string Name { get; init; }
    public required string TargetModel { get; init; }
    public required int RepeatCount { get; init; }
    public required SequenceStep[] Steps { get; init; }
    public double NominalSeconds => Steps.Sum(s => s.HoldSeconds) * RepeatCount;
    public SequenceDocument Freeze() => this with { Steps = Steps.Select(s => s with { }).ToArray() };
    public static SequenceDocument Example => new()
    {
        SchemaVersion = 1, Name = "Unloaded connection check", TargetModel = "66332A", RepeatCount = 1,
        Steps = [new() { Id = "step-1", Label = "Low voltage", VoltageV = 1, CurrentLimitA = .01, OutputEnabled = true, HoldSeconds = 3 },
                 new() { Id = "step-2", Label = "Second level", VoltageV = 2, CurrentLimitA = .01, OutputEnabled = true, HoldSeconds = 3 },
                 new() { Id = "step-3", Label = "Output off", VoltageV = 0, CurrentLimitA = .01, OutputEnabled = false, HoldSeconds = 2 }]
    };
}
public static class SequenceValidator
{
    public static IReadOnlyList<string> Validate(SequenceDocument document, DeviceSnapshot? state = null)
    {
        var errors = new List<string>();
        if (document.SchemaVersion != 1) errors.Add("Unsupported sequence schema version.");
        if (document.TargetModel != "66332A") errors.Add("Target model must be 66332A.");
        if (string.IsNullOrWhiteSpace(document.Name)) errors.Add("A sequence name is required.");
        if (document.RepeatCount is < 1 or > 100000) errors.Add("Repeat count must be an integer from 1 to 100000.");
        if (document.Steps is null || document.Steps.Length is < 1 or > 10000) { errors.Add("A sequence requires 1 to 10000 rows."); return errors; }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i < document.Steps.Length; i++)
        {
            var step = document.Steps[i];
            if (step is null) { errors.Add($"Row {i + 1}: missing step."); continue; }
            if (string.IsNullOrWhiteSpace(step.Id) || !ids.Add(step.Id)) errors.Add($"Row {i + 1}: ID must be present and unique.");
            if (!double.IsFinite(step.HoldSeconds) || step.HoldSeconds < 1 || step.HoldSeconds > TimeSpan.MaxValue.TotalSeconds)
                errors.Add($"Row {i + 1}: hold must be at least 1 second and fit a duration.");
            if (!double.IsFinite(step.VoltageV) || !double.IsFinite(step.CurrentLimitA) || step.VoltageV < 0 || step.CurrentLimitA < 0)
                errors.Add($"Row {i + 1}: setpoints must be finite and nonnegative.");
            if (state is not null)
                try { SettingsPolicy.Validate(new(step.VoltageV, step.CurrentLimitA), state); }
                catch (ArgumentException e) { errors.Add($"Row {i + 1}: {e.Message}"); }
        }
        if (!double.IsFinite(document.Steps.Where(s => s is not null).Sum(s => s.HoldSeconds) * document.RepeatCount)
            || document.Steps.Where(s => s is not null).Sum(s => s.HoldSeconds) * document.RepeatCount > TimeSpan.MaxValue.TotalSeconds)
            errors.Add("Nominal sequence duration overflows.");
        return errors;
    }
}
