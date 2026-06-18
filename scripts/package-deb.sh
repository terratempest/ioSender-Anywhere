#!/usr/bin/env bash
# Build iosender_<version>_<arch>.deb from a self-contained Linux publish.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="${RID:-linux-x64}"
PUBLISH_DIR="${PUBLISH_DIR:-$ROOT/artifacts/publish/$RID}"
STAGING="$ROOT/packaging/debian-staging/$RID"
ARTIFACTS="$ROOT/artifacts"
TEMPLATE_DEBIAN="$ROOT/packaging/debian"

case "$RID" in
  linux-x64)
    DEB_ARCH="amd64"
    ;;
  linux-arm64)
    DEB_ARCH="arm64"
    ;;
  *)
    echo "error: unsupported Debian RID: $RID" >&2
    exit 1
    ;;
esac

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
  if command -v git >/dev/null 2>&1 && git -C "$ROOT" describe --tags --abbrev=0 >/dev/null 2>&1; then
    VERSION="$(git -C "$ROOT" describe --tags --abbrev=0 | sed 's/^v//')"
  else
    VERSION="0.0.0"
  fi
fi

echo "Publishing fresh $RID output..."
RID="$RID" OUT_DIR="$PUBLISH_DIR" bash "$ROOT/scripts/publish-linux.sh"

if [[ ! -f "$PUBLISH_DIR/ioSender" ]]; then
  echo "error: $PUBLISH_DIR/ioSender not found after publish" >&2
  exit 1
fi

mapfile -t DEP_LINES < <(sed 's/\r$//' "$ROOT/packaging/linux-runtime-deps.txt" | grep -v '^[[:space:]]*#' | grep -v '^[[:space:]]*$')
DEPENDS="$(IFS=', '; echo "${DEP_LINES[*]}")"

rm -rf "$STAGING"
mkdir -p "$STAGING/DEBIAN"
mkdir -p "$STAGING/usr/lib/iosender"
mkdir -p "$STAGING/usr/bin"
mkdir -p "$STAGING/usr/share/applications"
mkdir -p "$STAGING/usr/share/icons/hicolor/256x256/apps"
mkdir -p "$STAGING/usr/lib/udev/rules.d"

cp -a "$PUBLISH_DIR/." "$STAGING/usr/lib/iosender/"

LAUNCHER="$TEMPLATE_DEBIAN/usr/bin/iosender"
if [[ -f "$LAUNCHER" ]]; then
  cp "$LAUNCHER" "$STAGING/usr/bin/iosender"
else
  cat > "$STAGING/usr/bin/iosender" <<'EOF'
#!/bin/sh
cd /usr/lib/iosender || exit 1
exec ./ioSender "$@"
EOF
fi
chmod 755 "$STAGING/usr/bin/iosender"
cp "$TEMPLATE_DEBIAN/usr/share/applications/iosender.desktop" "$STAGING/usr/share/applications/"
cp "$ROOT/Icon/iosendericon.png" "$STAGING/usr/share/icons/hicolor/256x256/apps/iosender.png"
cp "$TEMPLATE_DEBIAN/usr/lib/udev/rules.d/70-iosender-serial.rules" "$STAGING/usr/lib/udev/rules.d/"

sed -e "s/@VERSION@/$VERSION/g" -e "s/@ARCHITECTURE@/$DEB_ARCH/g" -e "s/@DEPENDS@/$DEPENDS/g" \
  "$TEMPLATE_DEBIAN/DEBIAN/control.in" > "$STAGING/DEBIAN/control"
cp "$TEMPLATE_DEBIAN/DEBIAN/postinst" "$STAGING/DEBIAN/postinst"
chmod 755 "$STAGING/DEBIAN/postinst"
sed -i 's/\r$//' \
  "$STAGING/DEBIAN/control" \
  "$STAGING/DEBIAN/postinst" \
  "$STAGING/usr/bin/iosender" \
  "$STAGING/usr/share/applications/iosender.desktop" \
  "$STAGING/usr/lib/udev/rules.d/70-iosender-serial.rules"

mkdir -p "$ARTIFACTS"
DEB_NAME="iosender_${VERSION}_${DEB_ARCH}.deb"
dpkg-deb --root-owner-group --build "$STAGING" "$ARTIFACTS/$DEB_NAME"
echo "Built $ARTIFACTS/$DEB_NAME"
