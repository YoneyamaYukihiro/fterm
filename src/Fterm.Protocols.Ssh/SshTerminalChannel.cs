using System.Threading.Channels;
using Fterm.Core.Sessions;
using Renci.SshNet;
using Renci.SshNet.Common;

namespace Fterm.Protocols.Ssh;

public sealed class SshTerminalChannelOptions
{
    public required string Host { get; init; }
    public int Port { get; init; } = 22;
    public required string Username { get; init; }
    public string? Password { get; init; }
    public string? PrivateKeyPem { get; init; }
    public string? PrivateKeyPassphrase { get; init; }
    public string TerminalName { get; init; } = "xterm-256color";
    public int InitialCols { get; init; } = 100;
    public int InitialRows { get; init; } = 30;
}

/// <summary>
/// SSH.NET の <see cref="ShellStream"/> を使った PTY ベースの ITerminalChannel 実装。
/// </summary>
public sealed class SshTerminalChannel : ITerminalChannel
{
    private readonly SshTerminalChannelOptions _options;
    private readonly KnownHostsStore _knownHosts;
    private readonly IHostKeyPolicy _hostKeyPolicy;

    private SshClient? _client;
    private ShellStream? _stream;
    private readonly Channel<ReadOnlyMemory<byte>> _rx = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    private CancellationTokenSource? _readCts;

    public event EventHandler<DisconnectedEventArgs>? Disconnected;

    public SshTerminalChannel(SshTerminalChannelOptions options, KnownHostsStore knownHosts, IHostKeyPolicy hostKeyPolicy)
    {
        _options = options;
        _knownHosts = knownHosts;
        _hostKeyPolicy = hostKeyPolicy;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        var authMethods = BuildAuthMethods().ToArray();
        var info = new ConnectionInfo(_options.Host, _options.Port, _options.Username, authMethods);

        _client = new SshClient(info);

        Exception? hostKeyFailure = null;
        SshHostKeyValidator.Attach(_client, _options.Host, _options.Port, _knownHosts, _hostKeyPolicy, ct,
            ex => hostKeyFailure = ex);

        try
        {
            await Task.Run(() => _client.Connect(), ct);
        }
        catch (Exception)
        {
            if (hostKeyFailure is not null) throw hostKeyFailure;
            throw;
        }
        if (hostKeyFailure is not null) throw hostKeyFailure;

        var terminalMode = new Dictionary<TerminalModes, uint>();
        _stream = _client.CreateShellStream(
            _options.TerminalName,
            (uint)_options.InitialCols,
            (uint)_options.InitialRows,
            800, 600,
            4096,
            terminalMode);

        _readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => ReadLoopAsync(_readCts.Token), _readCts.Token);

        _client.ErrorOccurred += (_, e) =>
        {
            Disconnected?.Invoke(this, new DisconnectedEventArgs(e.Exception.Message));
        };
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        if (_stream is null) return;
        var buffer = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var n = await _stream.ReadAsync(buffer.AsMemory(), ct);
                if (n <= 0) break;
                var copy = new byte[n];
                Buffer.BlockCopy(buffer, 0, copy, 0, n);
                await _rx.Writer.WriteAsync(copy, ct);
            }
        }
        catch (OperationCanceledException) { /* normal */ }
        catch (Exception ex)
        {
            Disconnected?.Invoke(this, new DisconnectedEventArgs(ex.Message));
        }
        finally
        {
            _rx.Writer.TryComplete();
        }
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (_stream is null) throw new InvalidOperationException("Not connected");
        await _stream.WriteAsync(data, ct);
        await _stream.FlushAsync(ct);
    }

    public IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct) =>
        _rx.Reader.ReadAllAsync(ct);

    public Task ResizeAsync(int cols, int rows, CancellationToken ct)
    {
        // SSH.NET 2024.x の ShellStream は外部からの window-change を直接公開していない。
        // 公式 API が拡張されるまでは新規 stream を張り直す方針も取りうるが、現状はノーオペ。
        // TODO(M4+): WindowChange 送信のための内部 Channel アクセス手段を追加。
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_readCts is not null) await _readCts.CancelAsync();
        }
        catch { /* ignore */ }
        _stream?.Dispose();
        _client?.Dispose();
        _rx.Writer.TryComplete();
        Disconnected?.Invoke(this, new DisconnectedEventArgs("disposed"));
        await Task.CompletedTask;
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
