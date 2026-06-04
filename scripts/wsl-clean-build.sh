#!/bin/bash
# Clean WSL build tree before a fresh linux publish/package.
set -eu

export DOTNET_ROOT="${DOTNET_ROOT:-$HOME/.dotnet}"
export PATH="$DOTNET_ROOT:$DOTNET_ROOT/tools:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin:$PATH"

BUILD_DIR="${IOSENDER_WSL_BUILD_DIR:-$HOME/ioSender-build}"

if [[ ! -d "$BUILD_DIR" ]]; then
  echo "error: build tree missing at $BUILD_DIR" >&2
  exit 1
fi

cd "$BUILD_DIR"

if command -v dotnet >/dev/null 2>&1; then
  dotnet clean ioSender.net.sln -c Release --nologo -v:q
fi

find . -type d \( -name bin -o -name obj \) -not -path './.git/*' -print0 2>/dev/null | xargs -0 rm -rf 2>/dev/null || true
rm -rf artifacts/publish/linux-x64 packaging/debian-staging

echo "==> WSL build tree cleaned"
