# Change log

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
