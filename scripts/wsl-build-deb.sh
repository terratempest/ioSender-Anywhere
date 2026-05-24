#!/bin/bash
# Build .deb from ~/ioSender-build (populated by build-all.ps1 via rsync).
# Arg1: WSL export dir on /mnt/c/... for copying the .deb back to Windows.
set -eu

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$PATH"

BUILD_DIR="${IOSENDER_WSL_BUILD_DIR:-$HOME/ioSender-build}"
EXPORT_DIR="${1:-}"

if [[ ! -f "$BUILD_DIR/scripts/package-deb.sh" ]]; then
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

if ! command -v dpkg-deb >/dev/null 2>&1; then
  echo "error: dpkg-deb not found. Run: sudo apt install -y dpkg-dev" >&2
  exit 1
fi

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

echo "==> Building .deb in $BUILD_DIR"
cd "$BUILD_DIR"
./scripts/package-deb.sh

DEB="$(ls -1 "$BUILD_DIR/artifacts/"iosender_*.deb 2>/dev/null | tail -1)"
if [[ -z "$DEB" ]]; then
  echo "error: no .deb produced in $BUILD_DIR/artifacts" >&2
  exit 1
fi

if [[ -n "$EXPORT_DIR" ]]; then
  mkdir -p "$EXPORT_DIR"
  cp -f "$DEB" "$EXPORT_DIR/"
  echo "==> Copied $(basename "$DEB") -> $EXPORT_DIR/"
else
  echo "==> Built $(basename "$DEB") (no export dir)"
fi
