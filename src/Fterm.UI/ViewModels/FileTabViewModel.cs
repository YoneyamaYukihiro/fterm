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

    /// <summary>衝突方針。Ask の場合は CollisionResolver を呼ぶ。</summary>
    public CollisionPolicy DefaultCollisionPolicy { get; set; } = CollisionPolicy.Ask;
    public ICollisionPolicyResolver? CollisionResolver { get; set; }

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private FileBrowserViewModel _activePane;

    public FileTabViewModel(string title, IFileChannel localChannel, string localInitial, IFileChannel remoteChannel, string remoteInitial, int parallelism = 4)
    {
        _title = title;
        Queue = new TransferQueue(parallelism);
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
    public Task TransferSelectedAsync() => TransferFromAsync(ActivePane);

    public async Task TransferFromAsync(FileBrowserViewModel src)
    {
        var dst = OtherPane(src);
        if (src.SelectedEntry is null || src.SelectedEntry.IsParentLink) return;

        var entry = src.SelectedEntry.Entry;
        var direction = src.IsRemote ? TransferDirection.Download : TransferDirection.Upload;
        try
        {
            if (entry.IsDirectory)
            {
                await EnqueueDirectoryAsync(src, dst, entry, direction);
            }
            else
            {
                await EnqueueFileAsync(src, dst, entry, dst.CombinePath(dst.CurrentPath, entry.Name), direction);
            }
        }
        catch (Exception ex)
        {
            src.StatusText = $"転送準備失敗: {ex.Message}";
        }
    }

    /// <summary>外部からドロップされたローカルファイルパスをリモート (or 反対側) に転送する。</summary>
    public async Task TransferExternalAsync(IEnumerable<string> localPaths, FileBrowserViewModel target)
    {
        // 「外部 (OS のエクスプローラ等) からのドロップ」はローカル → ターゲットの転送。
        // 万一ターゲットがローカルペインの場合はファイラのコピー扱いになる。
        var localChannel = Local.Channel;
        foreach (var path in localPaths)
        {
            try
            {
                var name = Path.GetFileName(path);
                if (Directory.Exists(path))
                {
                    var dstRoot = target.CombinePath(target.CurrentPath, name);
                    if (!await target.Channel.ExistsAsync(dstRoot, CancellationToken.None))
                    {
                        await target.Channel.MakeDirectoryAsync(dstRoot, CancellationToken.None);
                    }
                    await WalkLocalAsync(path, dstRoot, target);
                }
                else if (File.Exists(path))
                {
                    var info = new FileInfo(path);
                    var fakeEntry = new RemoteEntry(info.Name, info.FullName, info.Length, info.LastWriteTime, false, null, null, null);
                    var fakeSrc = Local; // 表示上のソース
                    await EnqueueFileAsync(fakeSrc, target, fakeEntry,
                        target.CombinePath(target.CurrentPath, info.Name),
                        target.IsRemote ? TransferDirection.Upload : TransferDirection.Download);
                }
            }
            catch (Exception ex)
            {
                target.StatusText = $"外部転送失敗: {ex.Message}";
            }
        }
    }

    private async Task WalkLocalAsync(string srcDir, string dstDir, FileBrowserViewModel target)
    {
        foreach (var child in Directory.EnumerateDirectories(srcDir))
        {
            var name = Path.GetFileName(child);
            var dst = target.CombinePath(dstDir, name);
            if (!await target.Channel.ExistsAsync(dst, CancellationToken.None))
                await target.Channel.MakeDirectoryAsync(dst, CancellationToken.None);
            await WalkLocalAsync(child, dst, target);
        }
        foreach (var f in Directory.EnumerateFiles(srcDir))
        {
            var info = new FileInfo(f);
            var entry = new RemoteEntry(info.Name, info.FullName, info.Length, info.LastWriteTime, false, null, null, null);
            await EnqueueFileAsync(Local, target, entry, target.CombinePath(dstDir, info.Name),
                target.IsRemote ? TransferDirection.Upload : TransferDirection.Download);
        }
    }

    private async Task EnqueueFileAsync(FileBrowserViewModel src, FileBrowserViewModel dst, RemoteEntry entry, string dstPath, TransferDirection direction)
    {
        long resumeOffset = 0;
        var finalDstPath = dstPath;

        if (await dst.Channel.ExistsAsync(dstPath, CancellationToken.None))
        {
            var existingSize = await dst.Channel.GetSizeAsync(dstPath, CancellationToken.None);
            var policy = DefaultCollisionPolicy;
            if (policy == CollisionPolicy.Ask)
            {
                if (CollisionResolver is null)
                {
                    policy = CollisionPolicy.Skip;
                }
                else
                {
                    policy = await CollisionResolver.ResolveAsync(
                        new CollisionPrompt(entry.FullPath, dstPath, entry.Size, existingSize),
                        CancellationToken.None);
                }
            }
            switch (policy)
            {
                case CollisionPolicy.Skip:
                    src.StatusText = $"スキップ: {entry.Name}";
                    return;
                case CollisionPolicy.Overwrite:
                    break;
                case CollisionPolicy.Resume:
                    if (existingSize >= 0 && existingSize < entry.Size) resumeOffset = existingSize;
                    else if (existingSize == entry.Size) { src.StatusText = $"完了済み: {entry.Name}"; return; }
                    break;
                case CollisionPolicy.Rename:
                    finalDstPath = await NextNonExistingNameAsync(dst, dstPath);
                    break;
            }
        }

        var task = new TransferTask
        {
            DisplayName = Path.GetFileName(finalDstPath),
            SourcePath = entry.FullPath,
            DestinationPath = finalDstPath,
            Direction = direction,
            TotalBytes = entry.Size,
            Operation = async (progress, ct) =>
            {
                await using var input = await src.Channel.OpenReadAsync(entry.FullPath, ct);
                Stream output;
                if (resumeOffset > 0)
                {
                    // ソース側を resumeOffset まで読み飛ばす
                    var skipBuf = new byte[8192];
                    var remaining = resumeOffset;
                    while (remaining > 0)
                    {
                        var read = await input.ReadAsync(skipBuf.AsMemory(0, (int)Math.Min(skipBuf.Length, remaining)), ct);
                        if (read <= 0) break;
                        remaining -= read;
                    }
                    output = await dst.Channel.OpenAppendAsync(finalDstPath, ct);
                    progress.Report(resumeOffset);
                }
                else
                {
                    output = await dst.Channel.OpenWriteAsync(finalDstPath, ct);
                }
                await using (output)
                {
                    await CopyWithProgressAsync(input, output, progress, ct, startingFrom: resumeOffset);
                }
            },
        };
        Queue.Enqueue(task);
        task.PropertyChanged += async (_, e) =>
        {
            if (e.PropertyName == nameof(TransferTask.State) && task.State == TransferState.Completed)
            {
                if (PathStartsWith(finalDstPath, dst.CurrentPath, dst.IsRemote)) await dst.ReloadAsync();
            }
        };
    }

    private async Task EnqueueDirectoryAsync(FileBrowserViewModel src, FileBrowserViewModel dst, RemoteEntry rootEntry, TransferDirection direction)
    {
        var dstRoot = dst.CombinePath(dst.CurrentPath, rootEntry.Name);
        if (!await dst.Channel.ExistsAsync(dstRoot, CancellationToken.None))
        {
            await dst.Channel.MakeDirectoryAsync(dstRoot, CancellationToken.None);
        }
        await WalkAsync(src, dst, rootEntry.FullPath, dstRoot, direction);
        await dst.ReloadAsync();
    }

    private async Task WalkAsync(FileBrowserViewModel src, FileBrowserViewModel dst, string srcDir, string dstDir, TransferDirection direction)
    {
        await foreach (var child in src.Channel.ListAsync(srcDir, CancellationToken.None))
        {
            var childDst = dst.CombinePath(dstDir, child.Name);
            if (child.IsDirectory)
            {
                if (!await dst.Channel.ExistsAsync(childDst, CancellationToken.None))
                    await dst.Channel.MakeDirectoryAsync(childDst, CancellationToken.None);
                await WalkAsync(src, dst, child.FullPath, childDst, direction);
            }
            else
            {
                await EnqueueFileAsync(src, dst, child, childDst, direction);
            }
        }
    }

    private static async Task<string> NextNonExistingNameAsync(FileBrowserViewModel dst, string path)
    {
        var dir = dst.IsRemote
            ? (path.LastIndexOf('/') is var i && i > 0 ? path[..i] : "")
            : Path.GetDirectoryName(path) ?? "";
        var name = dst.IsRemote
            ? path[(path.LastIndexOf('/') + 1)..]
            : Path.GetFileName(path);
        var stem = Path.GetFileNameWithoutExtension(name);
        var ext = Path.GetExtension(name);
        for (var n = 2; n < 1000; n++)
        {
            var candidate = dst.CombinePath(dir, $"{stem}_{n}{ext}");
            if (!await dst.Channel.ExistsAsync(candidate, CancellationToken.None)) return candidate;
        }
        return dst.CombinePath(dir, $"{stem}_{Guid.NewGuid():N}{ext}");
    }

    private static async Task CopyWithProgressAsync(Stream src, Stream dst, IProgress<long> progress, CancellationToken ct, long startingFrom = 0)
    {
        var buffer = new byte[81920];
        long total = startingFrom;
        int n;
        while ((n = await src.ReadAsync(buffer.AsMemory(), ct)) > 0)
        {
            await dst.WriteAsync(buffer.AsMemory(0, n), ct);
            total += n;
            progress.Report(total);
        }
    }

    private static bool PathStartsWith(string fullPath, string currentDir, bool isRemote)
    {
        if (isRemote)
        {
            var normalized = currentDir.TrimEnd('/') + "/";
            return fullPath.StartsWith(normalized, StringComparison.Ordinal) ||
                   (fullPath.Length > 0 && fullPath[..(fullPath.LastIndexOf('/') + 1)] == normalized);
        }
        return Path.GetDirectoryName(fullPath)?.Equals(currentDir, StringComparison.OrdinalIgnoreCase) ?? false;
    }

    public async ValueTask DisposeAsync()
    {
        Queue.Dispose();
        await Local.Channel.DisposeAsync();
        await Remote.Channel.DisposeAsync();
    }
}
