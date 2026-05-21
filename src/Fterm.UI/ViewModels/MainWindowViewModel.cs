using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fterm.Core.Connections;
using Fterm.Core.Security;
using Fterm.UI.Services;

namespace Fterm.UI.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IConnectionStore _store;
    private readonly ICredentialStore _credentials;
    private readonly IConnectionEditorService _editor;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private TabItemViewModel? _selectedTab;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditConnectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(DeleteConnectionCommand))]
    [NotifyCanExecuteChangedFor(nameof(DuplicateConnectionCommand))]
    private Connection? _selectedConnection;

    public ObservableCollection<TabItemViewModel> Tabs { get; } = [];
    public ObservableCollection<Connection> Connections { get; } = [];

    public MainWindowViewModel(IConnectionStore store, ICredentialStore credentials, IConnectionEditorService editor)
    {
        _store = store;
        _credentials = credentials;
        _editor = editor;
    }

    public async Task InitializeAsync()
    {
        Connections.Clear();
        foreach (var c in (await _store.LoadAllAsync()).OrderBy(c => c.Name))
        {
            Connections.Add(c);
        }
        StatusText = $"接続帳: {Connections.Count} 件";
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

    [RelayCommand]
    private async Task NewConnectionAsync()
    {
        var result = await _editor.EditAsync(null);
        if (result is null) return;
        await _store.SaveAsync(result);
        InsertSorted(result);
        SelectedConnection = result;
        StatusText = $"追加: {result.Name}";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedConnection))]
    private async Task EditConnectionAsync()
    {
        if (SelectedConnection is null) return;
        var result = await _editor.EditAsync(SelectedConnection);
        if (result is null) return;
        await _store.SaveAsync(result);
        var idx = Connections.IndexOf(SelectedConnection);
        if (idx >= 0) Connections[idx] = result;
        SelectedConnection = result;
        StatusText = $"更新: {result.Name}";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedConnection))]
    private async Task DeleteConnectionAsync()
    {
        if (SelectedConnection is null) return;
        var target = SelectedConnection;
        await _store.DeleteAsync(target.Id);
        Connections.Remove(target);
        SelectedConnection = null;
        StatusText = $"削除: {target.Name}";
    }

    [RelayCommand(CanExecute = nameof(HasSelectedConnection))]
    private async Task DuplicateConnectionAsync()
    {
        if (SelectedConnection is null) return;
        var copy = SelectedConnection with
        {
            Id = Guid.NewGuid(),
            Name = SelectedConnection.Name + " (copy)",
        };
        await _store.SaveAsync(copy);
        InsertSorted(copy);
        SelectedConnection = copy;
        StatusText = $"複製: {copy.Name}";
    }

    private bool HasSelectedConnection => SelectedConnection is not null;

    private void InsertSorted(Connection c)
    {
        var idx = 0;
        while (idx < Connections.Count &&
               string.Compare(Connections[idx].Name, c.Name, StringComparison.CurrentCultureIgnoreCase) < 0)
        {
            idx++;
        }
        Connections.Insert(idx, c);
    }
}
