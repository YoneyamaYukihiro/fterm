using Avalonia.Controls;
using Fterm.Core.Connections;
using Fterm.Core.Security;
using Fterm.UI.ViewModels;
using Fterm.UI.Views;

namespace Fterm.UI.Services;

public sealed class ConnectionEditorService : IConnectionEditorService
{
    private readonly Window _owner;
    private readonly ICredentialStore _credentialStore;

    public ConnectionEditorService(Window owner, ICredentialStore credentialStore)
    {
        _owner = owner;
        _credentialStore = credentialStore;
    }

    public async Task<Connection?> EditAsync(Connection? existing)
    {
        var vm = new ConnectionEditorViewModel(_credentialStore, existing);
        await vm.LoadCredentialsAsync(existing?.CredentialId);

        var dialog = new ConnectionEditorWindow
        {
            DataContext = vm,
            Title = existing is null ? "新規接続" : $"接続の編集: {existing.Name}",
        };

        var result = await dialog.ShowDialog<Connection?>(_owner);
        return result;
    }
}
