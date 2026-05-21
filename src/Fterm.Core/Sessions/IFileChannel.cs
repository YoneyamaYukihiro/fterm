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
}
