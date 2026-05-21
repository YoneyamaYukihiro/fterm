using System.Runtime.CompilerServices;
using Fterm.Core.Sessions;

namespace Fterm.Core.Files;

/// <summary>
/// ローカルディスクに対する <see cref="IFileChannel"/> 実装。
/// </summary>
public sealed class LocalFileChannel : IFileChannel
{
    public Task ConnectAsync(CancellationToken ct) => Task.CompletedTask;

    public async IAsyncEnumerable<RemoteEntry> ListAsync(string path, [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.Yield();
        foreach (var entry in EnumerateInternal(path))
        {
            ct.ThrowIfCancellationRequested();
            yield return entry;
        }
    }

    private static IEnumerable<RemoteEntry> EnumerateInternal(string path)
    {
        foreach (var d in Directory.EnumerateDirectories(path))
        {
            var info = new DirectoryInfo(d);
            yield return new RemoteEntry(info.Name, info.FullName, 0, info.LastWriteTime, true, null, null, null);
        }
        foreach (var f in Directory.EnumerateFiles(path))
        {
            var info = new FileInfo(f);
            yield return new RemoteEntry(info.Name, info.FullName, info.Length, info.LastWriteTime, false, null, null, null);
        }
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct) =>
        Task.FromResult<Stream>(File.OpenRead(path));

    public Task<Stream> OpenWriteAsync(string path, CancellationToken ct) =>
        Task.FromResult<Stream>(File.Create(path));

    public Task MakeDirectoryAsync(string path, CancellationToken ct)
    {
        Directory.CreateDirectory(path);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(string path, bool recursive, CancellationToken ct)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive);
        else if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }

    public Task RenameAsync(string from, string to, CancellationToken ct)
    {
        if (Directory.Exists(from)) Directory.Move(from, to);
        else File.Move(from, to, overwrite: false);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
