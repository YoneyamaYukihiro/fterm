#!/usr/bin/env bash
# fterm リリースビルドスクリプト。
# 引数:
#   $1: バージョン (例 1.2.3)。省略時は Directory.Build.props の VersionPrefix。
#   $2: 出力先ディレクトリ (既定: ./artifacts)
#
# 出力:
#   artifacts/<rid>/fterm           (Linux/macOS)
#   artifacts/<rid>/fterm.exe       (Windows)
#   artifacts/fterm-<version>-<rid>.tar.gz もしくは .zip
#   artifacts/manifest.json          (UpdateChecker が読む形式)
#
# コード署名は本スクリプトでは行わない。各 OS の署名ステップは
# CI ジョブで別途行うか、ローカルで `codesign` / `signtool` を実行する。
set -euo pipefail

VERSION="${1:-}"
OUT="${2:-./artifacts}"
mkdir -p "$OUT"

if [[ -z "$VERSION" ]]; then
  VERSION=$(grep -oP '(?<=<VersionPrefix>)[^<]+' Directory.Build.props || echo "0.0.0-dev")
fi

echo "Publishing fterm v$VERSION to $OUT"

declare -a RIDS=("win-x64" "linux-x64" "osx-x64" "osx-arm64")

declare -A MANIFEST_ENTRIES=()

for RID in "${RIDS[@]}"; do
  echo ""
  echo "=== Publishing for $RID ==="
  RID_OUT="$OUT/$RID"
  rm -rf "$RID_OUT"
  dotnet publish src/Fterm.App/Fterm.App.csproj \
    -c Release \
    -r "$RID" \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:Version="$VERSION" \
    -p:InformationalVersion="$VERSION" \
    -o "$RID_OUT"

  # アーカイブ
  if [[ "$RID" == win-* ]]; then
    ARCHIVE="$OUT/fterm-$VERSION-$RID.zip"
    (cd "$RID_OUT" && zip -qr "../fterm-$VERSION-$RID.zip" .)
  else
    ARCHIVE="$OUT/fterm-$VERSION-$RID.tar.gz"
    tar -C "$RID_OUT" -czf "$ARCHIVE" .
  fi

  # SHA-256
  SHA=$(sha256sum "$ARCHIVE" | awk '{print $1}')
  SIZE=$(stat -c '%s' "$ARCHIVE" 2>/dev/null || stat -f '%z' "$ARCHIVE")
  BASENAME=$(basename "$ARCHIVE")
  MANIFEST_ENTRIES["$RID"]="\"$RID\":{\"url\":\"$BASENAME\",\"sha256\":\"$SHA\",\"size\":$SIZE}"
  echo "  $ARCHIVE  sha256=$SHA"
done

# manifest.json を生成
{
  echo "{"
  echo "  \"version\": \"$VERSION\","
  echo "  \"notes\": \"fterm $VERSION\","
  echo "  \"assets\": {"
  FIRST=1
  for RID in "${RIDS[@]}"; do
    if [[ $FIRST -eq 1 ]]; then FIRST=0; else echo ","; fi
    echo -n "    ${MANIFEST_ENTRIES[$RID]}"
  done
  echo ""
  echo "  }"
  echo "}"
} > "$OUT/manifest.json"

echo ""
echo "Done. See $OUT/manifest.json"
