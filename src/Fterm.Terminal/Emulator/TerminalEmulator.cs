using Fterm.Terminal.Buffer;
using Fterm.Terminal.Vt;

namespace Fterm.Terminal.Emulator;

/// <summary>
/// <see cref="VtParser"/> からの CSI/ESC/OSC/Print/Execute を <see cref="TerminalBuffer"/> に反映する。
/// </summary>
public sealed class TerminalEmulator : IVtHandler
{
    public TerminalBuffer Buffer { get; }
    public event EventHandler? TitleChanged;

    public TerminalEmulator(TerminalBuffer buffer)
    {
        Buffer = buffer;
    }

    public void Print(char ch)
    {
        Buffer.Print(ch);
        Buffer.Bump();
    }

    public void Execute(byte controlByte)
    {
        switch (controlByte)
        {
            case 0x07: /* BEL */ break;
            case 0x08: Buffer.Backspace(); break;
            case 0x09: Buffer.Tab(); break;
            case 0x0A: case 0x0B: case 0x0C: Buffer.LineFeed(); break;
            case 0x0D: Buffer.CarriageReturn(); break;
        }
        Buffer.Bump();
    }

    public void EscDispatch(char final, ReadOnlySpan<char> intermediates)
    {
        switch (final)
        {
            case 'M': // RI: Reverse Index
                ReverseIndex();
                break;
            case 'c': // RIS: full reset
                Buffer.ResetAttributes();
                Buffer.MoveCursor(0, 0);
                Buffer.EraseInDisplay(2);
                Buffer.SwitchToMain();
                break;
        }
        Buffer.Bump();
    }

    public void CsiDispatch(char final, ReadOnlySpan<int> p, ReadOnlySpan<char> intermediates, bool isPrivate)
    {
        var p0 = p.Length > 0 && p[0] > 0 ? p[0] : 1;
        var p1 = p.Length > 1 && p[1] > 0 ? p[1] : 1;
        var p0z = p.Length > 0 ? p[0] : 0;

        switch (final)
        {
            case 'A': Buffer.OffsetCursor(-p0, 0); break;
            case 'B': Buffer.OffsetCursor(p0, 0); break;
            case 'C': Buffer.OffsetCursor(0, p0); break;
            case 'D': Buffer.OffsetCursor(0, -p0); break;
            case 'E': Buffer.MoveCursor(Buffer.Active.CursorRow + p0, 0); break;
            case 'F': Buffer.MoveCursor(Buffer.Active.CursorRow - p0, 0); break;
            case 'G': Buffer.MoveCursor(Buffer.Active.CursorRow, p0 - 1); break;
            case 'H':
            case 'f':
                Buffer.MoveCursor(p0 - 1, p1 - 1);
                break;
            case 'J': Buffer.EraseInDisplay(p0z); break;
            case 'K': Buffer.EraseInLine(p0z); break;
            case 'd': Buffer.MoveCursor(p0 - 1, Buffer.Active.CursorCol); break;
            case 'm': ApplySgr(p); break;
            case 'r':
                var bottom = p.Length > 1 && p[1] > 0 ? p[1] : Buffer.Rows;
                Buffer.SetScrollRegion(p0 - 1, bottom - 1);
                Buffer.MoveCursor(0, 0);
                break;
            case 'h' when isPrivate: SetMode(p, true); break;
            case 'l' when isPrivate: SetMode(p, false); break;
        }
        Buffer.Bump();
    }

    public void OscDispatch(int code, string data)
    {
        if (code is 0 or 2)
        {
            Buffer.Title = data;
            TitleChanged?.Invoke(this, EventArgs.Empty);
        }
        Buffer.Bump();
    }

    private void ReverseIndex()
    {
        var s = Buffer.Active;
        if (s.CursorRow == s.ScrollTop)
        {
            // スクロールダウン
            for (var r = s.ScrollBottom; r > s.ScrollTop; r--)
            {
                s.Rows[r] = s.Rows[r - 1];
            }
            s.Rows[s.ScrollTop] = Screen.NewBlankRow(Buffer.Cols);
        }
        else if (s.CursorRow > 0)
        {
            s.CursorRow--;
        }
    }

    private void SetMode(ReadOnlySpan<int> p, bool set)
    {
        for (var i = 0; i < p.Length; i++)
        {
            switch (p[i])
            {
                case 25: Buffer.CursorVisible = set; break;
                case 1049:
                    if (set) Buffer.SwitchToAlt();
                    else Buffer.SwitchToMain();
                    break;
                case 2004: Buffer.BracketedPaste = set; break;
            }
        }
    }

    private void ApplySgr(ReadOnlySpan<int> p)
    {
        if (p.Length == 0)
        {
            Buffer.ResetAttributes();
            return;
        }
        for (var i = 0; i < p.Length; i++)
        {
            var n = p[i];
            switch (n)
            {
                case 0: Buffer.ResetAttributes(); break;
                case 1: Buffer.CurrentAttributes |= CellAttributes.Bold; break;
                case 2: Buffer.CurrentAttributes |= CellAttributes.Faint; break;
                case 3: Buffer.CurrentAttributes |= CellAttributes.Italic; break;
                case 4: Buffer.CurrentAttributes |= CellAttributes.Underline; break;
                case 7: Buffer.CurrentAttributes |= CellAttributes.Reverse; break;
                case 8: Buffer.CurrentAttributes |= CellAttributes.Invisible; break;
                case 9: Buffer.CurrentAttributes |= CellAttributes.Strikethrough; break;
                case 22: Buffer.CurrentAttributes &= ~(CellAttributes.Bold | CellAttributes.Faint); break;
                case 23: Buffer.CurrentAttributes &= ~CellAttributes.Italic; break;
                case 24: Buffer.CurrentAttributes &= ~CellAttributes.Underline; break;
                case 27: Buffer.CurrentAttributes &= ~CellAttributes.Reverse; break;
                case 28: Buffer.CurrentAttributes &= ~CellAttributes.Invisible; break;
                case 29: Buffer.CurrentAttributes &= ~CellAttributes.Strikethrough; break;
                case >= 30 and <= 37: Buffer.CurrentForeground = Color.Indexed(n - 30); break;
                case 38:
                    i = ParseExtendedColor(p, i, isForeground: true);
                    break;
                case 39: Buffer.CurrentForeground = Color.Default; break;
                case >= 40 and <= 47: Buffer.CurrentBackground = Color.Indexed(n - 40); break;
                case 48:
                    i = ParseExtendedColor(p, i, isForeground: false);
                    break;
                case 49: Buffer.CurrentBackground = Color.Default; break;
                case >= 90 and <= 97: Buffer.CurrentForeground = Color.Indexed(n - 90 + 8); break;
                case >= 100 and <= 107: Buffer.CurrentBackground = Color.Indexed(n - 100 + 8); break;
            }
        }
    }

    private int ParseExtendedColor(ReadOnlySpan<int> p, int i, bool isForeground)
    {
        // 38;5;n / 38;2;r;g;b
        if (i + 1 >= p.Length) return i;
        var mode = p[i + 1];
        Color color;
        int consumed;
        if (mode == 5 && i + 2 < p.Length)
        {
            color = Color.Indexed(p[i + 2]);
            consumed = 2;
        }
        else if (mode == 2 && i + 4 < p.Length)
        {
            color = Color.Rgb((byte)p[i + 2], (byte)p[i + 3], (byte)p[i + 4]);
            consumed = 4;
        }
        else
        {
            return i;
        }
        if (isForeground) Buffer.CurrentForeground = color;
        else Buffer.CurrentBackground = color;
        return i + consumed;
    }
}
