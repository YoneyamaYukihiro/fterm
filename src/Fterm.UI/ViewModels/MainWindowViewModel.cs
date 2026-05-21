using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fterm.Core.Connections;

namespace Fterm.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IConnectionStore _store;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private TabItemViewModel? _selectedTab;

    public ObservableCollection<TabItemViewModel> Tabs { get; } = [];
    public ObservableCollection<Connection> Connections { get; } = [];

    public MainWindowViewModel(IConnectionStore store)
    {
        _store = store;
    }

    public async Task InitializeAsync()
    {
        Connections.Clear();
        foreach (var c in await _store.LoadAllAsync())
        {
            Connections.Add(c);
        }
        StatusText = $"Loaded {Connections.Count} connection(s)";
    }

    [RelayCommand]
    private void NewEchoTab()
    {
        var tab = new TabItemViewModel($"echo {Tabs.Count + 1}");
        _ = tab.StartAsync();
        Tabs.Add(tab);
        SelectedTab = tab;
    }

    [RelayCommand]
    private void CloseTab(TabItemViewModel tab)
    {
        Tabs.Remove(tab);
        _ = tab.DisposeAsync();
    }
}
