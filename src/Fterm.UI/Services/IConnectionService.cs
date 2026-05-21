using Fterm.Core.Connections;
using Fterm.UI.ViewModels;

namespace Fterm.UI.Services;

/// <summary>
/// 接続定義から実際のセッション (ViewModel タブ) を作る責務。
/// プロトコル分岐 + 認証情報の取得を担う。
/// </summary>
public interface IConnectionService
{
    Task<TerminalTabViewModel?> OpenTerminalAsync(Connection connection, CancellationToken ct);
}
