namespace Fterm.Terminal.Vt;

/// <summary>
/// <see cref="VtParser"/> が認識したシーケンスを受け取るコールバック。
/// バッファ操作の実体は <see cref="TerminalEmulator"/> が実装する。
/// </summary>
public interface IVtHandler
{
    void Print(char ch);
    void Execute(byte controlByte);
    void CsiDispatch(char final, ReadOnlySpan<int> parameters, ReadOnlySpan<char> intermediates, bool isPrivate);
    void EscDispatch(char final, ReadOnlySpan<char> intermediates);
    void OscDispatch(int code, string data);
}
