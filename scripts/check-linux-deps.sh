#!/usr/bin/env bash
# Verify published ioSender binary links against expected system libraries (run on Linux).
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLISH_DIR="${PUBLISH_DIR:-$ROOT/artifacts/publish/linux-x64}"
BINARY="${BINARY:-$PUBLISH_DIR/ioSender}"

if [[ ! -f "$BINARY" ]]; then
  echo "error: published binary not found: $BINARY" >&2
  echo "Run scripts/publish-linux.sh first." >&2
  exit 1
fi

if ! command -v ldd >/dev/null 2>&1; then
  echo "error: ldd not found (this script must run on Linux)" >&2
  exit 1
fi

echo "Checking dynamic dependencies for: $BINARY"
mapfile -t LDD_LINES < <(ldd "$BINARY")

missing=0
for line in "${LDD_LINES[@]}"; do
  if [[ "$line" == *"not found"* ]]; then
    echo "error: unresolved dependency: $line" >&2
    missing=1
  fi
done

if [[ "$missing" -ne 0 ]]; then
  exit 1
fi

# Key shared objects the Avalonia desktop stack typically needs.
declare -a REQUIRED_SOS=(
  "libc.so.6"
  "libX11.so.6"
  "libGL.so.1"
  "libEGL.so.1"
  "libfontconfig.so.1"
)

ldd_text="$(printf '%s\n' "${LDD_LINES[@]}")"
for so in "${REQUIRED_SOS[@]}"; do
  if ! grep -q "$so" <<<"$ldd_text"; then
    echo "error: expected linked library not found in ldd output: $so" >&2
    exit 1
  fi
done

DEPS_FILE="$ROOT/packaging/linux-runtime-deps.txt"
if [[ -f "$DEPS_FILE" ]]; then
  echo "Debian runtime Depends (from $DEPS_FILE):"
  grep -v '^[[:space:]]*#' "$DEPS_FILE" | grep -v '^[[:space:]]*$' | tr '\n' ', ' | sed 's/, $/\n/'
fi

echo "Linux dependency check passed."
