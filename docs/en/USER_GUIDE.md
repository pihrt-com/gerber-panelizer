# Gerber Panelizer user guide

## Purpose

Gerber Panelizer combines several PCB manufacturing jobs into one panel for CNC milling or another compatible manufacturing workflow. It works locally on the Windows computer: source files are not uploaded to a server.

The export is one ZIP production package. The package contains one Gerber file for each selected manufacturing layer and one Excellon file for drilling. This is intentional: Gerber and Excellon describe different manufacturing operations and must remain separate files.

## Start a project

1. Start `GerberPanelizer.App.exe`.
2. Open Settings with the gear icon and set the milling work-area width and height, for example 200 × 130 mm.
3. Use **Import files** to choose Fusion/EAGLE ZIP files, RAR files, or individual Gerber and Excellon files. You can repeat this later to add more boards.
4. Confirm whether the panel is for Top, Bottom, or both sides.
5. Imported boards appear immediately in the staging area beside the work area. Drag them inside manually or select **Panelize** for automatic arrangement.
6. Check the result with the layer filters and preview, then select **Export ZIP** for Gerber/Excellon or **Export Wegstr** for the separate machine G-code package.

Use **New project**, **Save project**, and **Open project** in the top bar to resume an unfinished panel. A project stores relative paths to its source archives or files, its chosen boards, and manual board positions; keep it beside the source manufacturing data when moving it to another computer.

![English panel overview](../images/english-panel-overview.png)

## Importing production data

Autodesk Fusion/EAGLE exports commonly contain `GerberFiles`, `DrillFiles`, and `Assembly` folders. The application uses Gerber and drill data for manufacturing. Assembly BOM and pick-and-place files are intentionally ignored.

The importer recognizes common Fusion layer names such as `copper_top`, `copper_bottom`, `profile`, `silkscreen_top`, `silkscreen_bottom`, `soldermask_top`, `soldermask_bottom`, `solderpaste_top`, and `solderpaste_bottom`. The `profile` layer preserves every contour and cutout; it is not reduced to a rectangle.

If an archive contains an unfamiliar layer name, inspect the import summary before panelizing. Do not export a job until all layers needed by the target machine are correctly classified.

## Work area and panelization

The work area represents the usable machine area, not necessarily the physical dimensions of the spoilboard. Set a safety margin to keep toolpaths away from clamps and machine limits, then set a board-to-board gap appropriate for your milling cutter and separation process.

After import, every board is visible on the grey staging area to the right. Drag a board into the machine work area, reposition it inside, or move it back out. Export and printing include only boards that are completely inside; a board crossing the boundary blocks export so incomplete manufacturing data cannot be created.

Automatic panelization starts from the safe area and places boards with their complete outline, drills, and selected layers. When automatic rotation is enabled, a board can be rotated by 90 degrees to fit. Boards that do not fit remain visible in the staging area. The same rotation is applied to preview, Gerber, Excellon, saved projects, and Wegstr output.

The mouse wheel zooms at the pointer; use the middle button or Space + drag to pan, and Home to fit the work and staging areas. Select a board in the imported-board list and use Duplicate or Delete, or press Delete. Re-run Panelize whenever automatic placement should replace manual positions.

## Application updates

After the main window appears, the application automatically checks the latest GitHub Release. When a newer version is available, it offers to open the download page. An up-to-date result or a network failure stays in the status bar without interrupting startup. The manual update-check button beside Settings remains available and also displays confirmation of the current version or a connection error.

## Top, Bottom, and mirroring

Top and Bottom Gerber layers normally share the board's coordinate system. Selecting Bottom therefore mirrors data only when the dedicated policy is set to Mirror X or Mirror Y. The selected transformation is applied around each board centre consistently to the preview, Gerber coordinates, Excellon drill hits, saved project, and Wegstr G-code.

Before producing an unfamiliar job, export a test panel and verify orientation with a sacrificial board. In particular, check text, connector footprints, and asymmetric drill patterns.

## Layer preview

Use the layer buttons above the work area to show or hide the drill, copper, profile, silkscreen, soldermask, and solderpaste layers. Colors are configurable in Settings and do not alter the manufacturing data.

The preview is a planning aid. The manufacturing machine remains the final authority for tool selection, coordinate zero, clamping, spindle operation, and material safety.

## Settings and portability

Settings include the work area, safety margins, spacing, automatic rotation, layer colors, language, Bottom mirror policy, and the Wegstr machine profile. The Wegstr profile contains safe Z, material thickness, isolation, drilling, and outline depths, isolation offset, outline-cutter diameter, feeds, spindle speed, tool number, and inclusion choices for drilling and outline cutting. The application stores these values for the current Windows user.

Use **Export settings** to save a portable configuration file and **Import settings** to restore it on another computer. Treat imported settings as machine profiles: verify dimensions and the mirror policy before first use.

![English Settings dialog](../images/english-settings.png)

## Wegstr machine export

**Export Wegstr** creates a ZIP containing `01-copper-isolation.txt`, optional `02-drill.txt`, and optional `03-board-outline.txt`. Its metric command style follows the supplied Wegstr/Vectric samples: absolute coordinates, negative Y from the top-left origin, safe-Z rapid moves, a rapid to Z0 before plunging, spindle command, numbered lines, and a final return to the origin.

The isolation program unions supported circular traces and circle, rectangle, or obround flashes before offsetting the copper boundary, so overlapping pads and traces are not cut apart. The outline program compensates closed profile contours by half of the configured cutter diameter and is disabled by default because it has no holding-tab generation. Gerber arcs and regions are rejected instead of being approximated silently; unsupported trace apertures are skipped with a warning. Run isolation first, drilling second, and outline cutting last, and inspect each program in the controller before machining.

See [Wegstr G-code](WEGSTR_GCODE.md) for the exact generated command subset, sample programs, and known limitations.

## Printing

Use Print Preview to verify scale and visible layers, then print through the standard Windows printer dialog. The preview and print job contain only the PCB work area, never the application window. Select Fit to page, actual size, 75 %, or 50 % before printing; disable printer scaling and confirm that a known reference distance measures correctly. A printout is for review or a manual template; it is not the machine export.

## Status details

The status bar reports the latest operation. Open Details directly above it to inspect individual import or export warnings, including the source filename and parser/export message. Review warnings before sending production data to a machine.

Warnings, handled errors, background-task failures, and unexpected crashes are written to `gerber-panelizer.log` with stack traces. A portable copy creates it beside the launched executable. If Windows prevents writing to the installation directory, such as under `Program Files`, the application uses `%LOCALAPPDATA%\GerberPanelizer\logs`. At 2 MB, the previous contents rotate to `gerber-panelizer.previous.log`. The log can contain local file paths and exception details; review it before sharing.

## Troubleshooting

| Symptom | Recommended action |
| --- | --- |
| A layer is missing | Check that the source archive contains the file and review its classification in the import summary. |
| Board placement is rejected | Increase the work area or reduce margins, gaps, or permitted rotation constraints. |
| Bottom side looks reversed | Check the explicit Bottom mirror policy; do not change Gerber files outside the application as a quick workaround. |
| Machine rejects the export | Keep the original source archive, inspect the layer and drill filenames expected by the machine, and validate with a small test panel. |

## Safety note

Gerber Panelizer prepares coordinate data; it does not operate the machine. Verify all dimensions, origin, tool diameters, material setup, clamps, and spindle safety before running a CNC job.
