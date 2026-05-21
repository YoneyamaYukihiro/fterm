using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Fterm.Core.Settings;
using Fterm.UI.Controls;
using Fterm.UI.Services;
using Fterm.UI.ViewModels;

namespace Fterm.UI.Views;

public partial class MainWindow : Window
{
    /// <summary>App.axaml.cs から注入。設定ダイアログから保存された設定を受け取る。</summary>
    public IAppSettingsStore? SettingsStore { get; set; }
    public ThemeService? ThemeService { get; set; }

    public MainWindow()
    {
        InitializeComponent();
        KeyDown += OnMainKeyDown;
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

    private void OnMainKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (e is { Key: Key.F, KeyModifiers: KeyModifiers.Control })
        {
            if (vm.SelectedTab is TerminalTabViewModel t) { t.ShowSearchCommand.Execute(null); e.Handled = true; }
        }
    }

    private void OnSearchKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;
        if (e.Key == Key.Escape && vm.SelectedTab is TerminalTabViewModel t)
        {
            t.HideSearchCommand.Execute(null);
            e.Handled = true;
        }
    }

    private void OnFindMenuClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm && vm.SelectedTab is TerminalTabViewModel t)
        {
            t.ShowSearchCommand.Execute(null);
        }
    }

    private async void OnPreferencesClicked(object? sender, RoutedEventArgs e)
    {
        if (SettingsStore is null) return;
        var current = await SettingsStore.LoadAsync();
        var dlg = new SettingsDialog
        {
            DataContext = new SettingsViewModel(current),
        };
        var result = await dlg.ShowDialog<AppSettings?>(this);
        if (result is not null)
        {
            await SettingsStore.SaveAsync(result);
            ThemeService?.Apply(result.Theme);
        }
    }
}
