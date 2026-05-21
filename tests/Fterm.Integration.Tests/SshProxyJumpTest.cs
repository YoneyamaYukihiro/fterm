using System.Text;
using Fterm.Protocols.Ssh;
using Xunit;

namespace Fterm.Integration.Tests;

/// <summary>
/// 同一 sshd を「踏み台 → 同じ sshd」と 2 段経由する自己ループ ProxyJump テスト。
/// 環境変数 FTERM_SSH_HOST / PORT / USER / PASS が必要。
/// </summary>
public class SshProxyJumpTest
{
    private sealed class AcceptAll : IHostKeyPolicy
    {
        public Task<HostKeyDecision> DecideAsync(HostKeyPrompt p, CancellationToken ct) =>
            Task.FromResult(HostKeyDecision.Accept);
    }

    [Fact]
    public async Task Connect_via_one_hop_and_echo()
    {
        var host = Environment.GetEnvironmentVariable("FTERM_SSH_HOST");
        var portStr = Environment.GetEnvironmentVariable("FTERM_SSH_PORT") ?? "22";
        var user = Environment.GetEnvironmentVariable("FTERM_SSH_USER");
        var pass = Environment.GetEnvironmentVariable("FTERM_SSH_PASS");
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass)) return;
        var port = int.Parse(portStr);

        var knownHosts = new KnownHostsStore(Path.Combine(Path.GetTempPath(), $"kh-{Guid.NewGuid():N}"));
        var hops = new List<SshProxyChainBuilder.HopSpec>
        {
            new(host, port, user, pass, null, null),
        };

        await using var channel = new SshTerminalChannel(new SshTerminalChannelOptions
        {
            Host = host, Port = port, Username = user, Password = pass,
            ProxyHops = hops,
        }, knownHosts, new AcceptAll());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await channel.ConnectAsync(cts.Token);
        await channel.WriteAsync(Encoding.UTF8.GetBytes("echo PROXY-OK\n"), cts.Token);

        var output = new StringBuilder();
        var enumerator = channel.ReadAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        try
        {
            while (await enumerator.MoveNextAsync())
            {
                output.Append(Encoding.UTF8.GetString(enumerator.Current.Span));
                if (output.ToString().Contains("PROXY-OK")) break;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }
        Assert.Contains("PROXY-OK", output.ToString());
    }
}
