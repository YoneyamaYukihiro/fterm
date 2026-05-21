using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fterm.Core.Sessions;
using Fterm.Core.Transfer;

namespace Fterm.UI.ViewModels;

/// <summary>
/// 二画面ファイルブラウザ用のタブ ViewModel。
/// ローカルとリモートの <see cref="FileBrowserViewModel"/> を並べ、
/// F5 / F8 / F7 / F2 等のキーで両者間の転送・操作を行う。
/// </summary>
public sealed partial class FileTabViewModel : ViewModelBase, IAsyncDisposable
{
    public TransferQueue Queue { get; }
    public FileBrowserViewModel Local { get; }
    public FileBrowserViewModel Remote { get; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private FileBrowserViewModel _activePane;

    public FileTabViewModel(string title, IFileChannel localChannel, string localInitial, IFileChannel remoteChannel, string remoteInitial)
    {
        _title = title;
        Queue = new TransferQueue(maxParallel: 4);
        Local = new FileBrowserViewModel(localChannel, localInitial, isRemote: false);
        Remote = new FileBrowserViewModel(remoteChannel, remoteInitial, isRemote: true);
        _activePane = Local;
    }

    public async Task InitializeAsync()
    {
        await Local.InitializeAsync();
        await Remote.InitializeAsync();
    }

    public FileBrowserViewModel OtherPane(FileBrowserViewModel pane) =>
        pane == Local ? Remote : Local;

    [RelayCommand]
    public void TransferSelected()
    {
        var src = ActivePane;
        var dst = OtherPane(src);
        if (src.SelectedEntry is null || src.SelectedEntry.IsParentLink) return;
        if (src.SelectedEntry.IsDirectory)
        {
            src.StatusText = "ディレクトリ転送は未対応 (M5 で対応予定)";
            return;
        }

        var entry = src.SelectedEntry.Entry;
        var dstPath = dst.CombinePath(dst.CurrentPath, entry.Name);

        var direction = src.IsRemote ? TransferDirection.Download : TransferDirection.Upload;
        var task = new TransferTask
        {
            DisplayName = entry.Name,
            SourcePath = entry.FullPath,
            DestinationPath = dstPath,
            Direction = direction,
            TotalBytes = entry.Size,
            Operation = async (progress, ct) =>
            {
                await using var input = await src.Channel.OpenReadAsync(entry.FullPath, ct);
                await using var output = await dst.Channel.OpenWriteAsync(dstPath, ct);
                await CopyWithProgressAsync(input, output, progress, ct);
            },
        };
        Queue.Enqueue(task);
        task.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(TransferTask.State) && task.State == TransferState.Completed)
            {
                await dst.ReloadAsync();
            }
        };
    }

    private static async Task CopyWithProgressAsync(Stream src, Stream dst, IProgress<long> progress, CancellationToken ct)
    {
        var buffer = new byte[81920];
        long total = 0;
        int n;
        while ((n = await src.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct);
            total += n;
            progress.Report(total);
        }
    }

    public async ValueTask DisposeAsync()
    {
        Queue.Dispose();
        await Local.Channel.DisposeAsync();
        await Remote.Channel.DisposeAsync();
    }
}
