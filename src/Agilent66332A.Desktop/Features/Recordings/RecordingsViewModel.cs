using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Agilent66332A.Storage.Recordings;

namespace Agilent66332A.Desktop.Features.Recordings;
public sealed partial class RecordingsViewModel(Func<string> root, Func<Func<Task>, Task> execute) : ObservableObject
{
    public ObservableCollection<RecordingSummary> Items { get; } = [];
    [ObservableProperty] private RecordingSummary? selected;
    [RelayCommand] public Task RefreshAsync() => execute(async () =>
    {
        var items = await Task.Run(() => RecordingCatalog.Discover(root()));
        Items.Clear(); foreach (var item in items) Items.Add(item);
    });
    [RelayCommand] private Task OpenFolderAsync() => execute(() =>
    {
        string path = Selected?.DirectoryPath ?? root();
        if (!System.IO.Directory.Exists(path)) throw new System.IO.DirectoryNotFoundException("No recordings yet. Start a recording to create the folder.");
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        return Task.CompletedTask;
    });
}
