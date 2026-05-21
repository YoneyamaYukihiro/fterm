namespace Fterm.Terminal.Buffer;

[Flags]
public enum CellAttributes
{
    None = 0,
    Bold = 1 << 0,
    Italic = 1 << 1,
    Underline = 1 << 2,
    Reverse = 1 << 3,
    Strikethrough = 1 << 4,
    Faint = 1 << 5,
    Invisible = 1 << 6,
}

/// <summary>
/// セルの前景色 / 背景色。
/// - 0..255 はインデックスカラー（256 色パレット）
/// - それ以外は 24bit RGB を 0xRRGGBB として持ち、最上位ビットを立てて識別する。
/// </summary>
public readonly record struct Color(uint Value)
{
    public const uint TrueColorFlag = 0x80000000u;

    public static Color Default { get; } = new(0xFFFFFFFFu);
    public static Color Indexed(int index) => new((uint)(index & 0xFF));
    public static Color Rgb(byte r, byte g, byte b) =>
        new(TrueColorFlag | ((uint)r << 16) | ((uint)g << 8) | b);

    public bool IsDefault => Value == 0xFFFFFFFFu;
    public bool IsTrueColor => (Value & TrueColorFlag) != 0 && !IsDefault;
    public int Index => (int)(Value & 0xFF);
    public (byte R, byte G, byte B) Rgb24 =>
        ((byte)((Value >> 16) & 0xFF), (byte)((Value >> 8) & 0xFF), (byte)(Value & 0xFF));
}

public readonly record struct Cell(char Char, Color Foreground, Color Background, CellAttributes Attributes)
{
    public static Cell Empty { get; } = new(' ', Color.Default, Color.Default, CellAttributes.None);
}
