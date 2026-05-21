using System.Text;
using Fterm.Core.Sessions;

namespace Fterm.Core.Macros;

public abstract record MacroStep;
public sealed record SendStep(string Text) : MacroStep;
public sealed record SleepStep(int Milliseconds) : MacroStep;
public sealed record ExpectStep(string Pattern, int TimeoutMs = 30_000) : MacroStep;

/// <summary>
/// 小さなマクロ DSL。各行は以下のいずれか:
///   send "文字列"        ... 文字列を送信 (\n /\r /\t /\\ /\" をエスケープ)
///   sleep N              ... N ms 待つ
///   expect "文字列"      ... 出力に文字列が現れるまで待つ (既定 30s タイムアウト)
///   expect "文字列" N    ... タイムアウト N ms
///   # コメント            ... 無視
/// </summary>
public static class Macro
{
    public static List<MacroStep> Parse(string source)
    {
        var steps = new List<MacroStep>();
        foreach (var raw in source.Split('\n'))
        {
            var line = raw.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            steps.Add(ParseLine(line));
        }
        return steps;
    }

    private static MacroStep ParseLine(string line)
    {
        if (line.StartsWith("send ", StringComparison.OrdinalIgnoreCase))
        {
            var s = ParseQuoted(line[5..].Trim());
            return new SendStep(Unescape(s));
        }
        if (line.StartsWith("sleep ", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(line[6..].Trim(), out var ms)) return new SleepStep(ms);
            throw new FormatException($"sleep の引数が数値ではありません: {line}");
        }
        if (line.StartsWith("expect ", StringComparison.OrdinalIgnoreCase))
        {
            var rest = line[7..].Trim();
            var pattern = ParseQuoted(rest);
            var consumed = QuotedLength(rest);
            var tail = rest[consumed..].Trim();
            var timeout = string.IsNullOrEmpty(tail) ? 30_000 : int.Parse(tail);
            return new ExpectStep(Unescape(pattern), timeout);
        }
        throw new FormatException($"不明なマクロ行: {line}");
    }

    private static string ParseQuoted(string s)
    {
        if (s.Length < 2 || s[0] != '"') throw new FormatException("ダブルクォートで囲ってください");
        var sb = new StringBuilder();
        var i = 1;
        while (i < s.Length && s[i] != '"')
        {
            if (s[i] == '\\' && i + 1 < s.Length) { sb.Append(s[i]); sb.Append(s[i + 1]); i += 2; }
            else { sb.Append(s[i]); i++; }
        }
        return sb.ToString();
    }

    private static int QuotedLength(string s)
    {
        var i = 1;
        while (i < s.Length && s[i] != '"')
        {
            if (s[i] == '\\' && i + 1 < s.Length) i += 2; else i++;
        }
        return i + 1;
    }

    private static string Unescape(string s) => s
        .Replace("\\r", "\r")
        .Replace("\\n", "\n")
        .Replace("\\t", "\t")
        .Replace("\\\"", "\"")
        .Replace("\\\\", "\\");
}

public sealed class MacroRunner
{
    public async Task RunAsync(IReadOnlyList<MacroStep> steps, ITerminalChannel channel, IAsyncEnumerable<ReadOnlyMemory<byte>> receivedStream, CancellationToken ct)
    {
        // 受信ストリームを別タスクで読み続け、Expect で文字列マッチ判定を行う。
        var buffer = new StringBuilder();
        var lockObj = new object();
        using var readerCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var readerTask = Task.Run(async () =>
        {
            try
            {
                await foreach (var chunk in receivedStream.WithCancellation(readerCts.Token))
                {
                    var s = Encoding.UTF8.GetString(chunk.Span);
                    lock (lockObj) buffer.Append(s);
                }
            }
            catch (OperationCanceledException) { }
        }, readerCts.Token);

        try
        {
            foreach (var step in steps)
            {
                ct.ThrowIfCancellationRequested();
                switch (step)
                {
                    case SendStep s:
                        await channel.WriteAsync(Encoding.UTF8.GetBytes(s.Text), ct);
                        break;
                    case SleepStep s:
                        await Task.Delay(s.Milliseconds, ct);
                        break;
                    case ExpectStep e:
                        await WaitForAsync(buffer, lockObj, e.Pattern, e.TimeoutMs, ct);
                        break;
                }
            }
        }
        finally
        {
            await readerCts.CancelAsync();
            try { await readerTask; } catch { /* ignore */ }
        }
    }

    private static async Task WaitForAsync(StringBuilder buffer, object lockObj, string pattern, int timeoutMs, CancellationToken ct)
    {
        var until = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTime.UtcNow < until)
        {
            ct.ThrowIfCancellationRequested();
            lock (lockObj)
            {
                if (buffer.ToString().Contains(pattern, StringComparison.Ordinal)) return;
            }
            await Task.Delay(50, ct);
        }
        throw new TimeoutException($"expect timed out waiting for: {pattern}");
    }
}
