using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Fterm.Core.Connections;
using Fterm.Core.Security;
using Fterm.Security;
using Fterm.UI.Services;
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
            IConnectionStore connectionStore = new JsonConnectionStore(JsonConnectionStore.DefaultPath());
            var masterKey = MasterKeyProvider.GetOrCreate(MasterKeyProvider.DefaultPath());
            ICredentialStore credentialStore = new EncryptedCredentialStore(EncryptedCredentialStore.DefaultPath(), masterKey);

            var window = new MainWindow();
            var editorService = new ConnectionEditorService(window, credentialStore);
            window.DataContext = new MainWindowViewModel(connectionStore, credentialStore, editorService);
            desktop.MainWindow = window;
        }
        base.OnFrameworkInitializationCompleted();
    }
}
