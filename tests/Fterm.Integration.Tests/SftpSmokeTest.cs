using System.Text;
using Fterm.Protocols.Ssh;
using Xunit;

namespace Fterm.Integration.Tests;

public class SftpSmokeTest
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
    public async Task List_upload_download_delete_roundtrip()
    {
        var env = FromEnv();
        if (env is null) return;
        var (host, port, user, pass) = env.Value;

        var knownHosts = new KnownHostsStore(Path.Combine(Path.GetTempPath(), $"kh-{Guid.NewGuid():N}"));
        await using var sftp = new SftpFileChannel(new SftpFileChannelOptions
        {
            Host = host, Port = port, Username = user, Password = pass,
        }, knownHosts, new AcceptAllHostKeyPolicy());

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await sftp.ConnectAsync(cts.Token);

        // Determine home directory by listing "." which SSH.NET resolves to user home for SFTP
        var entries = new List<string>();
        await foreach (var e in sftp.ListAsync(".", cts.Token)) entries.Add(e.Name);
        // 通常 sshd の chroot 等が無ければ少なくともディレクトリは listable
        Assert.NotNull(entries);

        // Upload
        var remoteName = $"fterm-test-{Guid.NewGuid():N}.txt";
        var payload = Encoding.UTF8.GetBytes("hello from fterm test\n");
        await using (var w = await sftp.OpenWriteAsync(remoteName, cts.Token))
        {
            await w.WriteAsync(payload, cts.Token);
        }

        // Download
        await using (var r = await sftp.OpenReadAsync(remoteName, cts.Token))
        {
            using var ms = new MemoryStream();
            await r.CopyToAsync(ms, cts.Token);
            Assert.Equal(payload, ms.ToArray());
        }

        // Delete
        await sftp.DeleteAsync(remoteName, recursive: false, cts.Token);
    }
}
