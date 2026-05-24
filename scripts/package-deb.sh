#!/usr/bin/env bash
# Build iosender_<version>_amd64.deb from a self-contained linux-x64 publish.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
PUBLISH_DIR="${PUBLISH_DIR:-$ROOT/artifacts/publish/linux-x64}"
STAGING="$ROOT/packaging/debian-staging"
ARTIFACTS="$ROOT/artifacts"
TEMPLATE_DEBIAN="$ROOT/packaging/debian"

resolve_version() {
  if [[ -n "${VERSION:-}" ]]; then
    echo "$VERSION"
    return
  fi
  dotnet msbuild "$ROOT/ioSender/ioSender.csproj" -getProperty:Version -nologo -v:q 2>/dev/null | tr -d '\r' || true
}

VERSION="$(resolve_version)"
if [[ -z "$VERSION" ]]; then
  if command -v git >/dev/null 2>&1 && git -C "$ROOT" describe --tags --abbrev=0 >/dev/null 2>&1; then
    VERSION="$(git -C "$ROOT" describe --tags --abbrev=0 | sed 's/^v//')"
  else
    VERSION="0.0.0"
  fi
fi

if [[ ! -f "$PUBLISH_DIR/ioSender" ]]; then
  echo "Publish output missing; running publish-linux.sh ..."
  OUT_DIR="$PUBLISH_DIR" bash "$ROOT/scripts/publish-linux.sh"
fi

if [[ ! -f "$PUBLISH_DIR/ioSender" ]]; then
  echo "error: $PUBLISH_DIR/ioSender not found after publish" >&2
  exit 1
fi

mapfile -t DEP_LINES < <(grep -v '^[[:space:]]*#' "$ROOT/packaging/linux-runtime-deps.txt" | grep -v '^[[:space:]]*$')
DEPENDS="$(IFS=', '; echo "${DEP_LINES[*]}")"

rm -rf "$STAGING"
mkdir -p "$STAGING/DEBIAN"
mkdir -p "$STAGING/usr/lib/iosender"
mkdir -p "$STAGING/usr/bin"
mkdir -p "$STAGING/usr/share/applications"
mkdir -p "$STAGING/usr/share/icons/hicolor/256x256/apps"

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

sed -e "s/@VERSION@/$VERSION/g" -e "s/@DEPENDS@/$DEPENDS/g" \
  "$TEMPLATE_DEBIAN/DEBIAN/control.in" > "$STAGING/DEBIAN/control"
cp "$TEMPLATE_DEBIAN/DEBIAN/postinst" "$STAGING/DEBIAN/postinst"
chmod 755 "$STAGING/DEBIAN/postinst"

mkdir -p "$ARTIFACTS"
DEB_NAME="iosender_${VERSION}_amd64.deb"
dpkg-deb --root-owner-group --build "$STAGING" "$ARTIFACTS/$DEB_NAME"
echo "Built $ARTIFACTS/$DEB_NAME"
