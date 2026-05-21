using System.Net;
using Renci.SshNet;

namespace Fterm.Protocols.Ssh;

/// <summary>
/// SSH 多段（ProxyJump）チェーン。最後のホップ上に開いた ForwardedPortLocal を通じて
/// 最終ターゲットに到達するためのローカル endpoint を提供する。
/// Dispose 時に踏み台 SSH 接続群を逆順に閉じる。
/// </summary>
public sealed class SshProxyChain : IDisposable
{
    private readonly List<SshClient> _clients = [];
    private readonly List<ForwardedPortLocal> _ports = [];

    /// <summary>このチェーンを介して最終ターゲットに到達するためのローカル endpoint。</summary>
    public (string Host, int Port) LocalEndpoint { get; internal set; }

    internal void AddHop(SshClient client) => _clients.Add(client);
    internal void AddPort(ForwardedPortLocal port) => _ports.Add(port);

    public void Dispose()
    {
        foreach (var p in _ports)
        {
            try { p.Stop(); } catch { /* ignore */ }
            try { p.Dispose(); } catch { /* ignore */ }
        }
        for (var i = _clients.Count - 1; i >= 0; i--)
        {
            try { _clients[i].Disconnect(); } catch { /* ignore */ }
            try { _clients[i].Dispose(); } catch { /* ignore */ }
        }
    }
}

public sealed class SshProxyChainBuilder
{
    private readonly KnownHostsStore _knownHosts;
    private readonly IHostKeyPolicy _hostKeyPolicy;

    public SshProxyChainBuilder(KnownHostsStore knownHosts, IHostKeyPolicy hostKeyPolicy)
    {
        _knownHosts = knownHosts;
        _hostKeyPolicy = hostKeyPolicy;
    }

    public record HopSpec(string Host, int Port, string Username, string? Password, string? PrivateKeyPem, string? PrivateKeyPassphrase);

    /// <summary>
    /// hops を順に SSH 接続し、最後のクライアント上に finalHost:finalPort への
    /// ローカル転送ポートを張る。チェーンが空の場合はそのまま (finalHost, finalPort) を返す。
    /// </summary>
    public SshProxyChain Build(IReadOnlyList<HopSpec> hops, string finalHost, int finalPort, CancellationToken ct)
    {
        var chain = new SshProxyChain { LocalEndpoint = (finalHost, finalPort) };
        if (hops.Count == 0) return chain;

        try
        {
            var nextConnectHost = hops[0].Host;
            var nextConnectPort = hops[0].Port;

            for (var i = 0; i < hops.Count; i++)
            {
                var hop = hops[i];
                var auth = BuildAuth(hop).ToArray();
                // 1 段目以降は前段のローカル転送ポートに接続する
                var info = new ConnectionInfo(nextConnectHost, nextConnectPort, hop.Username, auth);
                var client = new SshClient(info);

                Exception? hostKeyFailure = null;
                // 鍵検証は論理的なホスト名で行う (踏み台越しでも改ざん検出可)
                SshHostKeyValidator.Attach(client, hop.Host, hop.Port, _knownHosts, _hostKeyPolicy, ct,
                    ex => hostKeyFailure = ex);
                client.Connect();
                if (hostKeyFailure is not null) throw hostKeyFailure;
                chain.AddHop(client);

                // 次の宛先 = 最後のホップなら最終ターゲット、それ以外は次のホップ
                string fwdHost = i + 1 < hops.Count ? hops[i + 1].Host : finalHost;
                int fwdPort = i + 1 < hops.Count ? hops[i + 1].Port : finalPort;

                var localPort = (uint)PickFreeLocalPort();
                var forward = new ForwardedPortLocal("127.0.0.1", localPort, fwdHost, (uint)fwdPort);
                client.AddForwardedPort(forward);
                forward.Start();
                chain.AddPort(forward);

                nextConnectHost = "127.0.0.1";
                nextConnectPort = (int)localPort;
            }

            chain.LocalEndpoint = (nextConnectHost, nextConnectPort);
            return chain;
        }
        catch
        {
            chain.Dispose();
            throw;
        }
    }

    private static int PickFreeLocalPort()
    {
        var listener = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static List<AuthenticationMethod> BuildAuth(HopSpec hop)
    {
        var list = new List<AuthenticationMethod>();
        if (!string.IsNullOrEmpty(hop.PrivateKeyPem))
        {
            using var pemStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(hop.PrivateKeyPem));
            var key = string.IsNullOrEmpty(hop.PrivateKeyPassphrase)
                ? new PrivateKeyFile(pemStream)
                : new PrivateKeyFile(pemStream, hop.PrivateKeyPassphrase);
            list.Add(new PrivateKeyAuthenticationMethod(hop.Username, key));
        }
        if (!string.IsNullOrEmpty(hop.Password))
        {
            list.Add(new PasswordAuthenticationMethod(hop.Username, hop.Password));
        }
        if (list.Count == 0)
        {
            list.Add(new NoneAuthenticationMethod(hop.Username));
        }
        return list;
    }
}
