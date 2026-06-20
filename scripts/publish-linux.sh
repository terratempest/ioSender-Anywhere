#!/usr/bin/env bash
# Publish ioSender for Linux (self-contained).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="${RID:-linux-x64}"
OUT_DIR="${OUT_DIR:-$ROOT/artifacts/publish/$RID}"

case "$RID" in
  linux-x64|linux-arm64) ;;
  *)
    echo "error: unsupported Linux RID: $RID" >&2
    exit 1
    ;;
esac

if [[ -d "$OUT_DIR" ]]; then
  rm -rf "$OUT_DIR"
fi
mkdir -p "$OUT_DIR"

publish() {
  dotnet publish "$ROOT/ioSender/ioSender.csproj" \
    -c Release \
    -r "$RID" \
    -m:1 \
    --disable-build-servers \
    --self-contained true \
    -p:PublishSingleFile=false \
    --force \
    -o "$OUT_DIR"
}

publish_log="$(mktemp)"
if ! publish 2>&1 | tee "$publish_log"; then
  if grep -q "Root element is missing" "$publish_log" && grep -q "/.nuget/packages/" "$publish_log"; then
    echo "Detected a corrupt NuGet package cache in WSL; clearing global packages and retrying publish ..."
    dotnet nuget locals global-packages --clear
    publish
  else
    exit 1
  fi
fi
rm -f "$publish_log"
echo "Published to $OUT_DIR"
