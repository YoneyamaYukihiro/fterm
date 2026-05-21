using System.Text;
using Fterm.Protocols.Ftp;
using Xunit;

namespace Fterm.Integration.Tests;

public class FtpSmokeTest
{
    private static (string host, int port, string user, string pass)? FromEnv()
    {
        var host = Environment.GetEnvironmentVariable("FTERM_FTP_HOST");
        var user = Environment.GetEnvironmentVariable("FTERM_FTP_USER");
        var pass = Environment.GetEnvironmentVariable("FTERM_FTP_PASS");
        var portStr = Environment.GetEnvironmentVariable("FTERM_FTP_PORT") ?? "21";
        if (string.IsNullOrEmpty(host) || string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass)) return null;
        return (host, int.Parse(portStr), user, pass);
    }

    [Fact]
    public async Task List_upload_download_delete_roundtrip()
    {
        var env = FromEnv();
        if (env is null) return;
        var (host, port, user, pass) = env.Value;

        await using var ftp = new FtpFileChannel(new FtpFileChannelOptions
        {
            Host = host, Port = port, Username = user, Password = pass, Passive = true,
        });

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        await ftp.ConnectAsync(cts.Token);

        var name = $"fterm-test-{Guid.NewGuid():N}.txt";
        var payload = Encoding.UTF8.GetBytes("hello from fterm ftp\n");

        // Upload
        await using (var w = await ftp.OpenWriteAsync(name, cts.Token))
        {
            await w.WriteAsync(payload, cts.Token);
        }

        // List directory and find our file
        var found = false;
        await foreach (var e in ftp.ListAsync(".", cts.Token))
        {
            if (e.Name == name) { found = true; break; }
        }
        Assert.True(found, $"uploaded file {name} should appear in listing");

        // Download
        await using (var r = await ftp.OpenReadAsync(name, cts.Token))
        {
            using var ms = new MemoryStream();
            await r.CopyToAsync(ms, cts.Token);
            Assert.Equal(payload, ms.ToArray());
        }

        // Delete
        await ftp.DeleteAsync(name, recursive: false, cts.Token);
    }
}
