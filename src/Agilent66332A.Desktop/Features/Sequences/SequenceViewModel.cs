using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Agilent66332A.Application.Runs;
using Agilent66332A.Application.Sessions;
using Agilent66332A.Core.Abstractions;
using Agilent66332A.Core.Models;
using Agilent66332A.Core.Sequences;
using Agilent66332A.Storage.Sequences;

namespace Agilent66332A.Desktop.Features.Sequences;

public sealed partial class SequenceViewModel : ObservableObject
{
    private readonly SessionCoordinator session;
    private readonly IMonotonicClock clock;
    private readonly Func<Func<Task>, Task> execute;
    private SequenceRow? activeRow;
    [ObservableProperty] private string name = "Unloaded connection check";
    [ObservableProperty] private string repeats = "1";
    [ObservableProperty] private string filePath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "66332A Studio", "sequence.json");
    [ObservableProperty] private string validation = "";
    [ObservableProperty] private string progressText = "No active run";
    [ObservableProperty] private double holdFraction;
    [ObservableProperty] private string runResult = "";
    [ObservableProperty] private bool isLocked;
    [ObservableProperty] private bool isDirty;
    [ObservableProperty] private SequenceRow? selectedRow;
    public ObservableCollection<SequenceRow> Rows { get; } = [];
    public RunProgress? Progress { get; set; }
    public SequenceViewModel(SessionCoordinator session, IMonotonicClock clock, Func<Func<Task>, Task> execute)
    {
        this.session = session; this.clock = clock; this.execute = execute;
        LoadDocument(SequenceDocument.Example);
        Rows.CollectionChanged += (_, _) => MarkEdited();
    }
    public SequenceDocument Build()
    {
        if (!int.TryParse(Repeats, NumberStyles.None, CultureInfo.CurrentCulture, out int count)) throw new ArgumentException("Repeat count must be a positive integer.");
        return new() { SchemaVersion = 1, Name = Name, TargetModel = "66332A", RepeatCount = count, Steps = Rows.Select(row => row.ToStep()).ToArray() };
    }
    public string Preview
    {
        get
        {
            try
            {
                var document = Build();
                string steps = Rows.Count == 1 ? "step" : "steps";
                string repeats = document.RepeatCount == 1 ? "repeat" : "repeats";
                return $"{Rows.Count} {steps}, {document.RepeatCount} {repeats}, {document.NominalSeconds:G} s of holds";
            }
            catch (ArgumentException e) { return e.Message; }
        }
    }
    private bool Editable() => !IsLocked;
    private bool CanRun() => !IsLocked && session.CanConfigure && session.CanMeasureDc && !session.IsRecording;
    private SequenceRow Track(SequenceRow row)
    {
        row.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName != nameof(SequenceRow.IsActive)) MarkEdited();
        };
        return row;
    }
    private void MarkEdited()
    {
        IsDirty = true;
        Validation = "";
        OnPropertyChanged(nameof(Preview));
    }
    private void LoadDocument(SequenceDocument document)
    {
        Name = document.Name; Repeats = document.RepeatCount.ToString(CultureInfo.CurrentCulture);
        Rows.Clear(); foreach (var step in document.Steps) Rows.Add(Track(new(step)));
        SelectedRow = Rows.FirstOrDefault(); IsDirty = false; OnPropertyChanged(nameof(Preview));
    }
    [RelayCommand(CanExecute = nameof(Editable))] private void Add() { var row = Track(new()); Rows.Add(row); SelectedRow = row; }
    [RelayCommand(CanExecute = nameof(Editable))] private void Duplicate()
    {
        if (SelectedRow is not { } row) return;
        var copy = Track(new(row.ToStep() with { Id = Guid.NewGuid().ToString("N") })); Rows.Insert(Rows.IndexOf(row) + 1, copy); SelectedRow = copy;
    }
    [RelayCommand(CanExecute = nameof(Editable))] private void Delete() { if (SelectedRow is { } row) Rows.Remove(row); }
    [RelayCommand(CanExecute = nameof(Editable))] private void MoveUp() { if (SelectedRow is { } row && Rows.IndexOf(row) > 0) Rows.Move(Rows.IndexOf(row), Rows.IndexOf(row) - 1); }
    [RelayCommand(CanExecute = nameof(Editable))] private void MoveDown() { if (SelectedRow is { } row && Rows.IndexOf(row) < Rows.Count - 1) Rows.Move(Rows.IndexOf(row), Rows.IndexOf(row) + 1); }
    [RelayCommand(CanExecute = nameof(Editable))] private void Validate()
    {
        try { var errors = SequenceValidator.Validate(Build(), session.Snapshot); Validation = errors.Count == 0 ? "Sequence valid." : string.Join("\n", errors); }
        catch (ArgumentException e) { Validation = e.Message; }
    }
    [RelayCommand(CanExecute = nameof(Editable))] private Task SaveAsync() => execute(async () => { var d = Build(); await SequenceRepository.SaveAsync(FilePath, d); IsDirty = false; });
    [RelayCommand(CanExecute = nameof(Editable))] private Task OpenAsync() => execute(async () => { var d = await SequenceRepository.LoadAsync(FilePath); LoadDocument(d); Validation = "Sequence opened."; });
    [RelayCommand(CanExecute = nameof(CanRun))] private Task RunAsync() => execute(async () =>
    {
        RunResult = "";
        var result = await session.RunAsync(Build());
        string shutdown = result.Shutdown.Outcome == ShutdownOutcome.VerifiedOff ? "Output off verified" : "Output off unverified";
        RunResult = $"{result.ExecutionOutcome}  •  {shutdown}  •  Recording {result.RecordingOutcome.ToString().ToLowerInvariant()}";
        if (result.ExecutionOutcome == ExecutionOutcome.Failed) RunResult += $". {result.Error}";
    });
    public void Tick()
    {
        if (Progress is not { } p) return;
        MarkActiveRow(session.IsRunning ? p.Row : 0);
        double remainingSeconds = p.HoldDeadline - clock.Seconds;
        HoldFraction = p.Phase == "Holding" && activeRow is { } row &&
            double.TryParse(row.HoldSeconds, NumberStyles.Float, CultureInfo.CurrentCulture, out double duration) &&
            double.IsFinite(duration) && duration > 0
            ? 1 - Math.Clamp(remainingSeconds / duration, 0, 1)
            : 0;
        if (p.Row == 0)
        {
            ProgressText = p.Phase == "Finished" ? "Run finished" : p.Message;
            return;
        }
        string remaining = p.Phase == "Holding" ? $"  •  {Math.Max(0, remainingSeconds):F1} s left" : "";
        ProgressText = $"{p.Phase} step {p.Row}, repeat {p.Repeat}  •  {p.Message}{remaining}";
    }
    private void MarkActiveRow(int step)
    {
        var row = step > 0 && step <= Rows.Count ? Rows[step - 1] : null;
        if (row == activeRow) return;
        if (activeRow is not null) activeRow.IsActive = false;
        activeRow = row;
        if (activeRow is not null) activeRow.IsActive = true;
    }
    partial void OnRepeatsChanged(string value) => MarkEdited();
    partial void OnNameChanged(string value) => MarkEdited();
    public void Refresh(bool locked)
    {
        IsLocked = locked || session.IsRunning;
        foreach (var command in new IRelayCommand[] { AddCommand, DuplicateCommand, DeleteCommand, MoveUpCommand, MoveDownCommand, ValidateCommand, SaveCommand, OpenCommand, RunCommand }) command.NotifyCanExecuteChanged();
    }
}
