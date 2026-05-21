namespace Fterm.Core.Sessions;

public sealed record RemoteEntry(
    string Name,
    string FullPath,
    long Size,
    DateTimeOffset LastWriteTime,
    bool IsDirectory,
    string? Owner,
    string? Group,
    string? Permissions);

public interface IFileChannel : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken ct);
    IAsyncEnumerable<RemoteEntry> ListAsync(string path, CancellationToken ct);
    Task<Stream> OpenReadAsync(string path, CancellationToken ct);
    Task<Stream> OpenWriteAsync(string path, CancellationToken ct);
    Task MakeDirectoryAsync(string path, CancellationToken ct);
    Task DeleteAsync(string path, bool recursive, CancellationToken ct);
    Task RenameAsync(string from, string to, CancellationToken ct);

    /// <summary>パスに対応する要素が存在するか。存在しない場合は null。</summary>
    Task<bool> ExistsAsync(string path, CancellationToken ct);

    /// <summary>ファイルサイズ。存在しないかディレクトリなら -1。</summary>
    Task<long> GetSizeAsync(string path, CancellationToken ct);

    /// <summary>追記モードで開く（レジューム用）。先頭から書き直したい場合は OpenWriteAsync を使う。</summary>
    Task<Stream> OpenAppendAsync(string path, CancellationToken ct);
}
