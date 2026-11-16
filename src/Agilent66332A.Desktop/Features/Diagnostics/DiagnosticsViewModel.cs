using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Agilent66332A.Application.Sessions;
using Agilent66332A.Core.Models;

namespace Agilent66332A.Desktop.Features.Diagnostics;
public sealed partial class DiagnosticsViewModel(SessionCoordinator session, Func<Func<Task>, Task> execute) : ObservableObject
{
    public ObservableCollection<string> Events { get; } = [];
    public ObservableCollection<string> Transcript { get; } = [];
    public DiagnosticsRegistersViewModel Registers { get; } = new(session, execute);
    public void Refresh(bool isBusy) => Registers.Refresh(isBusy);
    [ObservableProperty] private string exportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "66332A Studio", "diagnostics.txt");
    public void Add(StudioEvent entry) { Events.Insert(0, $"{entry.Utc.LocalDateTime:HH:mm:ss.fff}  {entry.Type}: {entry.Message}"); while (Events.Count > 500) Events.RemoveAt(Events.Count - 1); }
    public void AddTrace(string text) { Transcript.Insert(0, $"{DateTime.Now:HH:mm:ss.fff}  {text}"); while (Transcript.Count > 1000) Transcript.RemoveAt(Transcript.Count - 1); }
    private string Text() => string.Join(Environment.NewLine, new[] { Registers.Summary, Registers.LastAction }
        .Concat(Registers.ErrorEntries).Concat(Events).Append("Protocol (most recent first)").Concat(Transcript));
    [RelayCommand] private void Copy() => Clipboard.SetText(Text());
    [RelayCommand] private Task ExportAsync() => execute(async () =>
    {
        string text = Text(); string path = Path.GetFullPath(ExportPath);
        await Task.Run(async () => { Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllTextAsync(path, text); });
    });
}
