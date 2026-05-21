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
- 「ファイル → エコータブを開く」では `EchoTerminalChannel` に対して送受信できます
  （M3 で実 SSH に置き換え予定）。

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
  Fterm.Protocols.Ssh/    SSH / SFTP アダプタ        (M3/M4)
  Fterm.Protocols.Ftp/    FTP / FTPS アダプタ        (M5)
  Fterm.Protocols.Serial/ シリアルアダプタ           (M5)
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
| M3 | ⬜ | SSH 接続、VT パーサ最小実装 |
| M4 | ⬜ | SFTP 二画面、転送キュー |
| M5 | ⬜ | FTP / FTPS / Telnet / シリアル |
| M6 | ⬜ | 検索、ログ、マクロ、テーマ、i18n |
| M7 | ⬜ | テスト整備、署名済みインストーラ、自動更新 |
