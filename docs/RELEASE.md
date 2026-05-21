# リリース手順

## 1. ローカルでの発行（テスト用）

```bash
./scripts/publish.sh 1.2.3
# -> artifacts/{win-x64,linux-x64,osx-x64,osx-arm64}/ に成果物
# -> artifacts/manifest.json も生成
```

## 2. CI 経由のリリース

タグ `vX.Y.Z` を push すると `.github/workflows/release.yml` が以下を行います:

1. 各 RID (win-x64 / linux-x64 / osx-x64 / osx-arm64) で
   `dotnet publish -c Release --self-contained -p:PublishSingleFile=true` を実行
2. tar.gz / zip にアーカイブ
3. SHA-256 入りの `manifest.json` を生成
4. GitHub Release に全アーティファクトをアップロード

```bash
git tag v1.2.3
git push origin v1.2.3
```

## 3. コード署名

CI ワークフローは **署名を行わない**。本物の配布前に各 OS で署名してください。

### Windows (Authenticode)

```powershell
signtool sign /fd SHA256 /td SHA256 /tr http://timestamp.digicert.com \
              /f cert.pfx /p $env:CERT_PASSWORD \
              .\fterm.exe
```

CI に組み込む場合は `Azure Trusted Signing` か `signtool` + シークレットからの
証明書取得を推奨。

### macOS (codesign + notarytool)

```bash
codesign --deep --force --options runtime \
         --sign "Developer ID Application: Your Name (TEAMID)" \
         fterm
# 公証
zip -r fterm.zip fterm
xcrun notarytool submit fterm.zip \
      --apple-id "$APPLE_ID" --team-id "$TEAM_ID" --password "$APP_PASSWORD" \
      --wait
xcrun stapler staple fterm
```

### Linux

GPG 署名のみ:

```bash
gpg --armor --detach-sign --output fterm-1.2.3-linux-x64.tar.gz.asc \
    fterm-1.2.3-linux-x64.tar.gz
```

## 4. 自動更新マニフェスト

`manifest.json` を Release Assets にアップロードしておけば、
クライアントから

```
https://github.com/<owner>/<repo>/releases/latest/download/manifest.json
```

として参照できます。`Fterm.Core.Updates.UpdateChecker` がこの形式を読みます。

形式:

```json
{
  "version": "1.2.3",
  "notes": "...",
  "assets": {
    "win-x64":  { "url": "...", "sha256": "...", "size": 12345 },
    "linux-x64": { "url": "...", "sha256": "...", "size": 12345 },
    "osx-x64":  { "url": "...", "sha256": "...", "size": 12345 },
    "osx-arm64": { "url": "...", "sha256": "...", "size": 12345 }
  }
}
```

## 5. 自動更新クライアント側の流れ

1. 起動時 (もしくはメニュー操作) で
   `UpdateChecker.CheckAsync(manifestUrl, RuntimeInformation.RuntimeIdentifier)`
2. 新バージョン検出時に UI 通知 (非モーダル)
3. ユーザ承諾後にダウンロード → `VerifyAsync(path, asset.Sha256)` で検証
4. 旧バイナリ横に置いて再起動 → swap

現状 M7 で実装されているのは (1)(3) の API のみ。
(2)(4) の UI フローは次マイルストーンで追加予定。
