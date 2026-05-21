using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Fterm.Core.Updates;
using Xunit;

namespace Fterm.Core.Tests;

public class UpdateCheckerTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;

        public StubHandler(HttpStatusCode status, string body) { _status = status; _body = body; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json"),
            });
    }

    private static UpdateChecker NewChecker(string body, HttpStatusCode status, Version current) =>
        new(new HttpClient(new StubHandler(status, body)), current);

    private const string Manifest_1_2_3 = """
    {
      "version": "1.2.3",
      "notes": "next!",
      "assets": {
        "linux-x64": { "url": "https://example/fterm.tar.gz", "sha256": "abcd", "size": 100 }
      }
    }
    """;

    [Fact]
    public async Task Returns_update_when_newer_version_available()
    {
        var checker = NewChecker(Manifest_1_2_3, HttpStatusCode.OK, new Version(1, 0, 0));
        var info = await checker.CheckAsync("https://x/manifest.json", "linux-x64", CancellationToken.None);
        Assert.NotNull(info);
        Assert.Equal(new Version(1, 2, 3), info!.NewVersion);
        Assert.Equal("https://example/fterm.tar.gz", info.Asset.Url);
        Assert.Equal("next!", info.Notes);
    }

    [Fact]
    public async Task Returns_null_when_already_latest()
    {
        var checker = NewChecker(Manifest_1_2_3, HttpStatusCode.OK, new Version(1, 2, 3));
        Assert.Null(await checker.CheckAsync("https://x/m.json", "linux-x64", CancellationToken.None));
    }

    [Fact]
    public async Task Returns_null_when_asset_missing_for_rid()
    {
        var checker = NewChecker(Manifest_1_2_3, HttpStatusCode.OK, new Version(1, 0, 0));
        Assert.Null(await checker.CheckAsync("https://x/m.json", "haiku-amd64", CancellationToken.None));
    }

    [Fact]
    public async Task Returns_null_on_network_error()
    {
        var checker = NewChecker("oops", HttpStatusCode.InternalServerError, new Version(1, 0, 0));
        Assert.Null(await checker.CheckAsync("https://x/m.json", "linux-x64", CancellationToken.None));
    }

    [Fact]
    public async Task VerifyAsync_returns_true_when_sha_matches()
    {
        var tmp = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(tmp, [1, 2, 3, 4]);
            var expected = Convert.ToHexString(SHA256.HashData(new byte[] { 1, 2, 3, 4 }));
            var checker = NewChecker("{}", HttpStatusCode.OK, new Version(1, 0, 0));
            Assert.True(await checker.VerifyAsync(tmp, expected, CancellationToken.None));
            Assert.False(await checker.VerifyAsync(tmp, "0000000000000000", CancellationToken.None));
        }
        finally
        {
            File.Delete(tmp);
        }
    }
}
