# Change log

## 2026-10-15

- Added German and French interface selections with English fallback for uncommon technical diagnostics.
- Replaced RGB sliders with a click-to-select colour palette while retaining direct HEX input.
- Added a custom PCB icon to the executable and application window.
- Corrected the WPF window and taskbar icon source so the custom PCB icon is visibly used at runtime.
- Added a localized confirmation prompt before the application exits.
- Fixed discontinuities at 45-degree trace joins and made the entire gridded work area zoom and pan with pointer-based mouse-wheel input.

## 2026-10-14

- Created the Gerber Panelizer Windows project foundation, including a .NET 8 solution, a core manufacturing-data library, and a WPF application shell.
- Defined the production rule that exports are manufacturing ZIP packages containing the required Gerber and Excellon files, not an incorrectly merged generic file.
- Started implementation from two Autodesk Fusion/EAGLE manufacturing-data samples.
- Implemented Windows WPF import, preview, automatic translation-only panelization, production ZIP export, persistent bilingual settings, printing, and a self-contained Windows x64 publish output.
- Added safe streamed RAR reading with archive-size limits, unified Gerber aperture and Excellon tool export, and automatic tests for parsing, layer classification, panelization, and aperture macros.
- Verified the complete workflow with the supplied Fusion ZIP samples: two boards were panelized on a 200 × 130 mm work area, exported as nine Gerber layers plus one Excellon drill file, and imported again without errors.
- Added incremental imports, saved portable projects, board duplicate/delete controls, keyboard deletion, zoom/pan/manual placement, a panel-only print preview, RGB color controls, clickable status diagnostics, and completed Czech/English UI localization.
