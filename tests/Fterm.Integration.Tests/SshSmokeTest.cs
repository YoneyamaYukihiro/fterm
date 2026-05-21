using System.Text;
using Fterm.Protocols.Ssh;
using Xunit;

namespace Fterm.Integration.Tests;

/// <summary>
/// 環境変数 FTERM_SSH_HOST / FTERM_SSH_USER / FTERM_SSH_PASS / FTERM_SSH_PORT を
/// セットしたときだけ実行される SSH スモークテスト。CI/開発機で sshd が用意できる
/// 環境でのみグリーンになる。
/// </summary>
public class SshSmokeTest
{
    private static (string host, int port, string user, string pass)? FromEnv()
    {
        var host = Environment.GetEnvironmentVariable("FTERM_SSH_HOST");
        var user = Environment.GetEnvironmentVariable("FTERM_SSH_USER");
        var pass = Environment.GetEnvironmentVariable("FTERM_SSH_PASS");
        var portStr = Environment.GetEnvironmentVariable("FTERM_SSH_PORT") ?? "22";
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass)) return null;
        return (host, int.Parse(portStr), user, pass);
    }

    private sealed class AcceptAllHostKeyPolicy : IHostKeyPolicy
    {
        public Task<HostKeyDecision> DecideAsync(HostKeyPrompt prompt, CancellationToken ct) =>
            Task.FromResult(HostKeyDecision.Accept);
    }

    [Fact]
    public async Task Connect_run_echo_and_receive_output()
    {
        var env = FromEnv();
        if (env is null) return; // 環境未設定なら静かにスキップ
        var (host, port, user, pass) = env.Value;

        var knownHosts = new KnownHostsStore(Path.Combine(Path.GetTempPath(), $"known-{Guid.NewGuid():N}"));
        var channel = new SshTerminalChannel(new SshTerminalChannelOptions
        {
            Host = host, Port = port, Username = user, Password = pass,
        }, knownHosts, new AcceptAllHostKeyPolicy());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await channel.ConnectAsync(cts.Token);

        await channel.WriteAsync(Encoding.UTF8.GetBytes("echo FTERM-OK\n"), cts.Token);

        var output = new StringBuilder();
        var enumerator = channel.ReadAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        try
        {
            while (await enumerator.MoveNextAsync())
            {
                output.Append(Encoding.UTF8.GetString(enumerator.Current.Span));
                if (output.ToString().Contains("FTERM-OK")) break;
            }
        }
        finally
        {
            await enumerator.DisposeAsync();
        }

        await channel.DisposeAsync();
        Assert.Contains("FTERM-OK", output.ToString());
    }
}
