using Fterm.Core.Localization;
using Fterm.Core.Settings;
using Xunit;

namespace Fterm.Core.Tests;

public class LocalizerTests
{
    [Fact]
    public void Returns_japanese_by_default()
    {
        var l = new Localizer();
        Assert.Equal("ファイル(_F)", l.T("menu.file"));
    }

    [Fact]
    public void Switches_to_english()
    {
        var l = new Localizer { Language = AppLanguage.En };
        Assert.Equal("_File", l.T("menu.file"));
    }

    [Fact]
    public void Falls_back_to_key_when_missing()
    {
        var l = new Localizer();
        Assert.Equal("nope.does.not.exist", l.T("nope.does.not.exist"));
    }

    [Fact]
    public void Format_with_args()
    {
        var l = new Localizer();
        Assert.Equal("2/5 件", l.TF("search.matches", 2, 5));
    }
}
