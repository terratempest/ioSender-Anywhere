#!/usr/bin/env bash
# Publish ioSender for Linux x64
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
OUT_DIR="$ROOT/artifacts/publish/linux-x64"
dotnet publish "$ROOT/ioSender/ioSender.csproj" \
  -c Release \
  -r linux-x64 \
  --self-contained false \
  -o "$OUT_DIR"
echo "Published to $OUT_DIR"
