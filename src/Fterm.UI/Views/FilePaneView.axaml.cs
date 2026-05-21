using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Fterm.UI.ViewModels;

namespace Fterm.UI.Views;

public partial class FilePaneView : UserControl
{
    public event EventHandler? Activated;

    public FilePaneView()
    {
        InitializeComponent();
    }

    private async void OnEntryDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is FileBrowserViewModel vm)
        {
            await vm.ActivateAsync();
        }
    }

    private async void OnAddressKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox box) return;
        if (DataContext is FileBrowserViewModel vm)
        {
            await vm.NavigateAsync(box.Text ?? "");
        }
    }

    private void OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        Activated?.Invoke(this, EventArgs.Empty);
    }
}
