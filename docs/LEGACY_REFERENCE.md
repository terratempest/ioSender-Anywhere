# Legacy WPF reference

This repository contains only the **.NET 8 / Avalonia** cross-platform application.

The original **WPF / .NET Framework 4.6.2** ioSender tree (including ioSender XL, CNC Controls modules, and related solutions) was removed from this repo as part of the Avalonia cutover. For historical comparison or unported UI behavior, use:

- Upstream: [terjeio/ioSender](https://github.com/terjeio/ioSender)
- Tag a commit before the legacy removal in your fork if you need the in-repo WPF copy (e.g. `legacy-wpf-reference`).

**Note:** Code and settings named “legacy” inside the Avalonia app (e.g. `GrblLegacy`, `IsLegacyController`) refer to **Grbl firmware protocol** compatibility, not the removed WPF UI.
