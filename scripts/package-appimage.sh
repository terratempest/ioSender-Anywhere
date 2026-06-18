#!/usr/bin/env bash
# Build ioSender-<version>-<rid>.AppImage from a self-contained Linux publish.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="${RID:-linux-x64}"
PUBLISH_DIR="${PUBLISH_DIR:-$ROOT/artifacts/publish/$RID}"
APPDIR="$ROOT/packaging/appimage-staging/$RID/ioSender.AppDir"
ARTIFACTS="$ROOT/artifacts"
TEMPLATE_DEBIAN="$ROOT/packaging/debian"

case "$RID" in
  linux-x64)
    APPIMAGE_ARCH="x86_64"
    ;;
  linux-arm64)
    APPIMAGE_ARCH="aarch64"
    ;;
  *)
    echo "error: unsupported AppImage RID: $RID" >&2
    exit 1
    ;;
esac

if ! command -v appimagetool >/dev/null 2>&1; then
  echo "error: appimagetool not found. Install appimagetool and ensure it is on PATH." >&2
  exit 1
fi

resolve_version() {
  if [[ -n "${VERSION:-}" ]]; then
    echo "$VERSION"
    return
  fi

  if command -v dotnet >/dev/null 2>&1; then
    dotnet msbuild "$ROOT/ioSender/ioSender.csproj" -getProperty:Version -nologo -v:q 2>/dev/null | tr -d '\r' || true
  fi
}

VERSION="$(resolve_version)"
if [[ -z "$VERSION" ]]; then
  VERSION="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT/Directory.Build.props" | head -1 | tr -d '\r' || true)"
fi
if [[ -z "$VERSION" ]]; then
  VERSION="0.0.0"
fi

echo "Publishing fresh $RID output..."
RID="$RID" OUT_DIR="$PUBLISH_DIR" bash "$ROOT/scripts/publish-linux.sh"

if [[ ! -f "$PUBLISH_DIR/ioSender" ]]; then
  echo "error: $PUBLISH_DIR/ioSender not found after publish" >&2
  exit 1
fi

rm -rf "$APPDIR"
mkdir -p "$APPDIR/usr/lib/iosender"
mkdir -p "$APPDIR/usr/share/applications"
mkdir -p "$APPDIR/usr/share/icons/hicolor/256x256/apps"

cp -a "$PUBLISH_DIR/." "$APPDIR/usr/lib/iosender/"
cp "$TEMPLATE_DEBIAN/usr/share/applications/iosender.desktop" "$APPDIR/ioSender.desktop"
cp "$TEMPLATE_DEBIAN/usr/share/applications/iosender.desktop" "$APPDIR/usr/share/applications/"
cp "$ROOT/Icon/iosendericon.png" "$APPDIR/iosender.png"
cp "$ROOT/Icon/iosendericon.png" "$APPDIR/usr/share/icons/hicolor/256x256/apps/iosender.png"
sed -i 's/\r$//' "$APPDIR/ioSender.desktop" "$APPDIR/usr/share/applications/iosender.desktop"

cat > "$APPDIR/AppRun" <<'EOF'
#!/bin/sh
HERE="$(dirname "$(readlink -f "$0")")"
cd "$HERE/usr/lib/iosender" || exit 1
exec ./ioSender "$@"
EOF
chmod 755 "$APPDIR/AppRun"
sed -i 's/\r$//' "$APPDIR/AppRun"

mkdir -p "$ARTIFACTS"
OUT="$ARTIFACTS/ioSender-$VERSION-$RID.AppImage"
ARCH="$APPIMAGE_ARCH" appimagetool "$APPDIR" "$OUT"
echo "Built $OUT"
