#!/bin/bash
# Copy Linux package artifacts from the WSL build tree to a Windows-visible export dir.
set -eu

EXPORT_DIR="${1:-}"
RID="${2:-linux-x64}"
if [[ -z "$EXPORT_DIR" ]]; then
  echo "error: export dir argument required" >&2
  exit 1
fi

shopt -s nullglob
files=(
  ~/ioSender-build/artifacts/iosender_*.deb
  ~/ioSender-build/artifacts/ioSender-*-"$RID".rpm
  ~/ioSender-build/artifacts/ioSender-*-"$RID".AppImage
)
if (( ${#files[@]} == 0 )); then
  echo "error: no package artifacts in ~/ioSender-build/artifacts" >&2
  exit 1
fi

mkdir -p "$EXPORT_DIR"
for artifact in "${files[@]}"; do
  cp -f "$artifact" "$EXPORT_DIR/"
  echo "==> Copied $(basename "$artifact") -> $EXPORT_DIR/"
done
