#!/bin/bash
# Copy the latest iosender_*.deb from the WSL build tree to a Windows-visible export dir.
set -eu

EXPORT_DIR="${1:-}"
if [[ -z "$EXPORT_DIR" ]]; then
  echo "error: export dir argument required" >&2
  exit 1
fi

shopt -s nullglob
files=(~/ioSender-build/artifacts/iosender_*.deb)
if (( ${#files[@]} == 0 )); then
  echo "error: no .deb in ~/ioSender-build/artifacts" >&2
  exit 1
fi

deb="${files[-1]}"
mkdir -p "$EXPORT_DIR"
cp -f "$deb" "$EXPORT_DIR/"
echo "==> Copied $(basename "$deb") -> $EXPORT_DIR/"
