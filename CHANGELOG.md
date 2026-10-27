# Change log

## 0.3.0 - 2026-10-27

- Add a separate direct Wegstr control tab that compiles the current panel operations in memory and sends the verified Wegstr 3.2.0 seven-byte protocol through either the safe simulator or an explicitly confirmed 9600 8N1 serial connection.
- Add manual X/Y/Z press-and-hold movement, exact one-click stepping, a 100% positioning rapid, editable work coordinates, per-axis/all-axis zeroing, safe-Z return to zero, and spindle control without changing production feed behavior.
- Add start, pause/continue, stop-after-acknowledgement, spindle-off shutdown, tool-change pauses, packet progress, controller-status handling, calibration settings, localized safety prompts, and protocol/compiler regression tests.
- Add a clear rounded Panel/Wegstr navigation switch and use consistent rounded controls for connection, manual movement, spindle, rapid positioning, production, and STOP actions.
- Document the reverse-engineered protocol evidence, original-app parity, safety boundary, and the requirement for physical machine acceptance before production use; auto-level probing remains in the original Wegstr application until its probe behavior can be verified on hardware.

## 0.2.4 - 2026-10-23

- Fix the Setup EXE updater: it now closes the `*.partial` download file before replacing the selected destination, so both Download and Download and install work on Windows without a file-lock error.

## 0.2.3 - 2026-10-23

- Add named work-area presets with X/Y dimensions, persistent active selection, and a layer-panel drop-down that applies the selected machine area to preview, panelization, printing, and all exports.
- Replace layer-filter checkboxes with colour-coded toggle buttons that turn grey when a layer is hidden.
- Add a TrackMaker export ZIP containing flat conventional Gerber layer files and Excellon drilling data for complete-board import.

## 0.2.2 - 2026-10-22

- Prevent a second installed or portable instance for the same Windows user from loading settings, rotating logs, checking updates, or starting work; instead, it restores and focuses the existing main window.
- Offer a newer GitHub Release as either a user-selected Setup EXE download or a download that immediately launches the published Setup EXE and closes the application for upgrade.
- Add the default-enabled Wegstr option to trace each closed board profile in `01-copper-isolation.txt` at isolation depth, so a board outline can be marked before it is cut manually without generating a separate outline-cut program.

## 0.2.1 - 2026-10-17

- Display imported boards immediately in a staging area beside the machine work area and allow dragging them into, within, or back out of the panel before export.
- Keep automatic panelization available for arranging all selected boards while leaving boards that do not fit visible in the staging area.
- Limit export and print output to complete boards inside the machine work area and warn when a board crosses its boundary.
- Check GitHub Releases automatically after startup, offer the newer release when available, and retain the manual update-check button.
- Remove current and legacy desktop shortcuts during installer upgrades before creating the selected shortcut again.
- Record warnings, handled errors, unobserved background failures, and fatal exceptions with stack traces in a rotating local log beside the executable when writable, with a per-user fallback for protected installations.

## 0.2.0 - 2026-10-17

- Implemented Bottom Mirror X/Mirror Y and 90-degree board rotation consistently in preview, Gerber, Excellon, saved projects, and machine output.
- Added legacy inch Excellon `M72` and `.drd` import, inch Gerber conversion, and explicit decimal metric drill coordinates for reliable TrackMaker import.
- Added an editable Wegstr machine profile and sample-compatible TXT export with top-left origin and negative Y, united and offset copper-isolation paths, optional drilling, and optional cutter-compensated outline cutting.
- Added a bilingual Wegstr G-code reference with the generated commands, coordinate conventions, examples, input-geometry limits, and first-run safety checks.

## 0.1.9 — 2026-10-16

- Added descriptive Czech and English application screenshots to `docs/images`, the bilingual READMEs, and the matching user guides.

## 0.1.8 — 2026-10-16

- Added the branded Setup EXE bootstrapper as the primary installer, with the PCB icon in its executable and Installed Apps entry.
- Corrected GPL-3.0 RTF escaping so the installer displays paragraphs as formatted text rather than literal RTF paragraph markup.
- Removed the low-resolution stretched icon from the MSI shortcut-options page and widened its title so it cannot overlap installer text.

## 0.1.7 — 2026-10-16

- Replaced the installer’s placeholder license screen with the complete GPL-3.0 text generated directly from the repository `LICENSE` file for every MSI build.
- Added the PCB icon to MSI metadata, Installed Apps, and custom installer pages.
- Added explicit prior-version detection: the installer offers to replace an installed Gerber Panelizer version, then removes it transactionally before installing the current release.

## 0.1.6 — 2026-10-16

- Replaced the invalid executable icon with a valid multi-resolution Windows ICO, including explicit icon metadata for installer shortcuts.
- Added installer choices for Desktop and Start-menu shortcuts; Windows taskbar pinning remains an explicit user action from the Start-menu shortcut.

## 0.1.5 — 2026-10-16

- Added the reproducible Windows x64 MSI installer project and build script alongside the portable self-contained EXE.
- Added prominent English and Czech GitHub documentation of the current unsigned-build and Windows SmartScreen notice, including published-checksum guidance.
- Added release, platform, .NET, license, and unsigned-build Shields.io badges to both README variants.

## 2026-10-15

- Added German and French interface selections with English fallback for uncommon technical diagnostics.
- Replaced RGB sliders with a click-to-select colour palette while retaining direct HEX input.
- Added a custom PCB icon to the executable and application window.
- Corrected the WPF window and taskbar icon source so the custom PCB icon is visibly used at runtime.
- Added a localized confirmation prompt before the application exits.
- Fixed discontinuities at 45-degree trace joins and made the entire gridded work area zoom and pan with pointer-based mouse-wheel input.
- Fixed the Top-layer preview so rectangular and obround SMT pad flashes are rendered instead of disappearing.
- Export now asks before replacing an existing ZIP, writes safely through a temporary archive, and confirms the final export location.
- Reclassified safe aperture-macro renaming as normal export behavior instead of a solder-mask warning.
- Added a header control that checks the latest GitHub Release and offers to open its download page when a newer version is available.

## 2026-10-14

- Created the Gerber Panelizer Windows project foundation, including a .NET 8 solution, a core manufacturing-data library, and a WPF application shell.
- Defined the production rule that exports are manufacturing ZIP packages containing the required Gerber and Excellon files, not an incorrectly merged generic file.
- Started implementation from two Autodesk Fusion/EAGLE manufacturing-data samples.
- Implemented Windows WPF import, preview, automatic translation-only panelization, production ZIP export, persistent bilingual settings, printing, and a self-contained Windows x64 publish output.
- Added safe streamed RAR reading with archive-size limits, unified Gerber aperture and Excellon tool export, and automatic tests for parsing, layer classification, panelization, and aperture macros.
- Verified the complete workflow with the supplied Fusion ZIP samples: two boards were panelized on a 200 × 130 mm work area, exported as nine Gerber layers plus one Excellon drill file, and imported again without errors.
- Added incremental imports, saved portable projects, board duplicate/delete controls, keyboard deletion, zoom/pan/manual placement, a panel-only print preview, RGB color controls, clickable status diagnostics, and completed Czech/English UI localization.
