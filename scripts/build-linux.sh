#!/usr/bin/env bash
# Build Linux package targets on a native Linux host.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
TARGET="${1:-${TARGET:-All}}"
RID="${2:-${RID:-}}"

resolve_host_rid() {
  case "$(uname -m)" in
    x86_64|amd64)
      echo "linux-x64"
      ;;
    aarch64|arm64)
      echo "linux-arm64"
      ;;
    *)
      echo "error: unsupported Linux host architecture: $(uname -m)" >&2
      return 1
      ;;
  esac
}

HOST_RID="$(resolve_host_rid)"
if [[ -z "$RID" ]]; then
  RID="$HOST_RID"
fi

case "$RID" in
  linux-x64|linux-arm64) ;;
  *)
    echo "error: unsupported Linux RID: $RID" >&2
    exit 1
    ;;
esac

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$PATH"

if ! command -v dotnet >/dev/null 2>&1; then
  echo "error: dotnet not found." >&2
  echo "  Install: curl -fsSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 8.0" >&2
  exit 1
fi

fix_crlf() {
  local f
  for f in "$@"; do
    [[ -f "$f" ]] || continue
    sed -i 's/\r$//' "$f"
  done
}

fix_crlf "$ROOT/scripts/"*.sh
fix_crlf "$ROOT/packaging/debian/DEBIAN/postinst"
chmod +x "$ROOT/scripts/"*.sh

run_target() {
  local target="$1"
  case "$target" in
    LinuxDeb|Deb|deb)
      RID="$RID" bash "$ROOT/scripts/package-deb.sh"
      ;;
    LinuxRpm|Rpm|rpm)
      RID="$RID" bash "$ROOT/scripts/package-rpm.sh"
      ;;
    LinuxAppImage|AppImage|appimage)
      RID="$RID" bash "$ROOT/scripts/package-appimage.sh"
      ;;
    *)
      echo "error: unknown Linux package target: $target" >&2
      exit 1
      ;;
  esac
}

case "$TARGET" in
  All|Linux)
    run_target LinuxDeb
    run_target LinuxRpm
    run_target LinuxAppImage
    ;;
  LinuxDeb|Deb|deb|LinuxRpm|Rpm|rpm|LinuxAppImage|AppImage|appimage)
    run_target "$TARGET"
    ;;
  *)
    echo "error: unknown Linux target: $TARGET" >&2
    exit 1
    ;;
esac
