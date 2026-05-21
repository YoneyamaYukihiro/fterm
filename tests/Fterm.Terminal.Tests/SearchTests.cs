using Fterm.Terminal.Buffer;
using Fterm.Terminal.Emulator;
using Fterm.Terminal.Vt;
using Xunit;

namespace Fterm.Terminal.Tests;

public class SearchTests
{
    [Fact]
    public void Finds_single_match()
    {
        var buf = new TerminalBuffer(40, 5);
        var p = new VtParser(new TerminalEmulator(buf));
        p.Feed("hello world"u8);

        var hits = buf.Find("world");
        Assert.Single(hits);
        Assert.Equal((0, 6, 5), hits[0]);
    }

    [Fact]
    public void Finds_multiple_matches_across_rows()
    {
        var buf = new TerminalBuffer(20, 5);
        var p = new VtParser(new TerminalEmulator(buf));
        p.Feed("abc def abc\r\nzabc"u8);

        var hits = buf.Find("abc");
        Assert.Equal(3, hits.Count);
    }

    [Fact]
    public void Case_insensitive_by_default()
    {
        var buf = new TerminalBuffer(20, 5);
        var p = new VtParser(new TerminalEmulator(buf));
        p.Feed("Hello"u8);
        Assert.Single(buf.Find("hello"));
        Assert.Empty(buf.Find("hello", caseSensitive: true));
    }

    [Fact]
    public void Returns_empty_for_empty_pattern()
    {
        var buf = new TerminalBuffer(20, 5);
        Assert.Empty(buf.Find(""));
    }
}
