using System.Text;
using Fterm.Terminal.Buffer;
using Fterm.Terminal.Emulator;
using Fterm.Terminal.Vt;
using Xunit;

namespace Fterm.Terminal.Tests;

public class VtParserTests
{
    private static (TerminalEmulator emu, VtParser parser) NewEmulator(int cols = 20, int rows = 5)
    {
        var buf = new TerminalBuffer(cols, rows);
        var emu = new TerminalEmulator(buf);
        return (emu, new VtParser(emu));
    }

    private static string ReadRow(TerminalBuffer b, int row)
    {
        var sb = new StringBuilder();
        foreach (var cell in b.Active[row]) sb.Append(cell.Char);
        return sb.ToString().TrimEnd();
    }

    [Fact]
    public void Prints_plain_text()
    {
        var (emu, p) = NewEmulator();
        p.Feed("hello"u8);
        Assert.Equal("hello", ReadRow(emu.Buffer, 0));
        Assert.Equal(5, emu.Buffer.Active.CursorCol);
    }

    [Fact]
    public void CrLf_moves_cursor_to_next_line_start()
    {
        var (emu, p) = NewEmulator();
        p.Feed("a\r\nb"u8);
        Assert.Equal("a", ReadRow(emu.Buffer, 0));
        Assert.Equal("b", ReadRow(emu.Buffer, 1));
        Assert.Equal(1, emu.Buffer.Active.CursorRow);
    }

    [Fact]
    public void Csi_cup_moves_cursor()
    {
        var (emu, p) = NewEmulator();
        p.Feed("\x1B[3;5H"u8);
        Assert.Equal(2, emu.Buffer.Active.CursorRow);
        Assert.Equal(4, emu.Buffer.Active.CursorCol);
    }

    [Fact]
    public void Csi_ed_clears_screen()
    {
        var (emu, p) = NewEmulator();
        p.Feed("hello"u8);
        p.Feed("\x1B[2J"u8);
        Assert.Equal("", ReadRow(emu.Buffer, 0));
    }

    [Fact]
    public void Sgr_sets_foreground_color()
    {
        var (emu, p) = NewEmulator();
        p.Feed("\x1B[31mR\x1B[0mN"u8);
        Assert.Equal(Color.Indexed(1), emu.Buffer.Active[0][0].Foreground);
        Assert.Equal(Color.Default, emu.Buffer.Active[0][1].Foreground);
    }

    [Fact]
    public void Sgr_true_color()
    {
        var (emu, p) = NewEmulator();
        p.Feed("\x1B[38;2;10;20;30mX"u8);
        var fg = emu.Buffer.Active[0][0].Foreground;
        Assert.True(fg.IsTrueColor);
        Assert.Equal(((byte)10, (byte)20, (byte)30), fg.Rgb24);
    }

    [Fact]
    public void Decset_1049_switches_to_alt_screen()
    {
        var (emu, p) = NewEmulator();
        p.Feed("main"u8);
        p.Feed("\x1B[?1049h"u8);
        Assert.True(emu.Buffer.IsAlt);
        p.Feed("alt"u8);
        Assert.Equal("alt", ReadRow(emu.Buffer, 0));
        p.Feed("\x1B[?1049l"u8);
        Assert.False(emu.Buffer.IsAlt);
        Assert.Equal("main", ReadRow(emu.Buffer, 0));
    }

    [Fact]
    public void Osc_0_sets_title()
    {
        var (emu, p) = NewEmulator();
        p.Feed("\x1B]0;hello world\x07"u8);
        Assert.Equal("hello world", emu.Buffer.Title);
    }

    [Fact]
    public void Backspace_moves_cursor_left()
    {
        var (emu, p) = NewEmulator();
        p.Feed("abc\b"u8);
        Assert.Equal(2, emu.Buffer.Active.CursorCol);
    }

    [Fact]
    public void Line_wrap_advances_to_next_row()
    {
        var (emu, p) = NewEmulator(cols: 3, rows: 3);
        p.Feed("abcdef"u8);
        Assert.Equal("abc", ReadRow(emu.Buffer, 0));
        Assert.Equal("def", ReadRow(emu.Buffer, 1));
    }

    [Fact]
    public void Scrolling_pushes_top_line_to_scrollback()
    {
        var (emu, p) = NewEmulator(cols: 4, rows: 2);
        p.Feed("a\r\nb\r\nc"u8);
        Assert.Equal("b", ReadRow(emu.Buffer, 0));
        Assert.Equal("c", ReadRow(emu.Buffer, 1));
        Assert.Single(emu.Buffer.Scrollback);
        Assert.Equal('a', emu.Buffer.Scrollback[0][0].Char);
    }

    [Fact]
    public void Utf8_multibyte_character_is_emitted_as_single_codepoint()
    {
        var (emu, p) = NewEmulator();
        p.Feed(Encoding.UTF8.GetBytes("あ"));
        Assert.Equal('あ', emu.Buffer.Active[0][0].Char);
    }
}
