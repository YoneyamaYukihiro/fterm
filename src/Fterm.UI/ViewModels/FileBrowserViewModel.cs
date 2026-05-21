using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fterm.Core.Sessions;

namespace Fterm.UI.ViewModels;

/// <summary>
/// 片側のペインを表す。リモート / ローカルは <see cref="IFileChannel"/> の実装で切替。
/// 親パス算出には <see cref="IsRemote"/> を使い、ローカルは <see cref="Path"/>、
/// リモートは POSIX の '/' 区切りで親を計算する。
/// </summary>
public sealed partial class FileBrowserViewModel : ViewModelBase
{
    private readonly IFileChannel _channel;
    public bool IsRemote { get; }

    [ObservableProperty]
    private string _currentPath = "";

    [ObservableProperty]
    private FileEntryViewModel? _selectedEntry;

    [ObservableProperty]
    private string _statusText = "";

    public ObservableCollection<FileEntryViewModel> Entries { get; } = [];
    public string PaneTitle => IsRemote ? "リモート" : "ローカル";

    public FileBrowserViewModel(IFileChannel channel, string initialPath, bool isRemote)
    {
        _channel = channel;
        IsRemote = isRemote;
        _currentPath = initialPath;
    }

    public async Task InitializeAsync()
    {
        await _channel.ConnectAsync(CancellationToken.None);
        await ReloadAsync();
    }

    [RelayCommand]
    public async Task ReloadAsync()
    {
        try
        {
            Entries.Clear();
            Entries.Add(new FileEntryViewModel(
                new RemoteEntry("..", "", 0, DateTimeOffset.MinValue, true, null, null, null),
                isParentLink: true));
            var list = new List<FileEntryViewModel>();
            await foreach (var e in _channel.ListAsync(CurrentPath, CancellationToken.None))
            {
                list.Add(new FileEntryViewModel(e));
            }
            foreach (var v in list.OrderBy(x => !x.IsDirectory).ThenBy(x => x.Entry.Name, StringComparer.OrdinalIgnoreCase))
            {
                Entries.Add(v);
            }
            StatusText = $"{CurrentPath} ({list.Count} 件)";
        }
        catch (Exception ex)
        {
            StatusText = $"取得失敗: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task ActivateAsync()
    {
        if (SelectedEntry is null) return;
        if (SelectedEntry.IsParentLink)
        {
            CurrentPath = ParentOf(CurrentPath);
            await ReloadAsync();
        }
        else if (SelectedEntry.IsDirectory)
        {
            CurrentPath = SelectedEntry.FullPath;
            await ReloadAsync();
        }
    }

    [RelayCommand]
    public async Task GoUpAsync()
    {
        CurrentPath = ParentOf(CurrentPath);
        await ReloadAsync();
    }

    [RelayCommand]
    public async Task NavigateAsync(string path)
    {
        CurrentPath = path;
        await ReloadAsync();
    }

    [RelayCommand]
    public async Task DeleteSelectedAsync()
    {
        if (SelectedEntry is null || SelectedEntry.IsParentLink) return;
        try
        {
            await _channel.DeleteAsync(SelectedEntry.FullPath, recursive: SelectedEntry.IsDirectory, CancellationToken.None);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"削除失敗: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task MakeDirectoryAsync(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        try
        {
            var newPath = CombinePath(CurrentPath, name);
            await _channel.MakeDirectoryAsync(newPath, CancellationToken.None);
            await ReloadAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"作成失敗: {ex.Message}";
        }
    }

    public IFileChannel Channel => _channel;

    public string CombinePath(string parent, string child) =>
        IsRemote
            ? (parent.EndsWith('/') ? parent + child : parent + "/" + child)
            : System.IO.Path.Combine(parent, child);

    private string ParentOf(string path)
    {
        if (IsRemote)
        {
            if (string.IsNullOrEmpty(path) || path == "/") return "/";
            var trimmed = path.TrimEnd('/');
            var idx = trimmed.LastIndexOf('/');
            return idx <= 0 ? "/" : trimmed[..idx];
        }
        var dir = System.IO.Path.GetDirectoryName(path);
        return string.IsNullOrEmpty(dir) ? path : dir;
    }
}
