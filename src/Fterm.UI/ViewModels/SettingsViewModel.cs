using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fterm.Core.Settings;

namespace Fterm.UI.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty] private ThemeKind _theme;
    [ObservableProperty] private AppLanguage _language;
    [ObservableProperty] private int _scrollbackLines;
    [ObservableProperty] private int _transferConcurrency;
    [ObservableProperty] private int _logRetentionDays;

    public ObservableCollection<ThemeKind> Themes { get; } =
        [ThemeKind.Auto, ThemeKind.Light, ThemeKind.Dark];
    public ObservableCollection<AppLanguage> Languages { get; } =
        [AppLanguage.Ja, AppLanguage.En];

    public AppSettings? Result { get; private set; }

    public SettingsViewModel(AppSettings current)
    {
        _theme = current.Theme;
        _language = current.Language;
        _scrollbackLines = current.ScrollbackLines;
        _transferConcurrency = current.TransferConcurrency;
        _logRetentionDays = current.LogRetentionDays;
    }

    [RelayCommand]
    private void Save()
    {
        Result = new AppSettings
        {
            Theme = Theme,
            Language = Language,
            ScrollbackLines = ScrollbackLines,
            TransferConcurrency = TransferConcurrency,
            LogRetentionDays = LogRetentionDays,
        };
    }

    [RelayCommand]
    private void Cancel() => Result = null;
}
