using System.ComponentModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using Agilent66332A.Core.Sequences;
using Agilent66332A.Desktop.Controls;

namespace Agilent66332A.Desktop.Features.Sequences;
public sealed partial class SequenceRow : ObservableObject, IDataErrorInfo
{
    public string Id { get; } = Guid.NewGuid().ToString("N");
    [ObservableProperty] private string label = "New step";
    [ObservableProperty] private string voltageV = "1";
    [ObservableProperty] private string currentLimitA = "0.01";
    [ObservableProperty] private bool outputEnabled;
    [ObservableProperty] private string holdSeconds = "3";
    [ObservableProperty] private bool isActive;
    public SequenceRow() { }
    public SequenceRow(SequenceStep step) { Id = step.Id; Label = step.Label; VoltageV = F(step.VoltageV); CurrentLimitA = F(step.CurrentLimitA); OutputEnabled = step.OutputEnabled; HoldSeconds = F(step.HoldSeconds); }
    private static string F(double v) => v.ToString("G", CultureInfo.CurrentCulture);
    public SequenceStep ToStep() => new() { Id = Id, Label = Label, VoltageV = NumericUnitEditor.Parse(VoltageV), CurrentLimitA = NumericUnitEditor.Parse(CurrentLimitA), OutputEnabled = OutputEnabled, HoldSeconds = NumericUnitEditor.Parse(HoldSeconds) };
    public string Error => string.Join(" ", new[] { nameof(VoltageV), nameof(CurrentLimitA), nameof(HoldSeconds) }.Select(p => this[p]).Where(s => s.Length > 0));
    public string this[string columnName]
    {
        get
        {
            if (columnName is not (nameof(VoltageV) or nameof(CurrentLimitA) or nameof(HoldSeconds))) return "";
            try
            {
                double n = NumericUnitEditor.Parse(columnName switch { nameof(VoltageV) => VoltageV, nameof(CurrentLimitA) => CurrentLimitA, _ => HoldSeconds });
                return columnName == nameof(HoldSeconds)
                    ? n < 1 ? "Hold for at least 1 s." : ""
                    : n < 0 ? "Enter a nonnegative setpoint." : "";
            }
            catch (ArgumentException e) { return e.Message; }
        }
    }
}
