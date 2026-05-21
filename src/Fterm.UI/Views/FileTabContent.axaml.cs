using Avalonia.Controls;
using Avalonia.Input;
using Fterm.UI.ViewModels;

namespace Fterm.UI.Views;

public partial class FileTabContent : UserControl
{
    public FileTabContent()
    {
        InitializeComponent();

        var local = this.FindControl<FilePaneView>("LocalPane")!;
        var remote = this.FindControl<FilePaneView>("RemotePane")!;
        local.Activated += (_, _) => SetActive(isLocal: true);
        remote.Activated += (_, _) => SetActive(isLocal: false);
    }

    private void SetActive(bool isLocal)
    {
        if (DataContext is not FileTabViewModel vm) return;
        vm.ActivePane = isLocal ? vm.Local : vm.Remote;
    }

    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not FileTabViewModel vm) return;
        switch (e.Key)
        {
            case Key.F5:
                vm.TransferSelected();
                e.Handled = true;
                break;
            case Key.F8:
            case Key.Delete:
                await vm.ActivePane.DeleteSelectedAsync();
                e.Handled = true;
                break;
            case Key.Back:
                await vm.ActivePane.GoUpAsync();
                e.Handled = true;
                break;
            case Key.Enter:
                await vm.ActivePane.ActivateAsync();
                e.Handled = true;
                break;
        }
    }
}
