using System.Runtime.CompilerServices;
using FluentFTP;
using Fterm.Core.Sessions;

namespace Fterm.Protocols.Ftp;

public enum FtpSecurityMode
{
    Plain,
    ExplicitTls,
    ImplicitTls,
}

public sealed class FtpFileChannelOptions
{
    public required string Host { get; init; }
    public int Port { get; init; } = 21;
    public required string Username { get; init; }
    public string? Password { get; init; }
    public FtpSecurityMode Security { get; init; } = FtpSecurityMode.Plain;
    public bool Passive { get; init; } = true;
    /// <summary>自己署名 TLS 証明書を許可するか（テスト用途、既定で false）。</summary>
    public bool AllowSelfSignedCertificate { get; init; }
}

/// <summary>
/// FluentFTP ベースの <see cref="IFileChannel"/> 実装。FTP / FTPS (明示・暗黙) に対応。
/// </summary>
public sealed class FtpFileChannel : IFileChannel
{
    private readonly FtpFileChannelOptions _options;
    private AsyncFtpClient? _client;

    public FtpFileChannel(FtpFileChannelOptions options)
    {
        _options = options;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        var config = new FtpConfig
        {
            DataConnectionType = _options.Passive ? FtpDataConnectionType.AutoPassive : FtpDataConnectionType.AutoActive,
            EncryptionMode = _options.Security switch
            {
                FtpSecurityMode.ExplicitTls => FtpEncryptionMode.Explicit,
                FtpSecurityMode.ImplicitTls => FtpEncryptionMode.Implicit,
                _ => FtpEncryptionMode.None,
            },
            ValidateAnyCertificate = _options.AllowSelfSignedCertificate,
        };

        _client = new AsyncFtpClient(_options.Host, _options.Username, _options.Password ?? "", _options.Port, config);
        await _client.Connect(ct);
    }

    public async IAsyncEnumerable<RemoteEntry> ListAsync(string path, [EnumeratorCancellation] CancellationToken ct)
    {
        EnsureConnected();
        var items = await _client!.GetListing(path, ct);
        foreach (var item in items)
        {
            if (item.Name is "." or "..") continue;
            yield return new RemoteEntry(
                item.Name,
                item.FullName,
                item.Type == FtpObjectType.File ? item.Size : 0,
                item.Modified == DateTime.MinValue ? DateTimeOffset.MinValue : new DateTimeOffset(item.Modified),
                item.Type == FtpObjectType.Directory,
                item.RawOwner,
                item.RawGroup,
                item.RawPermissions);
        }
    }

    public async Task<Stream> OpenReadAsync(string path, CancellationToken ct)
    {
        EnsureConnected();
        return await _client!.OpenRead(path, FtpDataType.Binary, 0, true, ct);
    }

    public async Task<Stream> OpenWriteAsync(string path, CancellationToken ct)
    {
        EnsureConnected();
        return await _client!.OpenWrite(path, FtpDataType.Binary, true, ct);
    }

    public async Task MakeDirectoryAsync(string path, CancellationToken ct)
    {
        EnsureConnected();
        await _client!.CreateDirectory(path, ct);
    }

    public async Task DeleteAsync(string path, bool recursive, CancellationToken ct)
    {
        EnsureConnected();
        if (await _client!.DirectoryExists(path, ct))
        {
            if (recursive)
            {
                await _client.DeleteDirectory(path, FtpListOption.Recursive, ct);
            }
            else
            {
                await _client.DeleteDirectory(path, ct);
            }
        }
        else
        {
            await _client.DeleteFile(path, ct);
        }
    }

    public async Task RenameAsync(string from, string to, CancellationToken ct)
    {
        EnsureConnected();
        await _client!.Rename(from, to, ct);
    }

    public async Task<bool> ExistsAsync(string path, CancellationToken ct)
    {
        EnsureConnected();
        return await _client!.FileExists(path, ct) || await _client.DirectoryExists(path, ct);
    }

    public async Task<long> GetSizeAsync(string path, CancellationToken ct)
    {
        EnsureConnected();
        try
        {
            if (await _client!.DirectoryExists(path, ct)) return -1;
            return await _client.GetFileSize(path, -1, ct);
        }
        catch
        {
            return -1;
        }
    }

    public async Task<Stream> OpenAppendAsync(string path, CancellationToken ct)
    {
        EnsureConnected();
        return await _client!.OpenAppend(path, FtpDataType.Binary, true, ct);
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            try { await _client.Disconnect(); } catch { /* ignore */ }
            _client.Dispose();
        }
    }

    private void EnsureConnected()
    {
        if (_client is null || !_client.IsConnected)
            throw new InvalidOperationException("FTP client is not connected");
    }
}
