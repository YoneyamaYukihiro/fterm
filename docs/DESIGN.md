# fterm 設計書

TeraTerm（ターミナル / SSH / Telnet / シリアル）と FFFTP（FTP / SFTP クライアント）の
両方の役割を 1 つにまとめた、UI の充実したデスクトップアプリケーションの設計書。

本書はコード実装前のフル機能設計フェーズの成果物である。

---

## 1. 目的と方針

### 1.1 目的
- TeraTerm 的なターミナル機能（SSH / Telnet / シリアル）
- FFFTP 的なファイル転送機能（SFTP / FTP / FTPS）
- これらを **同一ウィンドウ・同一セッション設定** で扱える統合 UI を提供する。
- 接続先（ホスト）単位で「ターミナル」「ファイル転送」をタブ／ペインで切替できる。
- 初心者にも扱いやすい GUI と、上級者向けのキーボード操作・スクリプトの両立。

### 1.2 非目的（v1 では扱わない）
- VPN クライアント機能
- リモートデスクトップ（RDP / VNC）
- ターミナル多重化（tmux 風機能の自前実装）
- モバイル版

### 1.3 設計原則
- **接続情報は 1 か所**で管理する（ターミナル用と FTP 用で二重管理しない）。
- 既存ユーザ体験の踏襲：TeraTerm / FFFTP 両ツールのキーバインドや UI 慣習を可能な限り温存。
- セキュリティ最優先：パスワード / 鍵情報の保護、ホスト鍵検証、最小限の権限。
- クロスプラットフォーム対応を視野に入れる（後述の Avalonia 採用根拠）。

---

## 2. 技術スタック

### 2.1 採用スタック
| 項目 | 採用 | 備考 |
|---|---|---|
| 言語 | C# 12 / .NET 8 LTS | 長期サポート版 |
| UI フレームワーク | **Avalonia 11**（推奨） | クロスプラットフォーム（Win/macOS/Linux）。WPF 互換の XAML。 |
| MVVM | CommunityToolkit.Mvvm | ソースジェネレータで `ObservableObject` 等を簡潔に。 |
| DI | Microsoft.Extensions.DependencyInjection | 標準的。 |
| ロギング | Serilog | 構造化ログ。ファイル + コンソール sink。 |
| 設定永続化 | JSON + DataProtection API（Win） / libsecret（Linux） / Keychain（macOS） | OS の安全領域に保管。 |
| SSH / SFTP | **SSH.NET** | 枯れていて広く使用される。 |
| FTP / FTPS | **FluentFTP** | FTPS、明示的・暗黙的双方サポート。 |
| ターミナル描画 | **AvaloniaEdit** ベース or 自前の VT100/xterm エミュレータ層 | VT パーサ（後述）を内製。 |
| シリアル | System.IO.Ports | .NET 標準。 |

### 2.2 WPF か Avalonia か
- **WPF**: Windows 専用。Windows でのネイティブ感は高い。
- **Avalonia**: Windows/macOS/Linux で動作。WPF と書き味が近い XAML。
- TeraTerm/FFFTP は歴史的に Windows 主体だが、現代では macOS/Linux 利用者が多く、
  Avalonia の方が将来性が高い。
- **結論：Avalonia を採用**。XAML スタイルを WPF 風に書き、将来 WPF 移植が必要でも乗り換えコストを抑える。

### 2.3 主要 NuGet 依存
- `Avalonia`, `Avalonia.Desktop`, `Avalonia.Themes.Fluent`
- `CommunityToolkit.Mvvm`
- `Microsoft.Extensions.Hosting`
- `Serilog`, `Serilog.Sinks.File`
- `SSH.NET`
- `FluentFTP`
- `System.IO.Ports`

---

## 3. アーキテクチャ

### 3.1 レイヤ構成
```
+---------------------------------------------+
|  UI (Views / XAML)         Avalonia         |
+---------------------------------------------+
|  ViewModels                MVVM             |
+---------------------------------------------+
|  Application Services                       |
|   - SessionManager                          |
|   - ConnectionStore                         |
|   - TransferQueue                           |
|   - MacroEngine                             |
+---------------------------------------------+
|  Domain                                     |
|   - Session, Connection, FileEntry          |
|   - TerminalBuffer, VTParser                |
+---------------------------------------------+
|  Infrastructure                             |
|   - SshAdapter (SSH.NET)                    |
|   - SftpAdapter (SSH.NET)                   |
|   - FtpAdapter (FluentFTP)                  |
|   - SerialAdapter                           |
|   - CredentialStore (DPAPI/Keychain/...)    |
+---------------------------------------------+
```

### 3.2 プロジェクト構成（ソリューション）
```
fterm.sln
├─ src/
│  ├─ Fterm.App/              Avalonia エントリ
│  ├─ Fterm.UI/               View / ViewModel
│  ├─ Fterm.Core/             ドメイン + アプリケーションサービス
│  ├─ Fterm.Terminal/         VT パーサ / ターミナルバッファ
│  ├─ Fterm.Protocols.Ssh/    SSH/SFTP アダプタ
│  ├─ Fterm.Protocols.Ftp/    FTP/FTPS アダプタ
│  ├─ Fterm.Protocols.Serial/ シリアル
│  └─ Fterm.Security/         資格情報・鍵管理
└─ tests/
   ├─ Fterm.Core.Tests/
   ├─ Fterm.Terminal.Tests/   VT パーサのゴールデンテスト
   └─ Fterm.Integration.Tests/ Docker (openssh-server, vsftpd) で結合試験
```

---

## 4. ドメインモデル

### 4.1 中核エンティティ
- `Connection`：保存済み接続定義（名前、ホスト、ポート、プロトコル、認証情報 ID、UI 設定、文字コード等）。
- `Session`：実行中のセッション。`Connection` を元に開かれる。
  - `TerminalSession`（SSH/Telnet/Serial 用）
  - `FileSession`（SFTP/FTP/FTPS 用）
  - 同一 `Connection` から両方同時に開ける（後述：複合タブ）。
- `Credential`：パスワード、秘密鍵、パスフレーズ。本体は `CredentialStore` に保管し、ID で参照。
- `KnownHost`：ホスト鍵のフィンガープリント。OpenSSH 互換の `known_hosts` 風フォーマットで保存。
- `TransferTask`：1 ファイル / 1 ディレクトリの転送ジョブ。状態（待機/転送中/完了/失敗）を持つ。
- `TerminalBuffer`：行 × 列のセル配列＋スクロールバック。属性（色、太字、下線、リバース等）。

### 4.2 プロトコル抽象
```csharp
public interface ITerminalChannel : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken ct);
    ValueTask WriteAsync(ReadOnlyMemory<byte> data, CancellationToken ct);
    IAsyncEnumerable<ReadOnlyMemory<byte>> ReadAsync(CancellationToken ct);
    Task ResizeAsync(int cols, int rows);
    event EventHandler<DisconnectedEventArgs>? Disconnected;
}

public interface IFileChannel : IAsyncDisposable
{
    Task ConnectAsync(CancellationToken ct);
    IAsyncEnumerable<RemoteEntry> ListAsync(string path, CancellationToken ct);
    Task<Stream> OpenReadAsync(string path, CancellationToken ct);
    Task<Stream> OpenWriteAsync(string path, CancellationToken ct);
    Task MakeDirectoryAsync(string path, CancellationToken ct);
    Task DeleteAsync(string path, bool recursive, CancellationToken ct);
    Task RenameAsync(string from, string to, CancellationToken ct);
    Task ChmodAsync(string path, UnixFileMode mode, CancellationToken ct);  // SFTP/FTP の差は実装側で吸収
}
```

---

## 5. UI 設計

### 5.1 全体レイアウト
```
┌────────────────────────────────────────────────────────────────────┐
│  メニューバー（ファイル/編集/表示/接続/転送/設定/ヘルプ）           │
├────────────────────────────────────────────────────────────────────┤
│  ツールバー [新規接続] [切断] [複製] [SFTP] [マクロ] [設定] ...    │
├──────────┬─────────────────────────────────────────────────────────┤
│ サイド    │  タブ群: [📡 prod-web] [📁 prod-web (SFTP)] [📡 db] ... │
│ パネル   ├─────────────────────────────────────────────────────────┤
│ ・接続帳 │                                                          │
│ ・お気入  │              アクティブタブの中身                        │
│ ・履歴   │                                                          │
│ ・転送   │   ※ ターミナルタブ または 二画面ファイル転送タブ        │
│   キュー │                                                          │
├──────────┴─────────────────────────────────────────────────────────┤
│  ステータスバー: 接続状態 / 文字コード / 行列 / 改行コード / 暗号  │
└────────────────────────────────────────────────────────────────────┘
```

- **サイドパネル**は表示/非表示切替可。F9 でトグル。
- **タブ**はドラッグで並べ替え／別ウィンドウへ分離可（DockManager 風）。
- **ペイン分割**：ターミナルは上下/左右分割可（同一ホストに複数チャネル）。

### 5.2 主要画面

#### 5.2.1 ようこそ画面（起動直後 / 接続なし時）
- 「新規接続」「最近使った接続」「クイック接続フォーム」を 1 画面に統合。
- クイック接続：`user@host:port` の 1 行入力＋プロトコル選択で即接続。

#### 5.2.2 接続マネージャ（接続帳）
- TeraTerm の "TTSSH SSH Authentication" や FFFTP の "ホストの設定" を統合した画面。
- ツリー：フォルダで接続をグルーピング。タグ付け（prod / staging / dev など）。
- 編集タブ：
  - **基本**：名前、ホスト、ポート、プロトコル（SSH/Telnet/SFTP/FTP/FTPS/Serial）。
  - **認証**：パスワード / 公開鍵（ファイル選択 + パスフレーズ）/ keyboard-interactive / エージェント。
  - **ターミナル**：文字コード（UTF-8/Shift_JIS/EUC-JP）、改行（CR/LF/CRLF）、TERM 名（xterm-256color 等）、フォント、色テーマ。
  - **ファイル転送**：初期リモートディレクトリ、初期ローカルディレクトリ、転送モード（自動/ASCII/バイナリ）、PASV/PORT、暗号化レベル。
  - **プロキシ**：HTTP/SOCKS4/5、踏み台（ProxyJump 相当）。
  - **詳細**：keep-alive、再接続、起動時マクロ、環境変数。

#### 5.2.3 ターミナルタブ
- VT100/xterm 互換エミュレーション。
- 機能：
  - **256 色 / True Color (24bit)**
  - **マウス報告**（vim/tmux 等で利用）
  - **OSC 8 ハイパーリンク**
  - **検索**：Ctrl+F、正規表現可、ヒット箇所ハイライト。
  - **スクロールバック**：行数設定可（既定 10,000）、エクスポート。
  - **コピペ**：TeraTerm 風「選択即コピー」設定もオプションで提供。
  - **テキスト保存 / ログ取得**：開始-停止、自動ローテーション。
  - **送信履歴**：上矢印で過去送信、サーバ側 history と区別。
  - **複数行貼り付け確認**：誤投入防止。

#### 5.2.4 ファイル転送タブ（二画面）
- 左ペイン：ローカル、右ペイン：リモート。
- 各ペインの構成：パンくず＋アドレスバー / 一覧（名前・サイズ・日時・パーミッション・所有者）/ ステータス。
- 操作：
  - ドラッグ＆ドロップで転送（左→右、右→左、外部エクスプローラ ↔ ペインも可）。
  - キーボードで F5 コピー、F6 移動、F7 新規フォルダ、F8 削除、F2 リネーム（FFFTP / 多くのファイラ準拠）。
  - **再帰転送、衝突時の方針**（上書き / リネーム / スキップ、サイズ・日時比較）。
  - **転送キュー**：複数ジョブを並列／逐次実行、レジューム対応（SFTP/FTP REST）。
  - **同期モード**：ミラー / 双方向、差分ハッシュ確認。
  - **属性編集**：chmod ダイアログ（数値・記号両対応）。
  - **テキスト編集**：内蔵簡易エディタ（AvaloniaEdit）で開く → 保存時に自動アップロード。

#### 5.2.5 複合タブ（ターミナル + ファイル転送）
- 1 つのタブ内で上下分割：上ターミナル、下 SFTP。
- 同一 SSH 接続上に SFTP サブシステムを多重化（SSH.NET の同一クライアント再利用）。

#### 5.2.6 転送キュー画面
- サイドパネルまたは独立ウィンドウ。
- 並び順変更、優先度、一時停止、再開、失敗の再試行。

#### 5.2.7 設定ダイアログ
- **全般**：起動時動作、自動更新、言語（日本語/英語）、テーマ（Fluent Light/Dark/Auto）。
- **ターミナル**：既定フォント、色テーマ、カーソル形状、ベル、貼り付け確認しきい値。
- **ファイル**：既定ローカル/リモート、転送既定、衝突方針、一覧キャッシュ。
- **セキュリティ**：known_hosts の場所、ホスト鍵変化時の挙動、資格情報ストア。
- **ショートカット**：キーバインド編集（プリセット：default / TeraTerm 風 / FFFTP 風 / Custom）。
- **プロキシ**：グローバル既定。
- **マクロ**：エディタ統合・実行履歴。

### 5.3 ショートカット（既定案、抜粋）
| 操作 | キー |
|---|---|
| 新規接続 | Ctrl+N |
| クイック接続 | Ctrl+Shift+N |
| タブ切替 | Ctrl+Tab / Ctrl+Shift+Tab |
| タブ閉じる | Ctrl+W |
| 同一ホストでファイル転送タブを開く | Ctrl+B |
| 検索 | Ctrl+F |
| 設定 | Ctrl+, |
| ターミナル文字コード切替 | Alt+E |
| サイドパネル | F9 |
| ファイル一覧の更新 | F5 |
| 転送キューを開く | Ctrl+J |

---

## 6. 機能詳細

### 6.1 接続フロー
1. 接続マネージャまたはクイック接続で `Connection` を選択／入力。
2. `SessionManager.OpenAsync(connection, channelKind)` が呼ばれる。
3. プロトコル判定 → 対応アダプタを生成 → 認証情報を `CredentialStore` から取得。
4. ホスト鍵検証（SSH）：
   - 既知 → 続行。
   - 未知 → ダイアログで指紋を表示し、ユーザ承認後に `known_hosts` に追加。
   - 変化 → 強い警告ダイアログ。続行は明示操作必須、ログにも残す。
5. 認証：パスワード → 公開鍵 → keyboard-interactive → エージェント の順に試行（設定で順序変更可）。
6. 成功時に対応するタブを生成。

### 6.2 ターミナルエミュレーション
- **VT パーサ**は ANSI X3.64 / xterm の主要シーケンスをサポート：
  CSI、OSC、DCS、SGR (0/1/3/4/7/22/24/27/30-37/38;5/38;2/40-47/48;5/48;2/90-97/100-107)、
  カーソル制御、画面消去、スクロール領域、保存/復帰カーソル、代替バッファ、
  マウス報告 (1000/1006)、ブラケット貼り付け (2004)、タイトル設定 (OSC 0/2)、
  リンク (OSC 8)、True Color。
- **入力**：キーイベント → VT シーケンス変換テーブル（xterm 準拠）。
- **再描画**：差分のみ Canvas に Draw（仮想化）。
- **コピー時の整形**：行折り返しを論理行に戻して改行付与。

### 6.3 ファイル転送
- 同時並列数は設定可（既定 4）。
- レジューム：SFTP は `SSH_FXP_OPEN` のオフセット指定、FTP は `REST` コマンド。
- ハッシュ照合：SFTP は `check-file`（OpenSSH 拡張）が使えれば利用、無ければ部分 SHA-256。
- 速度制限：上り/下り別。トークンバケットで実装。
- 衝突方針：
  - 「常に上書き」「常にスキップ」「サイズ/日時で判断」「都度確認」。
- 文字コード変換：FTP のみ。リスト/ファイル名は UTF-8/Shift_JIS を切替可。

### 6.4 マクロ / スクリプト
- **TTL ライク言語**（TeraTerm の TTL に近い独自の小さな DSL）：
  - `connect`, `wait`, `sendln`, `setdir`, `getfile`, `putfile`, `if`, `for` ...
  - サンドボックスで実行、外部プロセス起動はオプトイン。
- **C# スクリプト**（Roslyn Scripting）も別タブで実行可（上級者向け）。
- **記録**：操作からマクロを自動生成（任意）。

### 6.5 ログ・監査
- 接続イベント、認証成功/失敗、ホスト鍵変化、転送結果を JSON Lines で保存。
- ターミナル本文ログは別ファイルにテキスト or タイムスタンプ付き。
- ログ閲覧画面（フィルタ、エクスポート）。

---

## 7. セキュリティ設計

### 7.1 資格情報の保管
- パスワード／パスフレーズ／秘密鍵は OS の安全領域に保管。
  - Windows: DPAPI (`ProtectedData`)
  - macOS: Keychain
  - Linux: Secret Service (libsecret) / KWallet
- 設定 JSON にはハッシュ ID のみを保持し、平文を残さない。

### 7.2 ホスト鍵
- OpenSSH 互換 `known_hosts` を読み書き（ハッシュ化ホスト名対応）。
- 鍵変化時は **デフォルトで接続拒否**。明示的に許可した場合のみ続行。

### 7.3 ネットワーク
- 既定でアルゴリズムは最新世代を優先：
  - 鍵交換：curve25519-sha256, ecdh-sha2-nistp*, diffie-hellman-group14-sha256
  - 暗号：chacha20-poly1305, aes-256-gcm, aes-128-gcm
  - MAC：hmac-sha2-256-etm, hmac-sha2-512-etm
  - 弱いアルゴリズム（arcfour, MD5 MAC, ssh-dss）は既定で無効、ユーザが明示的に有効化可能。
- FTPS は明示モード既定、暗黙モードも選択可。
- SOCKS / HTTP プロキシ、SSH 多段（ProxyJump）対応。

### 7.4 メモリ
- パスワード／鍵バイト列は使用後に `CryptographicOperations.ZeroMemory` で消去。

### 7.5 自動更新
- 署名付きインストーラのハッシュ検証を必須。

---

## 8. 国際化（i18n）

- 既定リソースは日本語と英語。
- リソース：`.resx` ベース、ViewModel から `IStringLocalizer` で参照。
- 日付・サイズはロケール準拠（バイト/KiB/MiB はオプション）。

---

## 9. テスト戦略

- **VT パーサ**：xterm のテストケース（vttest 出力）をゴールデンとしたユニットテスト。
- **SSH/SFTP**：Docker（`linuxserver/openssh-server`）に対する結合テスト。
- **FTP/FTPS**：Docker（`vsftpd`、自己署名 TLS）。
- **UI**：Avalonia.Headless でビューモデルとビューの結線をテスト。
- **CI**：GitHub Actions、Win/macOS/Linux のマトリクス。

---

## 10. ロードマップ

| マイルストーン | 内容 |
|---|---|
| M0（本書） | 設計確定 |
| M1 | プロジェクト雛形、Avalonia 起動、空のタブ／メニュー |
| M2 | 接続マネージャ、設定永続化、資格情報ストア |
| M3 | SSH 接続 + VT パーサ最小実装、ターミナルタブで対話可 |
| M4 | SFTP 二画面、ドラッグ＆ドロップ転送、転送キュー |
| M5 | FTP/FTPS、Telnet、シリアル |
| M6 | 検索、ログ、マクロ（TTL ライク）、テーマ、i18n |
| M7 | テスト整備、署名済みインストーラ、自動更新 |

---

## 11. リスクと対策

| リスク | 対策 |
|---|---|
| VT エミュレーションの不完全さ | 早期に vim/htop/tmux 等の代表ソフトでの動作確認、ゴールデンテスト整備。 |
| Avalonia の描画パフォーマンス | ターミナルは独自 `Control` で `DrawingContext` 直接描画＋差分更新。 |
| SSH.NET の保守状況 | アダプタで隠蔽し、必要なら `Renci.SshNet` フォークや別実装に差替可能に。 |
| クロスプラットフォームでの資格情報差 | `ICredentialStore` でプラットフォーム実装を切替。 |
| 文字コード混在 | 接続単位＋セッション中切替の両方を提供。 |

---

## 12. 命名

- 作業名：**fterm**（"f" = FFFTP の f / file / fast、term = terminal）
- 製品名候補（要決定）：fterm / TeraFile / Sshell / OneTerm

---

## 13. 次のアクション（M1 着手の準備）

1. ソリューションとプロジェクト雛形の生成。
2. Avalonia アプリの起動確認（空ウィンドウ＋メニュー）。
3. `Connection` モデル、`ConnectionStore`（JSON 永続化）の実装。
4. ダミーの `TerminalChannel` で UI 上にエコー表示できることを確認。
5. CI（GitHub Actions）でビルド／テスト実行を確立。

