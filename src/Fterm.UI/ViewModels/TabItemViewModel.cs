using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fterm.Core.Sessions;

namespace Fterm.UI.ViewModels;

public sealed partial class TabItemViewModel : ViewModelBase, IAsyncDisposable
{
    private readonly ITerminalChannel _channel;
    private readonly CancellationTokenSource _cts = new();

    [ObservableProperty]
    private string _title;

    [ObservableProperty]
    private string _output = "";

    [ObservableProperty]
    private string _input = "";

    public TabItemViewModel(string title) : this(title, new EchoTerminalChannel())
    {
    }

    public TabItemViewModel(string title, ITerminalChannel channel)
    {
        _title = title;
        _channel = channel;
    }

    public async Task StartAsync()
    {
        await _channel.ConnectAsync(_cts.Token);
        _ = Task.Run(async () =>
        {
            await foreach (var chunk in _channel.ReadAsync(_cts.Token))
            {
                var text = Encoding.UTF8.GetString(chunk.Span);
                Output += text;
            }
        }, _cts.Token);
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        var line = Input + "\r\n";
        Input = "";
        await _channel.WriteAsync(Encoding.UTF8.GetBytes(line), _cts.Token);
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        await _channel.DisposeAsync();
        _cts.Dispose();
    }
}
