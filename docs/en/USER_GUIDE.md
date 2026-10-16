# Gerber Panelizer user guide

## Purpose

Gerber Panelizer combines several PCB manufacturing jobs into one panel for CNC milling or another compatible manufacturing workflow. It works locally on the Windows computer: source files are not uploaded to a server.

The export is one ZIP production package. The package contains one Gerber file for each selected manufacturing layer and one Excellon file for drilling. This is intentional: Gerber and Excellon describe different manufacturing operations and must remain separate files.

## Start a project

1. Start `GerberPanelizer.App.exe`.
2. Open Settings with the gear icon and set the milling work-area width and height, for example 200 × 130 mm.
3. Use **Import files** to choose Fusion/EAGLE ZIP files, RAR files, or individual Gerber and Excellon files. You can repeat this later to add more boards.
4. Confirm whether the panel is for Top, Bottom, or both sides.
5. Review the imported boards beside the work area, then select **Panelize**.
6. Check the result with the layer filters and preview, then select **Export** to create the production ZIP package.

Use **New project**, **Save project**, and **Open project** in the top bar to resume an unfinished panel. A project stores relative paths to its source archives or files, its chosen boards, and manual board positions; keep it beside the source manufacturing data when moving it to another computer.

![English panel overview](../images/english-panel-overview.png)

## Importing production data

Autodesk Fusion/EAGLE exports commonly contain `GerberFiles`, `DrillFiles`, and `Assembly` folders. The application uses Gerber and drill data for manufacturing. Assembly BOM and pick-and-place files are intentionally ignored.

The importer recognizes common Fusion layer names such as `copper_top`, `copper_bottom`, `profile`, `silkscreen_top`, `silkscreen_bottom`, `soldermask_top`, `soldermask_bottom`, `solderpaste_top`, and `solderpaste_bottom`. The `profile` layer preserves every contour and cutout; it is not reduced to a rectangle.

If an archive contains an unfamiliar layer name, inspect the import summary before panelizing. Do not export a job until all layers needed by the target machine are correctly classified.

## Work area and panelization

The work area represents the usable machine area, not necessarily the physical dimensions of the spoilboard. Set a safety margin to keep toolpaths away from clamps and machine limits, then set a board-to-board gap appropriate for your milling cutter and separation process.

Automatic panelization starts from the safe area and places boards with their complete outline, drills, and selected layers. The first release uses translation-only placement and validates that transformed geometry remains inside the configured work area before export. It rejects rotation rather than silently producing an unsafe manufacturing file.

After panelization, select a board in the work area and drag it to fine-tune its position. The mouse wheel zooms at the pointer; use the middle button or Space + drag to pan, and Home to fit the whole work area. Select a board in the imported-board list and use Duplicate or Delete, or press Delete. Re-run Panelize whenever automatic placement should replace manual positions.

## Top, Bottom, and mirroring

Top and Bottom Gerber layers normally share the board's coordinate system. Selecting Bottom must therefore not silently mirror data. The dedicated Bottom mirror policy is retained as an explicit machine setting, but this first release blocks a mirrored export until exact Gerber and Excellon mirror transforms are implemented.

Before producing an unfamiliar job, export a test panel and verify orientation with a sacrificial board. In particular, check text, connector footprints, and asymmetric drill patterns.

## Layer preview

Use the layer buttons above the work area to show or hide the drill, copper, profile, silkscreen, soldermask, and solderpaste layers. Colors are configurable in Settings and do not alter the manufacturing data.

The preview is a planning aid. The manufacturing machine remains the final authority for tool selection, coordinate zero, clamping, spindle operation, and material safety.

## Settings and portability

Settings include the work area, safety margins, spacing, layer colors, default export choices, language, and Bottom mirror policy. The application stores these values for the current Windows user.

Use **Export settings** to save a portable configuration file and **Import settings** to restore it on another computer. Treat imported settings as machine profiles: verify dimensions and the mirror policy before first use.

![English Settings dialog](../images/english-settings.png)

## Printing

Use Print Preview to verify scale and visible layers, then print through the standard Windows printer dialog. The preview and print job contain only the PCB work area, never the application window. Select Fit to page, actual size, 75 %, or 50 % before printing; disable printer scaling and confirm that a known reference distance measures correctly. A printout is for review or a manual template; it is not the machine export.

## Status details

The status bar reports the latest operation. Open Details directly above it to inspect individual import or export warnings, including the source filename and parser/export message. Review warnings before sending production data to a machine.

## Troubleshooting

| Symptom | Recommended action |
| --- | --- |
| A layer is missing | Check that the source archive contains the file and review its classification in the import summary. |
| Board placement is rejected | Increase the work area or reduce margins, gaps, or permitted rotation constraints. |
| Bottom side looks reversed | Check the explicit Bottom mirror policy; do not change Gerber files outside the application as a quick workaround. |
| Machine rejects the export | Keep the original source archive, inspect the layer and drill filenames expected by the machine, and validate with a small test panel. |

## Safety note

Gerber Panelizer prepares coordinate data; it does not operate the machine. Verify all dimensions, origin, tool diameters, material setup, clamps, and spindle safety before running a CNC job.
