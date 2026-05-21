using System.Threading.Channels;

namespace Fterm.Core.Sessions;

/// <summary>
/// 開発用のダミー実装。書き込んだバイト列をそのまま読み込み側に流す。
/// M1 で UI 結線確認に使用する。
/// </summary>
public sealed class EchoTerminalChannel : ITerminalChannel
{
    private readonly Channel<ReadOnlyMemory<byte>> _channel = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    private bool _connected;

    public event EventHandler<DisconnectedEventArgs>? Disconnected;

    public Task ConnectAsync(CancellationToken ct)
    {
        _connected = true;
        var banner = "fterm echo channel ready\r\n$ "u8.ToArray();
        _channel.Writer.TryWrite(banner);
        return Task.CompletedTask;
    }

    public ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (!_connected) throw new InvalidOperationException("Not connected");
        return _channel.Writer.WriteAsync(data.ToArray(), ct);
    }

    public IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct)
        => _channel.Reader.ReadAllAsync(ct);

    public Task ResizeAsync(int cols, int rows, CancellationToken ct) => Task.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _connected = false;
        _channel.Writer.TryComplete();
        Disconnected?.Invoke(this, new DisconnectedEventArgs("disposed"));
        return ValueTask.CompletedTask;
    }
}
