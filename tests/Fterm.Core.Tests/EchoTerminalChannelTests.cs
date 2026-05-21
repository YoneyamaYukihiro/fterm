using System.Text;
using Fterm.Core.Sessions;
using Xunit;

namespace Fterm.Core.Tests;

public class EchoTerminalChannelTests
{
    [Fact]
    public async Task Echoes_written_data_after_banner()
    {
        await using var channel = new EchoTerminalChannel();
        await channel.ConnectAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var enumerator = channel.ReadAsync(cts.Token).GetAsyncEnumerator(cts.Token);
        Assert.True(await enumerator.MoveNextAsync());
        var banner = Encoding.UTF8.GetString(enumerator.Current.Span);
        Assert.Contains("fterm echo channel ready", banner);

        await channel.WriteAsync("hello"u8.ToArray(), cts.Token);
        Assert.True(await enumerator.MoveNextAsync());
        Assert.Equal("hello", Encoding.UTF8.GetString(enumerator.Current.Span));
    }
}
