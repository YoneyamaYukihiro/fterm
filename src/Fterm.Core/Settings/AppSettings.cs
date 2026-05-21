namespace Fterm.Core.Settings;

public enum ThemeKind { Auto, Light, Dark }
public enum AppLanguage { Ja, En }

public sealed record AppSettings
{
    public ThemeKind Theme { get; init; } = ThemeKind.Dark;
    public AppLanguage Language { get; init; } = AppLanguage.Ja;
    public int ScrollbackLines { get; init; } = 10_000;
    public int TransferConcurrency { get; init; } = 4;
    public int LogRetentionDays { get; init; } = 14;
}
