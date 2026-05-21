using Fterm.Core.Settings;
using Xunit;

namespace Fterm.Core.Tests;

public class AppSettingsTests
{
    [Fact]
    public async Task Load_returns_defaults_when_file_missing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fterm-set-{Guid.NewGuid():N}.json");
        var store = new JsonAppSettingsStore(path);
        var loaded = await store.LoadAsync();
        Assert.Equal(ThemeKind.Dark, loaded.Theme);
        Assert.Equal(AppLanguage.Ja, loaded.Language);
    }

    [Fact]
    public async Task Save_and_load_roundtrip()
    {
        var path = Path.Combine(Path.GetTempPath(), $"fterm-set-{Guid.NewGuid():N}.json");
        try
        {
            var store = new JsonAppSettingsStore(path);
            await store.SaveAsync(new AppSettings
            {
                Theme = ThemeKind.Light,
                Language = AppLanguage.En,
                ScrollbackLines = 5000,
                TransferConcurrency = 8,
                LogRetentionDays = 30,
            });
            var loaded = await store.LoadAsync();
            Assert.Equal(ThemeKind.Light, loaded.Theme);
            Assert.Equal(AppLanguage.En, loaded.Language);
            Assert.Equal(5000, loaded.ScrollbackLines);
            Assert.Equal(8, loaded.TransferConcurrency);
            Assert.Equal(30, loaded.LogRetentionDays);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
