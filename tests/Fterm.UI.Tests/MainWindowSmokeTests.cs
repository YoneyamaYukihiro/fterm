using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Fterm.Core.Connections;
using Fterm.Core.Security;
using Fterm.UI.Services;
using Fterm.UI.ViewModels;
using Fterm.UI.Views;
using Xunit;

namespace Fterm.UI.Tests;

public class MainWindowSmokeTests
{
    private sealed class NullEditorService : IConnectionEditorService
    {
        public Task<Connection?> EditAsync(Connection? existing) => Task.FromResult<Connection?>(null);
    }

    private sealed class NullConnectionService : IConnectionService
    {
        public Task<TerminalTabViewModel?> OpenTerminalAsync(Connection c, CancellationToken ct) => Task.FromResult<TerminalTabViewModel?>(null);
        public Task<FileTabViewModel?> OpenFileBrowserAsync(Connection c, CancellationToken ct) => Task.FromResult<FileTabViewModel?>(null);
    }

    private sealed class InMemoryConnectionStore : IConnectionStore
    {
        private readonly List<Connection> _items = [];
        public Task<IReadOnlyList<Connection>> LoadAllAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<Connection>>(_items.ToList());
        public Task SaveAsync(Connection connection, CancellationToken ct = default)
        {
            _items.RemoveAll(c => c.Id == connection.Id);
            _items.Add(connection);
            return Task.CompletedTask;
        }
        public Task DeleteAsync(Guid id, CancellationToken ct = default)
        {
            _items.RemoveAll(c => c.Id == id);
            return Task.CompletedTask;
        }
    }

    private sealed class InMemoryCredentialStore : ICredentialStore
    {
        public Task<IReadOnlyList<CredentialSummary>> ListAsync(CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<CredentialSummary>>([]);
        public Task<Credential?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<Credential?>(null);
        public Task SaveAsync(Credential credential, CancellationToken ct = default) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken ct = default) => Task.CompletedTask;
    }

    [AvaloniaFact]
    public void Window_opens_with_empty_state()
    {
        var window = new MainWindow();
        window.DataContext = new MainWindowViewModel(
            new InMemoryConnectionStore(),
            new InMemoryCredentialStore(),
            new NullEditorService(),
            new NullConnectionService());
        window.Show();
        Assert.True(window.IsVisible);
        Assert.Equal("fterm", window.Title);
    }

    [AvaloniaFact]
    public void NewEchoTab_command_adds_a_tab()
    {
        var vm = new MainWindowViewModel(
            new InMemoryConnectionStore(),
            new InMemoryCredentialStore(),
            new NullEditorService(),
            new NullConnectionService());
        vm.NewEchoTabCommand.Execute(null);
        Assert.Single(vm.Tabs);
    }
}
