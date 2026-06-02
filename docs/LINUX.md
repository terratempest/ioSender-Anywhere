# Linux (Debian / Ubuntu amd64)

ioSender ships as a self-contained **linux-x64** build. You do not need to install the .NET runtime separately.

Supported for **Debian, Ubuntu, and derivatives on x86_64 (amd64)**. Fedora, Arch, and Alpine are not covered by the `.deb` package (AppImage/rpm may follow later). ARM builds are not provided yet.

## Install from `.deb`

Download `iosender_<version>_amd64.deb` from CI artifacts or a release, then:

```bash
sudo dpkg -i iosender_*_amd64.deb
sudo apt -f install   # install any missing dependencies
```

Launch from the application menu or run:

```bash
iosender
```

### Runtime dependencies

The package declares dependencies on common GUI/OpenGL libraries (X11, GTK, Mesa). These are installed automatically via `apt -f install` if missing.

Optional (camera preview): `libv4l-0`, `v4l-utils`.

### USB serial access

The `.deb` package installs udev rules that grant the active local desktop user access to common USB serial devices via systemd-logind. No `sudo` command should be needed for normal Debian/Ubuntu desktop installs.

If a controller was already plugged in while installing, disconnect and reconnect the USB device after installation.

## Build from source

**Prerequisites:** [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

```bash
dotnet restore ioSender.net.sln
dotnet build ioSender.net.sln -c Release
dotnet run --project ioSender/ioSender.csproj
```

Do not pass `-r` to `dotnet build` on the solution (use per-project publish instead).

## Publish and package locally

On Linux (or WSL with Linux tooling):

```bash
chmod +x scripts/*.sh
./scripts/publish-linux.sh
./scripts/check-linux-deps.sh
./scripts/package-deb.sh
```

Output:

- `artifacts/publish/linux-x64/` — self-contained publish tree
- `artifacts/iosender_<version>_amd64.deb` — installable package

Override version: `VERSION=2.0.48 ./scripts/package-deb.sh`

## Localization

Locale CSV files are installed under `/usr/lib/iosender/Locale/`. Override with `IOSENDER_LOCALE_ROOT` or `-locale de-DE` as on Windows.

## Troubleshooting

| Issue | What to try |
|-------|-------------|
| App does not start | Install deps: `sudo apt install libx11-6 libgtk-3-0 libgl1 libfontconfig1` |
| 3D view blank | Ensure Mesa/OpenGL works (`glxinfo`); viewer may degrade gracefully |
| No serial ports | Reconnect the USB device; verify it appears as `/dev/ttyUSB0`, `/dev/ttyACM0`, or `/dev/serial/by-id/*`; reinstall the `.deb` if udev rules were missing |
| Permission denied opening serial port | Reconnect the USB device or reload udev rules; `dialout` group setup is only a fallback for nonstandard, headless, or non-systemd systems |
| Camera unavailable | Install `libv4l-0`; check `/dev/video*` permissions |
