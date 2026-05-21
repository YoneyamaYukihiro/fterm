using Fterm.Core.Macros;
using Xunit;

namespace Fterm.Core.Tests;

public class MacroTests
{
    [Fact]
    public void Parses_send_step_with_escape_sequences()
    {
        var steps = Macro.Parse("send \"ls -la\\r\\n\"");
        Assert.Single(steps);
        var s = Assert.IsType<SendStep>(steps[0]);
        Assert.Equal("ls -la\r\n", s.Text);
    }

    [Fact]
    public void Parses_sleep_step()
    {
        var s = Assert.IsType<SleepStep>(Macro.Parse("sleep 250").Single());
        Assert.Equal(250, s.Milliseconds);
    }

    [Fact]
    public void Parses_expect_step_with_default_timeout()
    {
        var s = Assert.IsType<ExpectStep>(Macro.Parse("expect \"$ \"").Single());
        Assert.Equal("$ ", s.Pattern);
        Assert.Equal(30_000, s.TimeoutMs);
    }

    [Fact]
    public void Parses_expect_step_with_custom_timeout()
    {
        var s = Assert.IsType<ExpectStep>(Macro.Parse("expect \"login: \" 5000").Single());
        Assert.Equal("login: ", s.Pattern);
        Assert.Equal(5_000, s.TimeoutMs);
    }

    [Fact]
    public void Skips_blank_lines_and_comments()
    {
        var src = """
        # banner
        sleep 100

        send "hi"
        """;
        var steps = Macro.Parse(src);
        Assert.Equal(2, steps.Count);
    }

    [Fact]
    public void Throws_on_unknown_command()
    {
        Assert.Throws<FormatException>(() => Macro.Parse("frobnicate \"x\""));
    }
}
