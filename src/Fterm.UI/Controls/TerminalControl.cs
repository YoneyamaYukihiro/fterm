using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Fterm.Terminal.Buffer;
using Fterm.Terminal.Emulator;

namespace Fterm.UI.Controls;

/// <summary>
/// <see cref="TerminalEmulator"/> のバッファを描画する Avalonia 用カスタムコントロール。
/// 等幅フォントを前提にセルを直接 DrawingContext で描画する。
/// </summary>
public sealed class TerminalControl : Control
{
    private static readonly IReadOnlyList<Avalonia.Media.Color> AnsiPalette = BuildPalette();

    public static readonly StyledProperty<TerminalEmulator?> EmulatorProperty =
        AvaloniaProperty.Register<TerminalControl, TerminalEmulator?>(nameof(Emulator));

    public static readonly StyledProperty<IReadOnlyList<(int Row, int Col, int Length)>?> SearchHitsProperty =
        AvaloniaProperty.Register<TerminalControl, IReadOnlyList<(int Row, int Col, int Length)>?>(nameof(SearchHits));

    public TerminalEmulator? Emulator
    {
        get => GetValue(EmulatorProperty);
        set => SetValue(EmulatorProperty, value);
    }

    public IReadOnlyList<(int Row, int Col, int Length)>? SearchHits
    {
        get => GetValue(SearchHitsProperty);
        set => SetValue(SearchHitsProperty, value);
    }

    public event EventHandler<TerminalInputEventArgs>? UserInput;
    public event EventHandler<TerminalResizeEventArgs>? UserResize;

    private double _cellW = 8;
    private double _cellH = 16;
    private ulong _lastRevision = ulong.MaxValue;
    private Typeface _typeface = new("Cascadia Mono,Consolas,Menlo,Monospace");
    private readonly IBrush _backgroundBrush = new SolidColorBrush(Avalonia.Media.Color.FromRgb(0x1e, 0x1e, 0x1e));

    public TerminalControl()
    {
        Focusable = true;
        ClipToBounds = true;

        var probe = new FormattedText("M", System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight, _typeface, 13, Brushes.White);
        _cellW = probe.WidthIncludingTrailingWhitespace;
        _cellH = probe.Height;

        DispatcherTimer.Run(() =>
        {
            if (Emulator is { Buffer: var b } && b.RevisionCounter != _lastRevision)
            {
                _lastRevision = b.RevisionCounter;
                InvalidateVisual();
            }
            return true;
        }, TimeSpan.FromMilliseconds(33));
    }

    protected override void OnSizeChanged(SizeChangedEventArgs e)
    {
        base.OnSizeChanged(e);
        var cols = Math.Max(20, (int)(Bounds.Width / _cellW));
        var rows = Math.Max(5, (int)(Bounds.Height / _cellH));
        UserResize?.Invoke(this, new TerminalResizeEventArgs(cols, rows));
    }

    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx);
        if (Emulator is null) return;

        var buf = Emulator.Buffer;

        ctx.FillRectangle(_backgroundBrush, new Rect(Bounds.Size));

        for (var r = 0; r < buf.Rows; r++)
        {
            var row = buf.Active[r];
            for (var c = 0; c < buf.Cols; c++)
            {
                var cell = row[c];
                DrawCell(ctx, r, c, cell);
            }
        }

        if (buf.CursorVisible)
        {
            var x = buf.Active.CursorCol * _cellW;
            var y = buf.Active.CursorRow * _cellH;
            ctx.FillRectangle(new SolidColorBrush(Avalonia.Media.Color.FromArgb(0x80, 0xdc, 0xdc, 0xdc)),
                new Rect(x, y, _cellW, _cellH));
        }

        if (SearchHits is { Count: > 0 } hits)
        {
            var hitBrush = new SolidColorBrush(Avalonia.Media.Color.FromArgb(0x80, 0xff, 0xd7, 0x00));
            foreach (var (row, col, len) in hits)
            {
                ctx.FillRectangle(hitBrush, new Rect(col * _cellW, row * _cellH, len * _cellW, _cellH));
            }
        }
    }

    private void DrawCell(DrawingContext ctx, int row, int col, Cell cell)
    {
        var (fg, bg) = ResolveColors(cell);
        var x = col * _cellW;
        var y = row * _cellH;

        if (!cell.Background.IsDefault || (cell.Attributes & CellAttributes.Reverse) != 0)
        {
            ctx.FillRectangle(new SolidColorBrush(bg), new Rect(x, y, _cellW, _cellH));
        }

        if (cell.Char == ' ' || cell.Char == '\0') return;

        var text = new FormattedText(cell.Char.ToString(),
            System.Globalization.CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            (cell.Attributes & CellAttributes.Bold) != 0
                ? new Typeface(_typeface.FontFamily, FontStyle.Normal, FontWeight.Bold)
                : _typeface,
            13,
            new SolidColorBrush(fg));
        ctx.DrawText(text, new Point(x, y));
    }

    private static (Avalonia.Media.Color fg, Avalonia.Media.Color bg) ResolveColors(Cell cell)
    {
        var fg = cell.Foreground.IsDefault
            ? Avalonia.Media.Color.FromRgb(0xdc, 0xdc, 0xdc)
            : ToColor(cell.Foreground);
        var bg = cell.Background.IsDefault
            ? Avalonia.Media.Color.FromRgb(0x1e, 0x1e, 0x1e)
            : ToColor(cell.Background);
        if ((cell.Attributes & CellAttributes.Reverse) != 0) (fg, bg) = (bg, fg);
        return (fg, bg);
    }

    private static Avalonia.Media.Color ToColor(Fterm.Terminal.Buffer.Color c)
    {
        if (c.IsTrueColor)
        {
            var (r, g, b) = c.Rgb24;
            return Avalonia.Media.Color.FromRgb(r, g, b);
        }
        return AnsiPalette[c.Index];
    }

    private static IReadOnlyList<Avalonia.Media.Color> BuildPalette()
    {
        var p = new Avalonia.Media.Color[256];
        // 0-15: 標準 ANSI 16 色（xterm 既定）
        var basic = new (byte r, byte g, byte b)[]
        {
            (  0,   0,   0), (205,  49,  49), ( 13, 188, 121), (229, 229,  16),
            ( 36, 114, 200), (188,  63, 188), ( 17, 168, 205), (229, 229, 229),
            (102, 102, 102), (241,  76,  76), ( 35, 209, 139), (245, 245,  67),
            ( 59, 142, 234), (214, 112, 214), ( 41, 184, 219), (255, 255, 255),
        };
        for (var i = 0; i < 16; i++) p[i] = Avalonia.Media.Color.FromRgb(basic[i].r, basic[i].g, basic[i].b);
        // 16-231: 6x6x6 cube
        ReadOnlySpan<byte> levels = [0, 95, 135, 175, 215, 255];
        var idx = 16;
        for (var r = 0; r < 6; r++)
        for (var g = 0; g < 6; g++)
        for (var b = 0; b < 6; b++)
            p[idx++] = Avalonia.Media.Color.FromRgb(levels[r], levels[g], levels[b]);
        // 232-255: grayscale
        for (var i = 0; i < 24; i++)
        {
            var v = (byte)(8 + i * 10);
            p[232 + i] = Avalonia.Media.Color.FromRgb(v, v, v);
        }
        return p;
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Emulator is null) return;
        var bytes = KeyToSequence(e);
        if (bytes is not null)
        {
            UserInput?.Invoke(this, new TerminalInputEventArgs(bytes));
            e.Handled = true;
        }
    }

    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (string.IsNullOrEmpty(e.Text)) return;
        var bytes = System.Text.Encoding.UTF8.GetBytes(e.Text);
        UserInput?.Invoke(this, new TerminalInputEventArgs(bytes));
        e.Handled = true;
    }

    private static byte[]? KeyToSequence(KeyEventArgs e)
    {
        // 制御文字、矢印キー、Enter / Tab / Backspace 等を xterm 互換のシーケンスに変換する。
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
        {
            // Ctrl+[A-Z] -> 0x01..0x1A
            if (e.Key is >= Key.A and <= Key.Z)
            {
                return [(byte)(e.Key - Key.A + 1)];
            }
        }
        return e.Key switch
        {
            Key.Enter => "\r"u8.ToArray(),
            Key.Tab => "\t"u8.ToArray(),
            Key.Back => [0x7F],
            Key.Escape => [0x1B],
            Key.Up => "\x1B[A"u8.ToArray(),
            Key.Down => "\x1B[B"u8.ToArray(),
            Key.Right => "\x1B[C"u8.ToArray(),
            Key.Left => "\x1B[D"u8.ToArray(),
            Key.Home => "\x1B[H"u8.ToArray(),
            Key.End => "\x1B[F"u8.ToArray(),
            Key.PageUp => "\x1B[5~"u8.ToArray(),
            Key.PageDown => "\x1B[6~"u8.ToArray(),
            Key.Delete => "\x1B[3~"u8.ToArray(),
            _ => null,
        };
    }
}

public sealed class TerminalInputEventArgs(byte[] data) : EventArgs
{
    public byte[] Data { get; } = data;
}

public sealed class TerminalResizeEventArgs(int cols, int rows) : EventArgs
{
    public int Cols { get; } = cols;
    public int Rows { get; } = rows;
}
