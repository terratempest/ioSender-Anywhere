#!/bin/bash
# Build Linux package(s) from ~/ioSender-build (populated by build-all.ps1 via rsync).
# Arg1: WSL export dir on /mnt/c/... for copying artifacts back to Windows.
# Arg2: package target (LinuxPublish, LinuxDeb, LinuxRpm, LinuxAppImage, Linux).
# Arg3: RID (linux-x64 or linux-arm64).
# Arg4: reuse existing publish output (0 or 1).
set -eu

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$PATH"

BUILD_DIR="${IOSENDER_WSL_BUILD_DIR:-$HOME/ioSender-build}"
EXPORT_DIR="${1:-}"
TARGET="${2:-LinuxDeb}"
RID="${3:-linux-x64}"
REUSE_PUBLISH="${4:-0}"

if [[ ! -f "$BUILD_DIR/scripts/build-linux.sh" ]]; then
  echo "error: build tree missing at $BUILD_DIR (run build-all.ps1 sync first)" >&2
  exit 1
fi

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: dotnet not found in WSL." >&2
  echo "  Install: curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0" >&2
  echo "  Expected: $DOTNET_ROOT/dotnet" >&2
  exit 1
fi

echo "==> dotnet $(dotnet --version) ($DOTNET_ROOT)"

fix_crlf() {
  local f
  for f in "$@"; do
    [[ -f "$f" ]] || continue
    sed -i 's/\r$//' "$f"
  done
}

fix_crlf "$BUILD_DIR/scripts/"*.sh
fix_crlf "$BUILD_DIR/packaging/debian/DEBIAN/postinst"
fix_crlf "$BUILD_DIR/packaging/debian/usr/bin/iosender" 2>/dev/null || true
chmod +x "$BUILD_DIR/scripts/"*.sh

echo "==> Building $TARGET for $RID in $BUILD_DIR"
cd "$BUILD_DIR"
if [[ "$TARGET" == "LinuxPublish" ]]; then
  RID="$RID" ./scripts/publish-linux.sh
else
  RID="$RID" IOSENDER_REUSE_PUBLISH="$REUSE_PUBLISH" ./scripts/build-linux.sh "$TARGET" "$RID"
fi

shopt -s nullglob
case "$TARGET" in
  LinuxPublish)
    files=()
    ;;
  LinuxDeb|Deb|deb)
    files=("$BUILD_DIR/artifacts/"iosender_*.deb)
    ;;
  LinuxRpm|Rpm|rpm)
    files=("$BUILD_DIR/artifacts/"ioSender-*-"$RID".rpm)
    ;;
  LinuxAppImage|AppImage|appimage)
    files=("$BUILD_DIR/artifacts/"ioSender-*-"$RID".AppImage)
    ;;
  All|Linux)
    files=("$BUILD_DIR/artifacts/"iosender_*.deb "$BUILD_DIR/artifacts/"ioSender-*-"$RID".rpm "$BUILD_DIR/artifacts/"ioSender-*-"$RID".AppImage)
    ;;
  *)
    echo "error: unknown target: $TARGET" >&2
    exit 1
    ;;
esac

if [[ "$TARGET" == "LinuxPublish" ]]; then
  if [[ ! -f "$BUILD_DIR/artifacts/publish/$RID/ioSender" ]]; then
    echo "error: publish output missing: $BUILD_DIR/artifacts/publish/$RID/ioSender" >&2
    exit 1
  fi
  echo "==> Published $RID output"
  exit 0
fi

if (( ${#files[@]} == 0 )); then
  echo "error: no artifacts produced in $BUILD_DIR/artifacts" >&2
  exit 1
fi

if [[ -n "$EXPORT_DIR" ]]; then
  mkdir -p "$EXPORT_DIR"
  for artifact in "${files[@]}"; do
    cp -f "$artifact" "$EXPORT_DIR/"
    echo "==> Copied $(basename "$artifact") -> $EXPORT_DIR/"
  done
else
  for artifact in "${files[@]}"; do
    echo "==> Built $(basename "$artifact") (no export dir)"
  done
fi
