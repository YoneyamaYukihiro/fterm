using Avalonia.Controls;
using Avalonia.Input;
using Fterm.UI.Controls;
using Fterm.UI.ViewModels;

namespace Fterm.UI.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    protected override async void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        if (DataContext is MainWindowViewModel vm)
        {
            await vm.InitializeAsync();
        }
    }

    private void OnConnectionDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (vm.EditConnectionCommand.CanExecute(null))
        {
            vm.EditConnectionCommand.Execute(null);
        }
    }

    private void OnTerminalUserInput(object? sender, TerminalInputEventArgs e)
    {
        if (sender is not TerminalControl ctl) return;
        if (ctl.DataContext is TerminalTabViewModel tab)
        {
            _ = tab.SendAsync(e.Data);
        }
    }

    private void OnTerminalUserResize(object? sender, TerminalResizeEventArgs e)
    {
        if (sender is not TerminalControl ctl) return;
        if (ctl.DataContext is TerminalTabViewModel tab)
        {
            _ = tab.ResizeAsync(e.Cols, e.Rows);
        }
    }
}
