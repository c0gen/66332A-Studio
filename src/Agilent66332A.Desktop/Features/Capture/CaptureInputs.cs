using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Desktop.Features.Capture;

public sealed record CaptureChoice<T>(string Label, T Value)
{
    public override string ToString() => Label;
}

public sealed partial class SignalTriggerDraft : ObservableObject
{
    [ObservableProperty] private string level = "0";
    [ObservableProperty] private string hysteresis = "0";
    [ObservableProperty] private TriggerSlope slope;
    [ObservableProperty] private string count = "1";

    public IReadOnlyList<CaptureChoice<TriggerSlope>> Slopes { get; } =
        [new("Either edge", TriggerSlope.Either), new("Rising", TriggerSlope.Rising), new("Falling", TriggerSlope.Falling)];

    internal SignalTrigger Build() => new(CaptureInput.Number(Level, "Trigger level"),
        CaptureInput.Number(Hysteresis, "Hysteresis"), Slope, CaptureInput.Integer(Count, "Sweep count"));

    internal void Load(SignalTrigger trigger)
    {
        Level = CaptureInput.Text(trigger.Level);
        Hysteresis = CaptureInput.Text(trigger.Hysteresis);
        Slope = trigger.Slope;
        Count = CaptureInput.Text(trigger.Count);
    }
}

internal static class CaptureInput
{
    public static string Text(double value) => value.ToString("G", CultureInfo.CurrentCulture);
    public static double Number(string value, string field) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.CurrentCulture, out double parsed) && double.IsFinite(parsed)
            ? parsed : throw new ArgumentException($"Enter a numeric value for {field}.");
    public static int Integer(string value, string field) =>
        int.TryParse(value, NumberStyles.Integer, CultureInfo.CurrentCulture, out int parsed)
            ? parsed : throw new ArgumentException($"Enter a whole number for {field}.");
    public static TimeSpan WaitLimit(string value)
    {
        double seconds = Number(value, "wait limit");
        if (seconds is <= 0 or > 86400) throw new ArgumentException("Enter a wait limit between 0 and 86,400 seconds.");
        return TimeSpan.FromSeconds(seconds);
    }
}
