using System.ComponentModel;
using Fterm.Core.Localization;
using Fterm.Core.Settings;
using Xunit;

namespace Fterm.Core.Tests;

public class LocalizerLiveSwitchTests
{
    [Fact]
    public void Indexer_returns_translated_string()
    {
        var l = new Localizer { Language = AppLanguage.Ja };
        Assert.Equal("ファイル(_F)", l["menu.file"]);
        l.Language = AppLanguage.En;
        Assert.Equal("_File", l["menu.file"]);
    }

    [Fact]
    public void Language_change_raises_indexer_property_changed()
    {
        var l = new Localizer { Language = AppLanguage.Ja };
        var raised = new List<string?>();
        l.PropertyChanged += (_, e) => raised.Add(e.PropertyName);
        l.Language = AppLanguage.En;
        Assert.Contains("Item[]", raised);
    }

    [Fact]
    public void No_event_when_language_unchanged()
    {
        var l = new Localizer { Language = AppLanguage.Ja };
        var raised = 0;
        l.PropertyChanged += (_, _) => raised++;
        l.Language = AppLanguage.Ja;
        Assert.Equal(0, raised);
    }
}
