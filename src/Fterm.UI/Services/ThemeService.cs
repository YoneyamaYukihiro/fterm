using Avalonia;
using Avalonia.Styling;
using Fterm.Core.Settings;

namespace Fterm.UI.Services;

public sealed class ThemeService
{
    public void Apply(ThemeKind theme)
    {
        var app = Application.Current;
        if (app is null) return;
        app.RequestedThemeVariant = theme switch
        {
            ThemeKind.Light => ThemeVariant.Light,
            ThemeKind.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}
