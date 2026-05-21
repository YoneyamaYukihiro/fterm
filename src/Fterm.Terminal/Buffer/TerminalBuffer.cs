namespace Fterm.Terminal.Buffer;

/// <summary>
/// 単一スクリーンを表す行ベースの可変バッファ（主画面 / 代替画面のそれぞれが一つずつ持つ）。
/// 行は <see cref="Cell"/> 配列。サイズ変更時は内容を保持して幅を伸縮する。
/// </summary>
public sealed class Screen
{
    public List<Cell[]> Rows { get; } = [];

    public int Cols { get; private set; }
    public int CursorRow { get; set; }
    public int CursorCol { get; set; }
    public int ScrollTop { get; set; }
    public int ScrollBottom { get; set; }

    public Screen(int cols, int rows)
    {
        Resize(cols, rows);
        ScrollTop = 0;
        ScrollBottom = rows - 1;
    }

    public int Rows_Count => Rows.Count;

    public Cell[] this[int row] => Rows[row];

    public void Resize(int cols, int rows)
    {
        Cols = cols;
        while (Rows.Count < rows)
        {
            Rows.Add(NewBlankRow(cols));
        }
        while (Rows.Count > rows)
        {
            Rows.RemoveAt(Rows.Count - 1);
        }
        for (var i = 0; i < Rows.Count; i++)
        {
            if (Rows[i].Length == cols) continue;
            var newRow = NewBlankRow(cols);
            Array.Copy(Rows[i], newRow, Math.Min(Rows[i].Length, cols));
            Rows[i] = newRow;
        }
        if (CursorCol >= cols) CursorCol = cols - 1;
        if (CursorRow >= rows) CursorRow = rows - 1;
        if (ScrollBottom >= rows || ScrollBottom == 0) ScrollBottom = rows - 1;
        if (ScrollTop >= rows) ScrollTop = 0;
    }

    public static Cell[] NewBlankRow(int cols)
    {
        var row = new Cell[cols];
        Array.Fill(row, Cell.Empty);
        return row;
    }
}

/// <summary>
/// 端末全体のステート。主画面 / 代替画面、スクロールバック、カーソル属性などを保持する。
/// VT パーサが呼ぶエミュレータ層からのみ更新される。
/// </summary>
public sealed class TerminalBuffer
{
    private readonly int _maxScrollback;

    public int Cols { get; private set; }
    public int Rows { get; private set; }

    public Screen Main { get; private set; }
    public Screen Alt { get; private set; }
    public bool IsAlt { get; private set; }
    public Screen Active => IsAlt ? Alt : Main;

    public List<Cell[]> Scrollback { get; } = [];

    public Color CurrentForeground { get; set; } = Color.Default;
    public Color CurrentBackground { get; set; } = Color.Default;
    public CellAttributes CurrentAttributes { get; set; } = CellAttributes.None;

    public bool CursorVisible { get; set; } = true;
    public bool BracketedPaste { get; set; }
    public string Title { get; set; } = "";

    public ulong RevisionCounter { get; private set; }

    public TerminalBuffer(int cols, int rows, int maxScrollback = 10_000)
    {
        Cols = cols;
        Rows = rows;
        _maxScrollback = maxScrollback;
        Main = new Screen(cols, rows);
        Alt = new Screen(cols, rows);
    }

    public void Bump() => RevisionCounter++;

    public void Resize(int cols, int rows)
    {
        Cols = cols;
        Rows = rows;
        Main.Resize(cols, rows);
        Alt.Resize(cols, rows);
        Bump();
    }

    public void SwitchToAlt()
    {
        if (IsAlt) return;
        IsAlt = true;
        Alt.CursorRow = 0;
        Alt.CursorCol = 0;
        foreach (var row in Alt.Rows)
        {
            Array.Fill(row, Cell.Empty);
        }
        Bump();
    }

    public void SwitchToMain()
    {
        if (!IsAlt) return;
        IsAlt = false;
        Bump();
    }

    public void Print(char ch)
    {
        var s = Active;
        if (s.CursorCol >= Cols)
        {
            s.CursorCol = 0;
            LineFeed();
        }
        s[s.CursorRow][s.CursorCol] = new Cell(ch, CurrentForeground, CurrentBackground, CurrentAttributes);
        s.CursorCol++;
    }

    public void CarriageReturn()
    {
        Active.CursorCol = 0;
    }

    public void LineFeed()
    {
        var s = Active;
        if (s.CursorRow == s.ScrollBottom)
        {
            ScrollUpInRegion(s);
        }
        else if (s.CursorRow < Rows - 1)
        {
            s.CursorRow++;
        }
    }

    public void Backspace()
    {
        var s = Active;
        if (s.CursorCol > 0) s.CursorCol--;
    }

    public void Tab()
    {
        var s = Active;
        s.CursorCol = Math.Min(Cols - 1, (s.CursorCol / 8 + 1) * 8);
    }

    public void MoveCursor(int row, int col)
    {
        var s = Active;
        s.CursorRow = Math.Clamp(row, 0, Rows - 1);
        s.CursorCol = Math.Clamp(col, 0, Cols - 1);
    }

    public void OffsetCursor(int dRow, int dCol)
    {
        var s = Active;
        MoveCursor(s.CursorRow + dRow, s.CursorCol + dCol);
    }

    public void EraseInLine(int mode)
    {
        var s = Active;
        var row = s[s.CursorRow];
        switch (mode)
        {
            case 0:
                for (var c = s.CursorCol; c < Cols; c++) row[c] = Cell.Empty;
                break;
            case 1:
                for (var c = 0; c <= s.CursorCol && c < Cols; c++) row[c] = Cell.Empty;
                break;
            case 2:
                Array.Fill(row, Cell.Empty);
                break;
        }
    }

    public void EraseInDisplay(int mode)
    {
        var s = Active;
        switch (mode)
        {
            case 0:
                EraseInLine(0);
                for (var r = s.CursorRow + 1; r < Rows; r++) Array.Fill(s[r], Cell.Empty);
                break;
            case 1:
                EraseInLine(1);
                for (var r = 0; r < s.CursorRow; r++) Array.Fill(s[r], Cell.Empty);
                break;
            case 2:
            case 3:
                for (var r = 0; r < Rows; r++) Array.Fill(s[r], Cell.Empty);
                break;
        }
    }

    public void SetScrollRegion(int top, int bottom)
    {
        var s = Active;
        s.ScrollTop = Math.Clamp(top, 0, Rows - 1);
        s.ScrollBottom = Math.Clamp(bottom, s.ScrollTop, Rows - 1);
    }

    public void ResetAttributes()
    {
        CurrentForeground = Color.Default;
        CurrentBackground = Color.Default;
        CurrentAttributes = CellAttributes.None;
    }

    private void ScrollUpInRegion(Screen s)
    {
        if (s.ScrollTop == 0 && !IsAlt)
        {
            Scrollback.Add(s[0]);
            while (Scrollback.Count > _maxScrollback)
            {
                Scrollback.RemoveAt(0);
            }
        }
        for (var r = s.ScrollTop; r < s.ScrollBottom; r++)
        {
            s.Rows[r] = s.Rows[r + 1];
        }
        s.Rows[s.ScrollBottom] = Screen.NewBlankRow(Cols);
    }
}
