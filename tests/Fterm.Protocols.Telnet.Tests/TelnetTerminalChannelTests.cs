using System.Net;
using System.Net.Sockets;
using System.Text;
using Fterm.Protocols.Telnet;
using Xunit;

namespace Fterm.Protocols.Telnet.Tests;

public class TelnetTerminalChannelTests
{
    /// <summary>
    /// 軽量 Telnet サーバ: 接続が来たら指定したバイト列を 1 回送って閉じる。
    /// クライアントから書き戻されたバイトは Received に蓄積する。
    /// </summary>
    private sealed class TelnetServerStub : IDisposable
    {
        private readonly TcpListener _listener;
        public int Port => ((IPEndPoint)_listener.LocalEndpoint).Port;
        public List<byte> Received { get; } = [];

        public TelnetServerStub(byte[] greeting)
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            _ = Task.Run(async () =>
            {
                using var client = await _listener.AcceptTcpClientAsync();
                using var stream = client.GetStream();
                await stream.WriteAsync(greeting);
                await stream.FlushAsync();
                var buf = new byte[256];
                var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                try
                {
                    while (!cts.IsCancellationRequested)
                    {
                        var n = await stream.ReadAsync(buf.AsMemory(), cts.Token);
                        if (n <= 0) break;
                        lock (Received) Received.AddRange(buf.AsSpan(0, n).ToArray());
                    }
                }
                catch { /* timeout / closed */ }
            });
        }

        public void Dispose() => _listener.Stop();
    }

    [Fact]
    public async Task Strips_IAC_options_and_yields_clean_data()
    {
        // IAC WILL ECHO (0xFF 0xFB 0x01) を埋め込み、その前後に通常テキストを流す
        var greeting = new byte[] { (byte)'h', (byte)'i', 0xFF, 0xFB, 0x01, (byte)'!' };
        using var server = new TelnetServerStub(greeting);

        await using var ch = new TelnetTerminalChannel(new TelnetTerminalChannelOptions
        {
            Host = "127.0.0.1", Port = server.Port,
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await ch.ConnectAsync(cts.Token);

        var received = new List<byte>();
        var enumerator = ch.ReadAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        try
        {
            while (received.Count < 3 && await enumerator.MoveNextAsync())
            {
                received.AddRange(enumerator.Current.ToArray());
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        Assert.Equal("hi!", Encoding.ASCII.GetString(received.ToArray()));

        // クライアントは IAC DONT ECHO を返したはず
        await Task.Delay(150);
        lock (server.Received)
        {
            Assert.Contains((byte)0xFF, server.Received);
        }
    }

    [Fact]
    public async Task Doubles_IAC_in_outgoing_data()
    {
        using var server = new TelnetServerStub(Array.Empty<byte>());
        await using var ch = new TelnetTerminalChannel(new TelnetTerminalChannelOptions
        {
            Host = "127.0.0.1", Port = server.Port,
        });
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
        await ch.ConnectAsync(cts.Token);

        await ch.WriteAsync(new byte[] { 0x41, 0xFF, 0x42 }, cts.Token);
        await Task.Delay(150);
        lock (server.Received)
        {
            Assert.Equal(new byte[] { 0x41, 0xFF, 0xFF, 0x42 }, server.Received.ToArray());
        }
    }
}
