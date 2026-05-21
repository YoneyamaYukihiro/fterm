using Fterm.Core.Settings;

namespace Fterm.Core.Localization;

/// <summary>
/// 言語コードでキー→文字列を解決する軽量ローカライザ。
/// resx よりシンプルで、ビューモデル単体テストもしやすい。
/// </summary>
public sealed class Localizer
{
    private static readonly Dictionary<string, Dictionary<string, string>> Tables = new()
    {
        ["ja"] = new()
        {
            ["app.title"] = "fterm",
            ["menu.file"] = "ファイル(_F)",
            ["menu.file.newConnection"] = "新規接続(_N)...",
            ["menu.file.quickConnection"] = "クイック接続(_Q)...",
            ["menu.file.connect"] = "接続",
            ["menu.file.openEchoTab"] = "エコータブを開く(_E)",
            ["menu.file.exit"] = "終了(_X)",
            ["menu.edit"] = "編集(_E)",
            ["menu.edit.copy"] = "コピー",
            ["menu.edit.paste"] = "貼り付け",
            ["menu.edit.find"] = "検索...",
            ["menu.view"] = "表示(_V)",
            ["menu.view.sidePanel"] = "サイドパネル",
            ["menu.connection"] = "接続(_C)",
            ["menu.connection.new"] = "新規...",
            ["menu.connection.edit"] = "編集...",
            ["menu.connection.duplicate"] = "複製",
            ["menu.connection.delete"] = "削除",
            ["menu.transfer"] = "転送(_T)",
            ["menu.transfer.openSftp"] = "SFTP を開く",
            ["menu.transfer.openQueue"] = "転送キューを開く",
            ["menu.settings"] = "設定(_S)",
            ["menu.settings.preferences"] = "環境設定...",
            ["menu.help"] = "ヘルプ(_H)",
            ["menu.help.about"] = "バージョン情報",
            ["sidebar.connections"] = "接続帳",
            ["status.ready"] = "Ready",
            ["settings.title"] = "環境設定",
            ["settings.theme"] = "テーマ",
            ["settings.theme.auto"] = "自動",
            ["settings.theme.light"] = "ライト",
            ["settings.theme.dark"] = "ダーク",
            ["settings.language"] = "言語",
            ["settings.language.ja"] = "日本語",
            ["settings.language.en"] = "English",
            ["settings.scrollback"] = "スクロールバック行数",
            ["settings.transferConcurrency"] = "転送同時実行数",
            ["settings.logRetention"] = "ログ保持日数",
            ["dialog.ok"] = "OK",
            ["dialog.cancel"] = "キャンセル",
            ["dialog.save"] = "保存",
            ["search.placeholder"] = "検索 (Esc で閉じる)",
            ["search.matches"] = "{0}/{1} 件",
        },
        ["en"] = new()
        {
            ["app.title"] = "fterm",
            ["menu.file"] = "_File",
            ["menu.file.newConnection"] = "_New Connection...",
            ["menu.file.quickConnection"] = "_Quick Connect...",
            ["menu.file.connect"] = "Connect",
            ["menu.file.openEchoTab"] = "Open _Echo Tab",
            ["menu.file.exit"] = "E_xit",
            ["menu.edit"] = "_Edit",
            ["menu.edit.copy"] = "Copy",
            ["menu.edit.paste"] = "Paste",
            ["menu.edit.find"] = "Find...",
            ["menu.view"] = "_View",
            ["menu.view.sidePanel"] = "Side Panel",
            ["menu.connection"] = "_Connection",
            ["menu.connection.new"] = "New...",
            ["menu.connection.edit"] = "Edit...",
            ["menu.connection.duplicate"] = "Duplicate",
            ["menu.connection.delete"] = "Delete",
            ["menu.transfer"] = "_Transfer",
            ["menu.transfer.openSftp"] = "Open SFTP",
            ["menu.transfer.openQueue"] = "Open Transfer Queue",
            ["menu.settings"] = "_Settings",
            ["menu.settings.preferences"] = "Preferences...",
            ["menu.help"] = "_Help",
            ["menu.help.about"] = "About",
            ["sidebar.connections"] = "Connections",
            ["status.ready"] = "Ready",
            ["settings.title"] = "Preferences",
            ["settings.theme"] = "Theme",
            ["settings.theme.auto"] = "Auto",
            ["settings.theme.light"] = "Light",
            ["settings.theme.dark"] = "Dark",
            ["settings.language"] = "Language",
            ["settings.language.ja"] = "日本語",
            ["settings.language.en"] = "English",
            ["settings.scrollback"] = "Scrollback lines",
            ["settings.transferConcurrency"] = "Transfer concurrency",
            ["settings.logRetention"] = "Log retention (days)",
            ["dialog.ok"] = "OK",
            ["dialog.cancel"] = "Cancel",
            ["dialog.save"] = "Save",
            ["search.placeholder"] = "Search (Esc to close)",
            ["search.matches"] = "{0}/{1} matches",
        },
    };

    public AppLanguage Language { get; set; } = AppLanguage.Ja;

    public string T(string key)
    {
        var lang = Language == AppLanguage.En ? "en" : "ja";
        if (Tables.TryGetValue(lang, out var table) && table.TryGetValue(key, out var v)) return v;
        if (Tables["ja"].TryGetValue(key, out var fallback)) return fallback;
        return key;
    }

    public string TF(string key, params object[] args) => string.Format(T(key), args);
}
