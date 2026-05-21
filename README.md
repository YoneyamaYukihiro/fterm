# fterm

TeraTerm（SSH / Telnet / シリアル）と FFFTP（SFTP / FTP / FTPS）を統合した、UI 重視のデスクトップアプリ。

- 言語/ランタイム: C# 14 / **.NET 10 (LTS)**
- UI: Avalonia 11.3 (Win / macOS / Linux)
- 設計書: [`docs/DESIGN.md`](docs/DESIGN.md)

## ビルド

```bash
dotnet build fterm.slnx
```

## 実行

```bash
dotnet run --project src/Fterm.App
```

起動すると、メニュー / ツールバー / サイドパネル（接続帳）/ タブ領域 / ステータスバーで構成される
メインウィンドウが開きます。

- 「ファイル → 新規接続...」または「新規接続」ボタンで接続編集ダイアログを開けます。
- 接続帳のリストから選んで「編集」「複製」「削除」が可能（ダブルクリックで編集）。
- 接続データは `%APPDATA%/fterm/connections.json`、資格情報は AES-GCM で暗号化して
  `%APPDATA%/fterm/credentials.json` に保存されます。マスター鍵は
  `%APPDATA%/fterm/master.key`（非 Windows では 0600 権限）。
- 接続帳で接続を選んで「接続」ボタン（または Ctrl+Enter）で SSH ターミナルタブを開きます。
  初回接続時はホスト鍵フィンガープリント確認ダイアログが出ます。
- 「SFTP」ボタン（または 転送 → SFTP を開く）で同じ接続定義に対する二画面ファイルブラウザを開きます。
  - F5: 選択ファイルを反対のペインへ転送
  - Enter / ダブルクリック: ディレクトリへ移動
  - Backspace: 親ディレクトリ
  - F8 / Delete: 削除
  - アドレスバーで直接入力 → Enter で移動
  - 下部に転送キュー（並列 4、進捗バー、エラー表示）
- 「ファイル → エコータブを開く」では `EchoTerminalChannel` に対する開発用エコータブが開きます。

## 結合テスト

ローカルに sshd / FTP サーバを立てて環境変数を渡すと結合テストが実行されます。
（環境変数未設定のテストは黙ってスキップされます）

```bash
FTERM_SSH_HOST=127.0.0.1 FTERM_SSH_PORT=2222 \
FTERM_SSH_USER=youruser FTERM_SSH_PASS=yourpass \
FTERM_FTP_HOST=127.0.0.1 FTERM_FTP_PORT=2121 \
FTERM_FTP_USER=youruser FTERM_FTP_PASS=yourpass \
  dotnet test tests/Fterm.Integration.Tests
```

## テスト

```bash
dotnet test fterm.slnx
```

## プロジェクト構成

```
src/
  Fterm.App/              Avalonia エントリポイント
  Fterm.UI/               View / ViewModel
  Fterm.Core/             ドメイン + アプリケーションサービス
  Fterm.Terminal/         VT パーサ / ターミナルバッファ (M3 で実装)
  Fterm.Protocols.Ssh/    SSH / SFTP アダプタ
  Fterm.Protocols.Ftp/    FTP / FTPS アダプタ (FluentFTP)
  Fterm.Protocols.Telnet/ Telnet アダプタ (IAC 交渉)
  Fterm.Protocols.Serial/ シリアルアダプタ (System.IO.Ports)
  Fterm.Security/         資格情報・鍵管理            (M2)
tests/
  Fterm.Core.Tests/
  Fterm.Terminal.Tests/
  Fterm.Integration.Tests/ (Docker 必要、M3 以降で有効化)
```

## ロードマップ

| マイルストーン | 状態 | 内容 |
|---|---|---|
| M0 | ✅ | 設計確定 |
| M1 | ✅ | プロジェクト雛形、空ウィンドウ + メニュー、`EchoTerminalChannel` で UI 結線確認 |
| M2 | ✅ | 接続マネージャ UI（新規 / 編集 / 複製 / 削除）、AES-GCM 暗号化された資格情報ストア |
| M3 | ✅ | SSH 接続（SSH.NET + 既知ホスト鍵キャッシュ + 確認ダイアログ）、VT パーサ最小実装、Avalonia ターミナルコントロール |
| M4 | ✅ | SFTP 二画面ファイルブラウザ、転送キュー（並列実行 / キャンセル / 進捗表示）、ローカル ↔ リモート転送 |
| M5 | ✅ | FTP / FTPS（FluentFTP）、Telnet（IAC 交渉ハンドリング）、シリアル（System.IO.Ports）、ディレクトリ再帰転送 |
| M6 | ⬜ | 検索、ログ、マクロ、テーマ、i18n |
| M7 | ⬜ | テスト整備、署名済みインストーラ、自動更新 |
