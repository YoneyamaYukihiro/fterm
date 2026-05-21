using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Fterm.Core.Connections;
using Fterm.Core.Logging;
using Fterm.Core.Security;
using Fterm.Core.Settings;
using Fterm.Protocols.Ssh;
using Fterm.Security;
using Fterm.UI.Services;
using Fterm.UI.ViewModels;
using Fterm.UI.Views;
using Serilog;

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
            IAppSettingsStore settingsStore = new JsonAppSettingsStore(JsonAppSettingsStore.DefaultPath());
            var settings = settingsStore.LoadAsync().GetAwaiter().GetResult();

            LoggingSetup.Initialize(retentionDays: settings.LogRetentionDays);
            Log.Information("fterm starting up. theme={Theme} language={Language}", settings.Theme, settings.Language);

            IConnectionStore connectionStore = new JsonConnectionStore(JsonConnectionStore.DefaultPath());
            var masterKey = MasterKeyProvider.GetOrCreate(MasterKeyProvider.DefaultPath());
            ICredentialStore credentialStore = new EncryptedCredentialStore(EncryptedCredentialStore.DefaultPath(), masterKey);
            var knownHosts = new KnownHostsStore(KnownHostsStore.DefaultPath());

            var window = new MainWindow();
            var themeService = new ThemeService();
            themeService.Apply(settings.Theme);
            window.SettingsStore = settingsStore;
            window.ThemeService = themeService;

            var editorService = new ConnectionEditorService(window, credentialStore);
            var hostKeyPolicy = new InteractiveHostKeyPolicy(window);
            var connectionService = new SshConnectionService(credentialStore, knownHosts, hostKeyPolicy);
            window.DataContext = new MainWindowViewModel(connectionStore, credentialStore, editorService, connectionService);
            desktop.MainWindow = window;

            desktop.Exit += (_, _) => { Log.Information("fterm exiting."); Log.CloseAndFlush(); };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
