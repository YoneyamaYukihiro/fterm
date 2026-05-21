using CommunityToolkit.Mvvm.ComponentModel;
using Fterm.Core.Sessions;
using Fterm.Terminal.Buffer;
using Fterm.Terminal.Emulator;
using Fterm.Terminal.Vt;

namespace Fterm.UI.ViewModels;

/// <summary>
/// 実 <see cref="ITerminalChannel"/> に接続するターミナルタブ。
/// バッファとパーサを保持し、チャネルからのバイト列をパーサに流し続ける。
/// </summary>
public sealed partial class TerminalTabViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly ITerminalChannel _channel;
    private readonly VtParser _parser;
    private readonly CancellationTokenSource _cts = new();

    public TerminalEmulator Emulator { get; }
    public TerminalBuffer Buffer => Emulator.Buffer;

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private bool _isConnected;

    public TerminalTabViewModel(string title, ITerminalChannel channel, int initialCols = 100, int initialRows = 30)
    {
        _title = title;
        _channel = channel;
        var buffer = new TerminalBuffer(initialCols, initialRows);
        Emulator = new TerminalEmulator(buffer);
        Emulator.TitleChanged += (_, _) =>
        {
            if (!string.IsNullOrEmpty(Buffer.Title)) Title = Buffer.Title;
        };
        _parser = new VtParser(Emulator);
    }

    public async Task StartAsync()
    {
        await _channel.ConnectAsync(_cts.Token);
        IsConnected = true;
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var chunk in _channel.ReadAsync(_cts.Token))
                {
                    _parser.Feed(chunk.Span);
                }
            }
            catch (OperationCanceledException) { /* normal */ }
            finally
            {
                IsConnected = false;
            }
        }, _cts.Token);
    }

    public ValueTask SendAsync(ReadOnlyMemory<byte> data) => _channel.WriteAsync(data, _cts.Token);

    public Task ResizeAsync(int cols, int rows)
    {
        Buffer.Resize(cols, rows);
        return _channel.ResizeAsync(cols, rows, _cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        await _channel.DisposeAsync();
        _cts.Dispose();
    }
}
