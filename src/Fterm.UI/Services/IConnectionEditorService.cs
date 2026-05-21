using Fterm.Core.Connections;

namespace Fterm.UI.Services;

/// <summary>
/// 接続編集ダイアログを開く責務を View 側に注入するための抽象。
/// ViewModel から直接 Avalonia の Window を扱わないために用意する。
/// </summary>
public interface IConnectionEditorService
{
    /// <summary>新規 / 既存接続の編集ダイアログを開く。キャンセル時は null。</summary>
    Task<Connection?> EditAsync(Connection? existing);
}
