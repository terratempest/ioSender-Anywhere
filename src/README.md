# ioSender (.NET 8 / Avalonia)

Cross-platform migration tree. Legacy WPF app remains in repo root folders (`ioSender/`, `CNC Controls/`, etc.).

## Build

```bash
dotnet build src/ioSender.net.sln -c Release
dotnet run --project src/ioSender/ioSender.csproj
```

## Layout

| Project | Role |
|---------|------|
| `ioSender` | Avalonia host |
| `CNC.Platform.Abstractions` | OS-neutral interfaces |
| `CNC.Platform.Windows` | Windows implementations (builds on Windows only) |
| `CNC.Platform.Linux` | Linux implementations (builds on Linux only) |

**Large layout (formerly ioSender XL):** View → Toggle large layout (persisted in settings in a later phase).

## Linux serial

Add user to `dialout`: `sudo usermod -aG dialout $USER` then re-login.

## Linux publish/runtime notes

The default Linux package is framework-dependent:

```bash
scripts/publish-linux.sh
```

Targeting `linux-x64` selects `CNC.Platform.Linux`, defines `IOSENDER_LINUX`, and includes the official OpenCvSharp Linux x64 runtime package. The target machine needs the .NET 8 desktop/runtime stack available, `xdg-open` for external file editing, serial device permissions such as `dialout`, and a desktop session with camera/OpenCV native dependencies installed. After copying the publish folder, ensure the launcher is executable, for example `chmod +x ioSender`.

## Localization (Locale CSV)

Legacy WPF strings live under `Locale/{culture}/csv/*.csv` at the repo root. The Avalonia host loads them at runtime via `CNC.Localization` (no embedded BAML).

### Culture selection

1. Command line: `ioSender -locale de-DE` (sets `CultureInfo` and loads that folder)
2. App config: `<Locale>de-DE</Locale>` in the ioSender XML settings (same effect as `-locale`)
3. Default: `en-US` CSV catalog; thread culture stays the OS default unless (1) or (2) is set

`Locale` is copied next to the executable on build (`ioSender` project content link).

Override folder: set environment variable `IOSENDER_LOCALE_ROOT` to a directory that contains culture subfolders.

### Keys and API

CSV rows map to keys `{Assembly}.{page}.{control}` (from the locbaml-style CSV), e.g.:

- `ioSender.mainwindow.mnu_file` → File menu
- `CNC.Controls.WPF.portdialog.btn_cancel` → Port dialog Cancel

```csharp
using CNC.Localization;
var text = LocalizedStrings.Get("ioSender.mainwindow.mnu_exit", "_Exit");
```

Avalonia markup / code:

```xml
xmlns:loc="using:CNC.Localization.Avalonia"
<MenuItem Header="_File" loc:Localize.Key="ioSender.mainwindow.mnu_file"/>
```

```csharp
Localize.Set(button, "CNC.Controls.WPF.portdialog.btn_cancel", "Cancel");
Localize.Apply(button);
```

### Test a locale

```bash
dotnet build src/ioSender.net.sln -c Release
dotnet run --project src/ioSender/ioSender.csproj -- -locale de-DE
```

Check **File** menu (`_Datei`), **Connect** dialog title (`Verbindung zum Sender`), **Grbl** tab job buttons (`Start` / `Pause` in de-DE), and status row (`Status:` / `Homen`). Proof wiring: `MainWindow`, `PortDialog`, `JobControl`, `StatusControl`, `FileActionControl` (file buttons use fallback English until CSV rows exist).
