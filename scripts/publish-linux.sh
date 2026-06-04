#!/usr/bin/env bash
# Publish ioSender for Linux x64 (self-contained)
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="${OUT_DIR:-$ROOT/artifacts/publish/linux-x64}"

if [[ -d "$OUT_DIR" ]]; then
  rm -rf "$OUT_DIR"
fi
mkdir -p "$OUT_DIR"

publish() {
  dotnet publish "$ROOT/ioSender/ioSender.csproj" \
    -c Release \
    -r linux-x64 \
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
