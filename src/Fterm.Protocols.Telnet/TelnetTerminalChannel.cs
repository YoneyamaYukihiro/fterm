using System.Net.Sockets;
using System.Threading.Channels;
using Fterm.Core.Sessions;

namespace Fterm.Protocols.Telnet;

public sealed class TelnetTerminalChannelOptions
{
    public required string Host { get; init; }
    public int Port { get; init; } = 23;
}

/// <summary>
/// 生 TCP 上で Telnet IAC (RFC 854) 交渉を最低限こなす ITerminalChannel。
/// 既定方針: DO/WILL に対しては全て WONT/DONT で応答し、データだけを上位レイヤに流す。
/// （本格的な端末タイプ / NAWS 交渉は M6+ で拡張）
/// </summary>
public sealed class TelnetTerminalChannel : ITerminalChannel
{
    private const byte IAC = 0xFF;
    private const byte DONT = 0xFE;
    private const byte DO = 0xFD;
    private const byte WONT = 0xFC;
    private const byte WILL = 0xFB;
    private const byte SB = 0xFA;
    private const byte SE = 0xF0;

    private readonly TelnetTerminalChannelOptions _options;
    private TcpClient? _tcp;
    private NetworkStream? _stream;
    private readonly Channel<ReadOnlyMemory<byte>> _rx = Channel.CreateUnbounded<ReadOnlyMemory<byte>>();
    private CancellationTokenSource? _readCts;

    public event EventHandler<DisconnectedEventArgs>? Disconnected;

    public TelnetTerminalChannel(TelnetTerminalChannelOptions options)
    {
        _options = options;
    }

    public async Task ConnectAsync(CancellationToken ct)
    {
        _tcp = new TcpClient();
        await _tcp.ConnectAsync(_options.Host, _options.Port, ct);
        _stream = _tcp.GetStream();
        _readCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _ = Task.Run(() => ReadLoopAsync(_readCts.Token), _readCts.Token);
    }

    private async Task ReadLoopAsync(CancellationToken ct)
    {
        if (_stream is null) return;
        var buf = new byte[4096];
        var cleanBuf = new List<byte>(4096);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var n = await _stream.ReadAsync(buf.AsMemory(), ct);
                if (n <= 0) break;

                cleanBuf.Clear();
                var i = 0;
                while (i < n)
                {
                    if (buf[i] != IAC)
                    {
                        cleanBuf.Add(buf[i]);
                        i++;
                        continue;
                    }
                    // IAC <cmd> ... を消費
                    if (i + 1 >= n) break;
                    var cmd = buf[i + 1];
                    switch (cmd)
                    {
                        case IAC: // 0xFF 0xFF = データの 0xFF
                            cleanBuf.Add(IAC);
                            i += 2;
                            break;
                        case DO:
                        case DONT:
                        case WILL:
                        case WONT:
                            if (i + 2 >= n) { i = n; break; }
                            var option = buf[i + 2];
                            await RespondAsync(cmd, option, ct);
                            i += 3;
                            break;
                        case SB:
                            // sub-negotiation: SE まで読み飛ばす
                            var j = i + 2;
                            while (j + 1 < n && !(buf[j] == IAC && buf[j + 1] == SE)) j++;
                            i = j + 2;
                            break;
                        default:
                            i += 2;
                            break;
                    }
                }
                if (cleanBuf.Count > 0)
                {
                    var copy = cleanBuf.ToArray();
                    await _rx.Writer.WriteAsync(copy, ct);
                }
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

    private async Task RespondAsync(byte cmd, byte option, CancellationToken ct)
    {
        if (_stream is null) return;
        // すべてのオプションを拒否する：DO -> WONT, WILL -> DONT, DONT/WONT は応答不要
        byte? reply = cmd switch
        {
            DO => WONT,
            WILL => DONT,
            _ => null,
        };
        if (reply is not null)
        {
            await _stream.WriteAsync(new byte[] { IAC, reply.Value, option }, ct);
        }
    }

    public async ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct)
    {
        if (_stream is null) throw new InvalidOperationException("Not connected");
        // データ中の 0xFF を二重化する必要がある (RFC 854)
        if (!data.Span.Contains(IAC))
        {
            await _stream.WriteAsync(data, ct);
        }
        else
        {
            var src = data.Span;
            var dst = new byte[src.Length * 2];
            var w = 0;
            for (var i = 0; i < src.Length; i++)
            {
                dst[w++] = src[i];
                if (src[i] == IAC) dst[w++] = IAC;
            }
            await _stream.WriteAsync(dst.AsMemory(0, w), ct);
        }
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
        _stream?.Dispose();
        _tcp?.Dispose();
        _rx.Writer.TryComplete();
        Disconnected?.Invoke(this, new DisconnectedEventArgs("disposed"));
    }
}
