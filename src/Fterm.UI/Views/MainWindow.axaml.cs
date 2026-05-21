using Avalonia.Controls;
using Avalonia.Input;
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
}
