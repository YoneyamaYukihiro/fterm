using System.IO.Ports;
using System.Threading.Channels;
using Fterm.Core.Sessions;

namespace Fterm.Protocols.Serial;

public sealed class SerialTerminalChannelOptions
{
    public required string PortName { get; init; }
    public int BaudRate { get; init; } = 115200;
    public int DataBits { get; init; } = 8;
    public Parity Parity { get; init; } = Parity.None;
    public StopBits StopBits { get; init; } = StopBits.One;
    public Handshake Handshake { get; init; } = Handshake.None;
}

/// <summary>
/// System.IO.Ports.SerialPort を ITerminalChannel に適合させる。
/// </summary>
public sealed class SerialTerminalChannel : ITerminalChannel
{
    private readonly SerialTerminalChannelOptions _options;
    private SerialPort? _port;
    private readonly Channel<ReadOnlyMemory<byte>> _rx = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    private CancellationTokenSource? _readCts;

    public event EventHandler<DisconnectedEventArgs>? Disconnected;

    public SerialTerminalChannel(SerialTerminalChannelOptions options)
    {
        _options = options;
    }

    public Task ConnectAsync(CancellationToken ct)
    {
        _port = new SerialPort(_options.PortName, _options.BaudRate, _options.Parity, _options.DataBits, _options.StopBits)
        {
            Handshake = _options.Handshake,
            ReadTimeout = SerialPort.InfiniteTimeout,
            WriteTimeout = 5000,
        };
        _port.Open();

        _readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => ReadLoopAsync(_readCts.Token), _readCts.Token);
        return Task.CompletedTask;
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        if (_port is null) return;
        var buf = new byte[4096];
        try
        {
            while (!ct.IsCancellationRequested && _port.IsOpen)
            {
                var n = await _port.BaseStream.ReadAsync(buf.AsMemory(), ct);
                if (n <= 0) break;
                var copy = new byte[n];
                Buffer.BlockCopy(buf, 0, copy, 0, n);
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
        if (_port is null || !_port.IsOpen) throw new InvalidOperationException("Not connected");
        await _port.BaseStream.WriteAsync(data, ct);
    }

    public IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct) =>
        _rx.Reader.ReadAllAsync(ct);

    public Task ResizeAsync(int cols, int rows, CancellationToken ct) => Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_readCts is not null) await _readCts.CancelAsync();
        }
        catch { /* ignore */ }
        try { _port?.Close(); } catch { /* ignore */ }
        _port?.Dispose();
        _rx.Writer.TryComplete();
        Disconnected?.Invoke(this, new DisconnectedEventArgs("disposed"));
    }
}
