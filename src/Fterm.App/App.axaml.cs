using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using System.Net.Http;
using System.Runtime.InteropServices;
using Fterm.Core.Connections;
using Fterm.Core.Logging;
using Fterm.Core.Security;
using Fterm.Core.Settings;
using Fterm.Core.Updates;
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

            // バックグラウンドで更新確認（失敗は黙って無視）。
            _ = Task.Run(async () =>
            {
                try
                {
                    using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
                    var checker = new UpdateChecker(http);
                    var url = Environment.GetEnvironmentVariable("FTERM_UPDATE_MANIFEST_URL");
                    if (string.IsNullOrEmpty(url)) return;
                    var info = await checker.CheckAsync(url, RuntimeInformation.RuntimeIdentifier, CancellationToken.None);
                    if (info is not null)
                    {
                        Log.Information("Update available: {Version} (current {Current})", info.NewVersion, info.CurrentVersion);
                    }
                }
                catch { /* swallow */ }
            });
        }
        base.OnFrameworkInitializationCompleted();
    }
}
