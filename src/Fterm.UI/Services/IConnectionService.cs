using Fterm.Core.Connections;
using Fterm.UI.ViewModels;

namespace Fterm.UI.Services;

public interface IConnectionService
{
    Task<TerminalTabViewModel?> OpenTerminalAsync(Connection connection, CancellationToken ct);
    Task<FileTabViewModel?> OpenFileBrowserAsync(Connection connection, CancellationToken ct);
}
