using Avalonia.Controls;
using Avalonia.Interactivity;
using Fterm.Core.Connections;
using Fterm.UI.ViewModels;

namespace Fterm.UI.Views;

public partial class ConnectionEditorWindow : Window
{
    public ConnectionEditorWindow()
    {
        InitializeComponent();
    }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConnectionEditorViewModel vm) return;
        // SaveCommand は async なのでバインディング側に任せ、完了後に Result を確認する。
        // 直接呼んで完了を待つことで Close 時に Result が確定している状態を作る。
        await vm.SaveCommand.ExecuteAsync(null);
        if (vm.Result is Connection result)
        {
            Close(result);
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }
}
