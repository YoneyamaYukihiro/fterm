using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Fterm.Core.Connections;
using Fterm.UI.ViewModels;
using Fterm.UI.Views;

namespace Fterm.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var store = new JsonConnectionStore(JsonConnectionStore.DefaultPath());
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(store),
            };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
