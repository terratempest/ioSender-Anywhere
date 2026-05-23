# ioSender Avalonia migration status

Last updated: 2026-05-18 (dark theme phase 1)

## Build & run

```bash
dotnet build src/ioSender.net.sln -c Release
dotnet run --project src/ioSender/ioSender.csproj
```

Last integrated build: **succeeded** (wave 4: flyouts, goto, job warnings, theme, camera, l10n).

## Done (baseline)

| Area | Notes |
|------|--------|
| Platform / shell | .NET 8, Avalonia host, Windows/Linux platform libs, CI |
| Core comms | Serial, telnet, websocket, Grbl parser, connection coordinator |
| Main UI | JobView (compact + large), offsets, tools, SD card, status/DRO/jog |
| Grbl $ settings | Grid/tree, save/reload, backup; `GrblConfigApp` standalone |
| Config extras | Trinamic, PID, stepper cal, jog UI, app settings panels |
| Probing | All wizard tabs (TLO, edge/center, rotation, height map), profiles, macros |
| G-code transforms | Rotation, height map apply, drag knife menu, converters registry |
| 3D viewer | `RenderControl` path rendering wired from `JobView` on file load |
| Job streaming | `JobStreamingService` + `JobControl` cycle/hold/stop/rewind |
| Lathe | Wizards + profile dialog |
| Localization | CSV bootstrap; key menus/dialogs/job controls |
| Theming (phase 1) | Dark Fluent default; `AppTheme`; hardcoded grays → `Theme*Brush`; legacy `default`/`Standard` migrates to Dark |

## Remaining (code / polish)

| Item | Status |
|------|--------|
| Theming (phase 2) | Custom `IoSenderTheme.axaml` palette (accent, DRO contrast, sidebar) |
| Macro execute flyout | Not ported (legacy XL sidebar) |
| Connect / View menu localization | No CSV keys yet |
| Manual QA | Flyouts, Alt+R job keys, theme toggle, camera resolution |

## Human sign-off (not agent-owned)

- WPF → Avalonia cutover decision
- Hardware soak (serial, probe cycles, job stream, camera on target OS)

## Agent coordination

See `AGENT_WORKPACKAGES.md` (wave 4).
