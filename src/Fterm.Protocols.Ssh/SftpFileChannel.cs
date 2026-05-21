using System.Runtime.CompilerServices;
using Fterm.Core.Sessions;
using Renci.SshNet;

namespace Fterm.Protocols.Ssh;

public sealed class SftpFileChannelOptions
{
    public required string Host { get; init; }
    public int Port { get; init; } = 22;
    public required string Username { get; init; }
    public string? Password { get; init; }
    public string? PrivateKeyPem { get; init; }
    public string? PrivateKeyPassphrase { get; init; }
    /// <summary>ProxyJump 用の踏み台チェーン (任意)。</summary>
    public IReadOnlyList<SshProxyChainBuilder.HopSpec>? ProxyHops { get; init; }
}

/// <summary>
/// SSH.NET の <see cref="SftpClient"/> を使った <see cref="IFileChannel"/> 実装。
/// </summary>
public sealed class SftpFileChannel : IFileChannel
{
    private readonly SftpFileChannelOptions _options;
    private readonly KnownHostsStore _knownHosts;
    private readonly IHostKeyPolicy _hostKeyPolicy;

    private SftpClient? _client;
    private SshProxyChain? _proxyChain;

    public SftpFileChannel(SftpFileChannelOptions options, KnownHostsStore knownHosts, IHostKeyPolicy hostKeyPolicy)
    {
        _options = options;
        _knownHosts = knownHosts;
        _hostKeyPolicy = hostKeyPolicy;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        string connectHost = _options.Host;
        int connectPort = _options.Port;
        if (_options.ProxyHops is { Count: > 0 } hops)
        {
            var builder = new SshProxyChainBuilder(_knownHosts, _hostKeyPolicy);
            _proxyChain = await Task.Run(() => builder.Build(hops, _options.Host, _options.Port, ct), ct);
            connectHost = _proxyChain.LocalEndpoint.Host;
            connectPort = _proxyChain.LocalEndpoint.Port;
        }

        var auth = BuildAuthMethods().ToArray();
        var info = new ConnectionInfo(connectHost, connectPort, _options.Username, auth);
        _client = new SftpClient(info);

        Exception? hostKeyFailure = null;
        SshHostKeyValidator.Attach(_client, _options.Host, _options.Port, _knownHosts, _hostKeyPolicy, ct,
            ex => hostKeyFailure = ex);

        try
        {
            await Task.Run(() => _client.Connect(), ct);
        }
        catch
        {
            if (hostKeyFailure is not null) throw hostKeyFailure;
            throw;
        }
        if (hostKeyFailure is not null) throw hostKeyFailure;
    }

    public async IAsyncEnumerable<RemoteEntry> ListAsync(string path, [EnumeratorCancellation] CancellationToken ct)
    {
        EnsureConnected();
        // SftpClient.ListDirectory は同期 API。短時間で済むので Task.Run で非同期化する。
        var entries = await Task.Run(() => _client!.ListDirectory(path).ToList(), ct);
        foreach (var f in entries)
        {
            if (f.Name is "." or "..") continue;
            yield return new RemoteEntry(
                f.Name,
                f.FullName,
                f.IsDirectory ? 0 : f.Length,
                f.LastWriteTime,
                f.IsDirectory,
                f.UserId.ToString(),
                f.GroupId.ToString(),
                FormatPermissions(f));
        }
    }

    private static string FormatPermissions(Renci.SshNet.Sftp.ISftpFile f)
    {
        var d = f.IsDirectory ? 'd' : '-';
        char R(bool b, char c) => b ? c : '-';
        return $"{d}{R(f.OwnerCanRead, 'r')}{R(f.OwnerCanWrite, 'w')}{R(f.OwnerCanExecute, 'x')}{R(f.GroupCanRead, 'r')}{R(f.GroupCanWrite, 'w')}{R(f.GroupCanExecute, 'x')}{R(f.OthersCanRead, 'r')}{R(f.OthersCanWrite, 'w')}{R(f.OthersCanExecute, 'x')}";
    }

    public Task<Stream> OpenReadAsync(string path, CancellationToken ct)
    {
        EnsureConnected();
        return Task.FromResult<Stream>(_client!.OpenRead(path));
    }

    public Task<Stream> OpenWriteAsync(string path, CancellationToken ct)
    {
        EnsureConnected();
        return Task.FromResult<Stream>(_client!.Create(path));
    }

    public async Task MakeDirectoryAsync(string path, CancellationToken ct)
    {
        EnsureConnected();
        await Task.Run(() => _client!.CreateDirectory(path), ct);
    }

    public async Task DeleteAsync(string path, bool recursive, CancellationToken ct)
    {
        EnsureConnected();
        await Task.Run(() =>
        {
            var attrs = _client!.GetAttributes(path);
            if (attrs.IsDirectory)
            {
                if (recursive) DeleteRecursive(path);
                else _client.DeleteDirectory(path);
            }
            else
            {
                _client.DeleteFile(path);
            }
        }, ct);
    }

    private void DeleteRecursive(string path)
    {
        foreach (var child in _client!.ListDirectory(path))
        {
            if (child.Name is "." or "..") continue;
            if (child.IsDirectory) DeleteRecursive(child.FullName);
            else _client.DeleteFile(child.FullName);
        }
        _client.DeleteDirectory(path);
    }

    public async Task RenameAsync(string from, string to, CancellationToken ct)
    {
        EnsureConnected();
        await Task.Run(() => _client!.RenameFile(from, to), ct);
    }

    public Task<bool> ExistsAsync(string path, CancellationToken ct)
    {
        EnsureConnected();
        return Task.Run(() => _client!.Exists(path), ct);
    }

    public Task<long> GetSizeAsync(string path, CancellationToken ct)
    {
        EnsureConnected();
        return Task.Run<long>(() =>
        {
            try
            {
                var attrs = _client!.GetAttributes(path);
                return attrs.IsDirectory ? -1 : attrs.Size;
            }
            catch
            {
                return -1;
            }
        }, ct);
    }

    public Task<Stream> OpenAppendAsync(string path, CancellationToken ct)
    {
        EnsureConnected();
        return Task.FromResult<Stream>(_client!.Open(path, FileMode.Append, FileAccess.Write));
    }

    public ValueTask DisposeAsync()
    {
        _client?.Dispose();
        _proxyChain?.Dispose();
        return ValueTask.CompletedTask;
    }

    private void EnsureConnected()
    {
        if (_client is null || !_client.IsConnected)
            throw new InvalidOperationException("SFTP client is not connected");
    }

    private List<AuthenticationMethod> BuildAuthMethods()
    {
        var list = new List<AuthenticationMethod>();
        if (!string.IsNullOrEmpty(_options.PrivateKeyPem))
        {
            using var pemStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(_options.PrivateKeyPem));
            var key = string.IsNullOrEmpty(_options.PrivateKeyPassphrase)
                ? new PrivateKeyFile(pemStream)
                : new PrivateKeyFile(pemStream, _options.PrivateKeyPassphrase);
            list.Add(new PrivateKeyAuthenticationMethod(_options.Username, key));
        }
        if (!string.IsNullOrEmpty(_options.Password))
        {
            list.Add(new PasswordAuthenticationMethod(_options.Username, _options.Password));
        }
        if (list.Count == 0)
        {
            list.Add(new NoneAuthenticationMethod(_options.Username));
        }
        return list;
    }
}
