#!/usr/bin/env bash
# Build ioSender-<version>-<rid>.rpm from a self-contained Linux publish.
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/.." && pwd)"
RID="${RID:-linux-x64}"
PUBLISH_DIR="${PUBLISH_DIR:-$ROOT/artifacts/publish/$RID}"
STAGING="$ROOT/packaging/rpm-staging/$RID"
RPMROOT="$STAGING/rpmbuild"
PAYLOAD="$STAGING/payload"
ARTIFACTS="$ROOT/artifacts"
TEMPLATE_DEBIAN="$ROOT/packaging/debian"
INTERMEDIATE_DIR="${INTERMEDIATE_DIR:-$ROOT/artifacts/msbuild/package-rpm/$RID}"

case "$RID" in
  linux-x64)
    RPM_ARCH="x86_64"
    ;;
  linux-arm64)
    RPM_ARCH="aarch64"
    ;;
  *)
    echo "error: unsupported RPM RID: $RID" >&2
    exit 1
    ;;
esac

case "$(uname -m)" in
  x86_64|amd64)
    HOST_RPM_ARCH="x86_64"
    ;;
  aarch64|arm64)
    HOST_RPM_ARCH="aarch64"
    ;;
  *)
    HOST_RPM_ARCH="$(uname -m)"
    ;;
esac

if ! command -v rpmbuild >/dev/null 2>&1; then
  echo "error: rpmbuild not found. Install rpm-build." >&2
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

if [[ "${IOSENDER_REUSE_PUBLISH:-}" == "1" && -f "$PUBLISH_DIR/ioSender" ]]; then
  echo "Reusing existing $RID publish output at $PUBLISH_DIR"
else
  echo "Publishing fresh $RID output..."
  RID="$RID" OUT_DIR="$PUBLISH_DIR" INTERMEDIATE_DIR="$INTERMEDIATE_DIR" bash "$ROOT/scripts/publish-linux.sh"
fi

if [[ ! -f "$PUBLISH_DIR/ioSender" ]]; then
  echo "error: $PUBLISH_DIR/ioSender not found after publish" >&2
  exit 1
fi

mapfile -t DEP_LINES < <(sed 's/\r$//' "$ROOT/packaging/rpm-runtime-deps.txt" | grep -v '^[[:space:]]*#' | grep -v '^[[:space:]]*$')

rm -rf "$STAGING"
mkdir -p "$PAYLOAD/usr/lib/iosender"
mkdir -p "$PAYLOAD/usr/bin"
mkdir -p "$PAYLOAD/usr/share/applications"
mkdir -p "$PAYLOAD/usr/share/icons/hicolor/256x256/apps"
mkdir -p "$PAYLOAD/usr/lib/udev/rules.d"
mkdir -p "$RPMROOT/BUILD" "$RPMROOT/BUILDROOT" "$RPMROOT/RPMS" "$RPMROOT/SOURCES" "$RPMROOT/SPECS" "$RPMROOT/SRPMS"
mkdir -p "$RPMROOT/rpmdb"

cp -a "$PUBLISH_DIR/." "$PAYLOAD/usr/lib/iosender/"

LAUNCHER="$TEMPLATE_DEBIAN/usr/bin/iosender"
if [[ -f "$LAUNCHER" ]]; then
  cp "$LAUNCHER" "$PAYLOAD/usr/bin/iosender"
else
  cat > "$PAYLOAD/usr/bin/iosender" <<'EOF'
#!/bin/sh
cd /usr/lib/iosender || exit 1
exec ./ioSender "$@"
EOF
fi
chmod 755 "$PAYLOAD/usr/bin/iosender"
cp "$TEMPLATE_DEBIAN/usr/share/applications/iosender.desktop" "$PAYLOAD/usr/share/applications/"
cp "$ROOT/Icon/iosendericon.png" "$PAYLOAD/usr/share/icons/hicolor/256x256/apps/iosender.png"
cp "$TEMPLATE_DEBIAN/usr/lib/udev/rules.d/70-iosender-serial.rules" "$PAYLOAD/usr/lib/udev/rules.d/"
sed -i 's/\r$//' \
  "$PAYLOAD/usr/bin/iosender" \
  "$PAYLOAD/usr/share/applications/iosender.desktop" \
  "$PAYLOAD/usr/lib/udev/rules.d/70-iosender-serial.rules"

tar -C "$PAYLOAD" -czf "$RPMROOT/SOURCES/iosender-payload.tar.gz" .

SPEC="$RPMROOT/SPECS/iosender.spec"
{
  cat <<EOF
Name: iosender
Version: $VERSION
Release: 1%{?dist}
Summary: G-code sender for grblHAL and Grbl controllers
License: BSD-3-Clause
URL: https://github.com/terjeio/ioSender
BuildArch: $RPM_ARCH
Source0: iosender-payload.tar.gz
EOF

  for dep in "${DEP_LINES[@]}"; do
    echo "Requires: $dep"
  done

  cat <<'EOF'

%description
ioSender is a desktop application for sending G-code to Grbl and grblHAL
CNC controllers over serial or network connections.

%prep

%build

%install
rm -rf %{buildroot}
mkdir -p %{buildroot}
tar -xzf %{SOURCE0} -C %{buildroot}

%post
if command -v udevadm >/dev/null 2>&1; then
    udevadm control --reload-rules || true
    udevadm trigger --subsystem-match=tty || true
fi

%files
/usr/bin/iosender
/usr/lib/iosender
/usr/share/applications/iosender.desktop
/usr/share/icons/hicolor/256x256/apps/iosender.png
/usr/lib/udev/rules.d/70-iosender-serial.rules
EOF
} > "$SPEC"

RPMBUILD_ARGS=(
  --target "$RPM_ARCH-linux"
  --define "_topdir $RPMROOT"
  --define "_dbpath $RPMROOT/rpmdb"
  --define "__brp_strip %{nil}"
  --define "__brp_strip_static_archive %{nil}"
  --define "__brp_strip_comment_note %{nil}"
)

if [[ "$RPM_ARCH" != "$HOST_RPM_ARCH" ]]; then
  RPMRC="$RPMROOT/rpmrc"
  cp /usr/lib/rpm/rpmrc "$RPMRC"
  {
    echo "buildarch_compat: $HOST_RPM_ARCH: $RPM_ARCH noarch"
    echo "arch_compat: $HOST_RPM_ARCH: $RPM_ARCH noarch"
  } >> "$RPMRC"
  RPMBUILD_ARGS=(--rcfile "$RPMRC" "${RPMBUILD_ARGS[@]}")
fi

rpmbuild "${RPMBUILD_ARGS[@]}" -bb "$SPEC"

mkdir -p "$ARTIFACTS"
RPM="$(find "$RPMROOT/RPMS" -type f -name "iosender-${VERSION}-*.${RPM_ARCH}.rpm" | sort | tail -1)"
if [[ -z "$RPM" ]]; then
  echo "error: rpm not produced under $RPMROOT/RPMS" >&2
  exit 1
fi

OUT="$ARTIFACTS/ioSender-$VERSION-$RID.rpm"
cp -f "$RPM" "$OUT"
echo "Built $OUT"
