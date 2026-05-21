using Avalonia.Controls;
using Avalonia.Interactivity;
using Fterm.Core.Settings;
using Fterm.UI.ViewModels;

namespace Fterm.UI.Views;

public partial class SettingsDialog : Window
{
    public SettingsDialog()
    {
        InitializeComponent();
    }

    private void OnSave(object? sender, RoutedEventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
        {
            vm.SaveCommand.Execute(null);
            Close(vm.Result);
        }
    }

    private void OnCancel(object? sender, RoutedEventArgs e) => Close((AppSettings?)null);
}
