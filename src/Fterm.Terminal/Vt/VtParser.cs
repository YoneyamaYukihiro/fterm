using System.Text;

namespace Fterm.Terminal.Vt;

/// <summary>
/// VT 系シーケンスを解釈する状態機械パーサ。
/// 参考: https://vt100.net/emu/dec_ansi_parser
/// 入力は UTF-8 バイト列を受け付け、内部で <see cref="Decoder"/> を介して
/// コードポイントに変換しつつ状態遷移する。
/// </summary>
public sealed class VtParser
{
    private enum State
    {
        Ground,
        Escape,
        EscapeIntermediate,
        CsiEntry,
        CsiParam,
        CsiIntermediate,
        CsiIgnore,
        OscString,
    }

    private readonly IVtHandler _handler;
    private readonly Decoder _decoder = Encoding.UTF8.GetDecoder();
    private readonly char[] _charBuf = new char[1];

    private State _state = State.Ground;
    private readonly List<int> _params = [];
    private int _currentParam = -1;
    private readonly List<char> _intermediates = [];
    private bool _isPrivate;
    private readonly StringBuilder _oscBuf = new(64);

    public VtParser(IVtHandler handler)
    {
        _handler = handler;
    }

    public void Feed(ReadOnlySpan<byte> bytes)
    {
        var input = new byte[1];
        for (var i = 0; i < bytes.Length; i++)
        {
            var b = bytes[i];

            // 制御バイトは即時処理（任意の状態から）
            if (b < 0x20 && _state != State.OscString)
            {
                HandleControl(b);
                continue;
            }

            switch (_state)
            {
                case State.Ground:
                    FeedPrintable(b, input);
                    break;
                case State.Escape:
                    HandleEscape(b);
                    break;
                case State.EscapeIntermediate:
                    HandleEscapeIntermediate(b);
                    break;
                case State.CsiEntry:
                    HandleCsiEntry(b);
                    break;
                case State.CsiParam:
                    HandleCsiParam(b);
                    break;
                case State.CsiIntermediate:
                    HandleCsiIntermediate(b);
                    break;
                case State.CsiIgnore:
                    HandleCsiIgnore(b);
                    break;
                case State.OscString:
                    HandleOsc(b);
                    break;
            }
        }
    }

    private void FeedPrintable(byte b, byte[] tmp)
    {
        tmp[0] = b;
        var written = _decoder.GetChars(tmp, 0, 1, _charBuf, 0, flush: false);
        if (written > 0)
        {
            _handler.Print(_charBuf[0]);
        }
    }

    private void HandleControl(byte b)
    {
        switch (b)
        {
            case 0x1B: // ESC
                EnterEscape();
                break;
            case 0x18: // CAN
            case 0x1A: // SUB
                Reset();
                break;
            default:
                _handler.Execute(b);
                break;
        }
    }

    private void EnterEscape()
    {
        _state = State.Escape;
        _params.Clear();
        _currentParam = -1;
        _intermediates.Clear();
        _isPrivate = false;
    }

    private void Reset()
    {
        _state = State.Ground;
        _params.Clear();
        _currentParam = -1;
        _intermediates.Clear();
        _isPrivate = false;
        _oscBuf.Clear();
    }

    private void HandleEscape(byte b)
    {
        switch (b)
        {
            case (byte)'[':
                _state = State.CsiEntry;
                _params.Clear();
                _currentParam = -1;
                _intermediates.Clear();
                _isPrivate = false;
                break;
            case (byte)']':
                _state = State.OscString;
                _oscBuf.Clear();
                break;
            default:
                if (b is >= 0x20 and <= 0x2F)
                {
                    _intermediates.Add((char)b);
                    _state = State.EscapeIntermediate;
                }
                else if (b is >= 0x30 and <= 0x7E)
                {
                    _handler.EscDispatch((char)b, _intermediates.ToArray());
                    Reset();
                }
                break;
        }
    }

    private void HandleEscapeIntermediate(byte b)
    {
        if (b is >= 0x20 and <= 0x2F)
        {
            _intermediates.Add((char)b);
        }
        else if (b is >= 0x30 and <= 0x7E)
        {
            _handler.EscDispatch((char)b, _intermediates.ToArray());
            Reset();
        }
    }

    private void HandleCsiEntry(byte b)
    {
        if (b is >= 0x3C and <= 0x3F)
        {
            _isPrivate = true;
            _state = State.CsiParam;
        }
        else if (b is >= 0x30 and <= 0x39 || b == ';' || b == ':')
        {
            HandleCsiParam(b);
        }
        else if (b is >= 0x20 and <= 0x2F)
        {
            _intermediates.Add((char)b);
            _state = State.CsiIntermediate;
        }
        else if (b is >= 0x40 and <= 0x7E)
        {
            DispatchCsi((char)b);
        }
        else
        {
            _state = State.CsiIgnore;
        }
    }

    private void HandleCsiParam(byte b)
    {
        if (b is >= 0x30 and <= 0x39)
        {
            if (_currentParam < 0) _currentParam = 0;
            _currentParam = _currentParam * 10 + (b - '0');
            _state = State.CsiParam;
        }
        else if (b == ';' || b == ':')
        {
            _params.Add(_currentParam < 0 ? 0 : _currentParam);
            _currentParam = -1;
            _state = State.CsiParam;
        }
        else if (b is >= 0x20 and <= 0x2F)
        {
            if (_currentParam >= 0) { _params.Add(_currentParam); _currentParam = -1; }
            _intermediates.Add((char)b);
            _state = State.CsiIntermediate;
        }
        else if (b is >= 0x40 and <= 0x7E)
        {
            if (_currentParam >= 0) { _params.Add(_currentParam); _currentParam = -1; }
            DispatchCsi((char)b);
        }
        else if (b is >= 0x3C and <= 0x3F)
        {
            // 既にエントリで private にする場合のみ意味があるが、ここでは無視
            _state = State.CsiIgnore;
        }
    }

    private void HandleCsiIntermediate(byte b)
    {
        if (b is >= 0x20 and <= 0x2F)
        {
            _intermediates.Add((char)b);
        }
        else if (b is >= 0x40 and <= 0x7E)
        {
            DispatchCsi((char)b);
        }
        else
        {
            _state = State.CsiIgnore;
        }
    }

    private void HandleCsiIgnore(byte b)
    {
        if (b is >= 0x40 and <= 0x7E)
        {
            Reset();
        }
    }

    private void DispatchCsi(char final)
    {
        _handler.CsiDispatch(final, _params.ToArray(), _intermediates.ToArray(), _isPrivate);
        Reset();
    }

    private void HandleOsc(byte b)
    {
        // ST: ESC \ (0x1B 0x5C) または BEL (0x07) で終端
        if (b == 0x07)
        {
            FinishOsc();
            return;
        }
        if (b == 0x1B)
        {
            // 次のバイト '\' を期待する。簡略化：そこで終端扱い。
            FinishOsc();
            return;
        }
        _oscBuf.Append((char)b);
    }

    private void FinishOsc()
    {
        var raw = _oscBuf.ToString();
        var sep = raw.IndexOf(';');
        if (sep < 0)
        {
            // コード単独 (e.g. ESC ] 0 BEL) は無視
            if (int.TryParse(raw, out var only))
            {
                _handler.OscDispatch(only, "");
            }
        }
        else if (int.TryParse(raw.AsSpan(0, sep), out var code))
        {
            _handler.OscDispatch(code, raw[(sep + 1)..]);
        }
        Reset();
    }
}
