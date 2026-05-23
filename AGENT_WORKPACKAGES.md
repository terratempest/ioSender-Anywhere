# Agent work packages (review-first)

**Rules for all agents**
- Do **not** run `dotnet build`, `dotnet test`, or publish.
- Do **not** `git commit`, `git push`, or change CI/workflows unless asked.
- Stay inside your **owned paths** only.
- End with a short report: files changed, assumptions, what human should verify.

**Human integrates:** review diffs → resolve overlaps → single build.

---

## Current build note

`ProbingViewModel` inherits `ProbingPanelViewModel` — base class must not be `sealed` (fixed in tree if present).

---

## Package A — Probing core (owner: probing VM/program)

**Owns:** `src/CNC.Controls.Probing.Avalonia/Probing*.cs`, `ProbeMacro*.cs`, `ProbingStrings.cs`, `IProbeTab.cs`  
**Do not touch:** `Views/*` (Package B), `CNC.Localization/**`, `CNC.Controls.Config.**`

**Resume state:** TLO largely ported (`ProbingProgram`, `ProbingViewModel`, `ToolLengthControl`).  
**Remaining:** audit `ProbingViewModel` vs legacy for missing TLO paths; macro dialog stub; `VerifyProbe` / `ProbeVerifyWindow` polish.

---

## Package B — Probing UI tabs (owner: views)

**Owns:** `src/CNC.Controls.Probing.Avalonia/Views/**` only  
**Do not touch:** `ProbingViewModel.cs`, `ProbingProgram.cs`, localization, config.

**Resume state:** `ProbingView`, `ToolLengthControl`, `CsSelectControl`, `ProbeVerifyWindow` exist.  
**Next tab:** port legacy `EdgeFinderControl` → Avalonia, wire tab in `ProbingView.axaml` (stubs for other tabs).

---

## Package C — Localization (DONE — extend only)

**Owns:** `src/CNC.Localization/**`, locale keys in `src/ioSender/MainWindow.axaml`, `src/CNC.Controls.Avalonia/Views/PortDialog.axaml` (+ agreed control files)  
**Do not touch:** probing, `CNC.Controls.Config.**`

**Resume state:** CSV loader + bootstrap + menu/PortDialog proof.  
**Optional:** wire 3–5 more controls; document keys in README.

---

## Package D — Grbl config grid

**Owns:** `src/CNC.Controls.Config.Avalonia/Views/GrblConfig*.axaml*`, `IGrblConfigTab.cs`, `src/GrblConfigApp/**`  
**Do not touch:** probing, localization.

**Resume state:** `GrblConfigControl` DataGrid + details implemented.  
**Remaining:** tab activation from MainWindow; save/refresh cycle audit vs legacy.

---

## Package E — 3D viewer polish (DONE — verify only)

**Owns:** `src/CNC.GCodeViewer.Avalonia/**`  
**Resume state:** grid, envelopes, config hooks in `RenderControl`.

---

## Package F — Lathe / dragknife / converters (not started)

**Owns:** new `src/CNC.Controls.Lathe.*` depth, future converter projects.  
**Wait** until Packages A–D merged and build is green.

---

## Wave 3 (done)

Merged: gcode-transform, heightmap-preview, job-streaming core, config-jog-stepper, app-config-polish, lathe-profile. App launches after `Comms.com` startup guards.

## Wave 4 (active — polish)

| Agent | Owns | Goal |
|-------|------|------|
| shell-flyouts-goto | `ioSender/MainWindow*`, `ioSender/Views/JobView*`, `CNC.Controls.Avalonia/Views/Goto*`, `*Flyout*` | Port Goto control; 22px sidebar flyouts (jog/goto/outline/mpos) |
| job-streaming-polish | `JobStreamingService.cs`, `JobControl*`, dialog helper in `CNC.Controls.Avalonia` | Load warnings (tool change, G28/G30); `OnCycleStart` keyboard; SD host run |
| theme-camera | `AppAppearanceConfigPanel*`, `ioSender/App.axaml.cs`, `OpenCvCameraCapture.cs`, `CameraWindow*` | Apply theme without restart; `CaptureWidth/Height` on open |
| localization-sweep | `CNC.Localization/**`, agreed XAML in `CNC.Controls.Avalonia/Views/**`, `ioSender/**` | `loc:Localize.Key` on remaining high-traffic strings (no CSV reformat) |

**Human:** review diffs → `dotnet build src/ioSender.net.sln -c Release` → fix overlaps.

## Wave 2 (done)

| Agent | Owns | Legacy source |
|-------|------|----------------|
| probing-vm | `ProbingViewModel.cs`, `ProbingProgram.cs`, `ProbeMacro*.cs` | `CNC Controls Probing/**` |
| probing-ui | `Views/**` | probing tab controls |
| lathe | `CNC.Controls.Lathe.Avalonia/**` | `CNC Controls Lathe/**` |
| config-extras | Config + MainWindow Trinamic/PID | legacy config |
| modules-extra | Dragknife + Converters Avalonia | legacy modules |

## Suggested order

1. A + B (probing) — same feature, different folders  
2. D (config) — parallel if no probing edits  
3. C (localization strings) — parallel, XAML only in agreed files  
4. Human: `dotnet build src/ioSender.net.sln -c Release`
