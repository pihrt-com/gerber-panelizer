# Change log

## 0.3.5 - 2026-11-11

- Render filled Gerber regions as filled polygons instead of tracing their outlines with the active aperture, preserving the separation between adjacent SMT pads in the panel preview.
- Accept ZIP archives stored inside an imported ZIP and ask the operator to assign a manufacturing layer to each Gerber file whose filename does not identify its layer.
- Accept supported production files by dropping them onto the panel work area as well as through the import-file dialog.

## 0.3.4 - 2026-11-09

- Complete the Czech, English, German, and French UI resource sets so every language exposes the same controls, labels, hints, statuses, and Wegstr terminology.
- Apply the selected language before constructing the first window and refresh runtime-computed text after a language change, including the default work-area name, imported-board details, USB connection state, prepared-operation names and summaries, surface status, and manual-motion mode.
- Add localization regression tests that require matching resource keys, reject known Czech text in foreign resources, and verify that every statically referenced UI key exists.
- Refresh the README screenshots with the version 0.3.3 interface and state the four supported interface languages.

## 0.3.3 - 2026-11-09

- Replace the imported-board checkbox with a clear switch, derive a useful board name for loose Gerber/Excellon layers instead of `Imported layers`, hide or restore the corresponding placement immediately, and visibly highlight the selected manufacturing side.
- Show the board name and every imported layer in a right-click menu over a panel placement, and clear the completed Wegstr head trail whenever a new automatic panelization is created.
- Cache parsed Gerber/Excellon drawings and the static panel scene so board dragging, zooming, and live machine-path updates do not repeatedly parse and redraw complex source geometry; keep the changing head trail as a separate compact geometry.
- Add press-and-hold keyboard jogging with arrow keys for X/Y and `+`/`-` for Z, remove the inactive centre `XY` button, and display elapsed task time beside packet progress.
- When direct Wegstr preparation reports risky or unsupported geometry, show the warnings and let the operator explicitly continue preparing the available operations or cancel; retain the raised-tool dry-run requirement before machining.

## 0.3.2 - 2026-11-03

- Add an explicit USB connection state, a one-click acknowledgment that hides the machine-safety notice persistently, and a recovery action that switches the spindle off, clears surface measurement, and checks the controller again without moving an axis.
- Log each armed surface-probe packet and its controller response, including a reported touch Z position, to the diagnostics log.
- Add a no-motion surface-probe preflight for an already closed probe, localize prepared-operation summaries, make the machine-safety notice compact, and place recovery beside STOP.
- Cancel a waiting run exchange when STOP is pressed so that a failed or interrupted operation returns control promptly after the current serial activity ends; pause also switches the spindle off and resume switches it on before machining continues.
- Keep the controller-reported absolute probe counter separate from the user work Z coordinate, start a new surface measurement after successful recovery, and localize remaining controller-link errors.
- Display a live head marker and the completed motion trail in the panel preview while measuring or milling; surface sampling remains restricted to raster cells crossed by cutting paths and excludes safe travel from 0,0 to the board.
- Exclude the generated safe-Z rapid after spindle start from the probing plan; a point now requires XY motion with the spindle on below work Z0.
- Keep the spindle off throughout surface probing, re-evaluate the Measure surface command immediately after preparing operations, and make the live-head marker plus completed path colour configurable in Settings.
- After successful probing, retain the surface map, select `01-copper-isolation.txt`, reset progress to zero, and re-enable Start so machining begins without recovery or another measurement.
- Reassert controller readiness and the Copper isolation selection after the final successful probing response, and log every Start request plus accepted program start for physical-machine diagnosis.
- Throttle the live head preview to ten updates per second, retain a compact completed-motion trail, and avoid copying the complete trail for every panel redraw so measurement remains responsive while the Panel tab is open.
- Prepare the level-compensated machining packets on a cancellable worker task, retaining a responsive UI and explicit preparation status before the first machining packet is sent.
- Keep manual positioning at the verified 1–100% range after physical hardware testing showed that queued packets above 100% can freeze the controller.
- Apply level compensation only to spindle-on XY cutting moves below work Z0 and retain byte-identical program packets for every other movement, including initial approach, Z plunge, safe travel, and return.
- Keep a completed surface map when the operator changes only the Z work origin after a tool change; changing X, Y, all axes, prepared operations, or explicitly clearing the measurement still invalidates it.
- Return safely to X/Y work zero and one probing-safe-Z clearance above the board after leveling, then precompute the compensated Copper isolation packets before enabling Start.
- Raise the head to at least 20 mm after each completed operation for tool replacement; level compensation is intentionally limited to the measured Copper isolation operation and is not required for drilling or board outline.
- Permit a selected prepared operation to start without surface measurement while retaining the no-motion controller preflight, and clear prepared Wegstr operations, surface data, and the Panel head-path history when a new project is created or another project is opened.

## 0.3.1 - 2026-10-27

- Add automatic Wegstr surface probing for the raster cells crossed by the prepared machining paths, using response `4` and its signed absolute-Z position from the controller.
- Add Wegstr-style level compensation that references the first touch point, approaches each raster-cell correction one Z step per motion sample, and includes configurable Z backlash compensation.
- Add simulator support for a deterministic uneven surface, localized measurement controls and safety prompts, persistent raster/safe-Z/backlash settings, and automatic invalidation after preparation or work-coordinate changes.
- Add regression coverage for path-scoped probing, first-point correction, and bounded gradual compensation; physical acceptance on a connected Wegstr machine and its probe remains required before production use.

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
