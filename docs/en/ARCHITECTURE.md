# Technical architecture

## Solution layout

`GerberPanelizer.Core` is the platform-independent manufacturing-data library. It owns input classification, Gerber and Excellon parsing, geometry, panel placement, validation, and output generation.

`GerberPanelizer.App` is the Windows Presentation Foundation application. It owns the localized user interface, persistent user settings, file and printer dialogs, view models, and the rendering adapter. The application references Core; Core must never depend on WPF.

## Input pipeline

1. An archive or direct layer file is selected.
2. The reader enumerates candidates and ignores non-manufacturing files such as BOM and pick-and-place data.
3. File path, filename, content, and optional Fusion/EAGLE `.gbrjob` metadata classify each layer.
4. Gerber RS-274X and Excellon files are parsed into a normalized millimetre geometry model while preserving source origin and source format information.
5. The importer creates a board job with bounds derived from its profile where available.

The import pipeline handles metric data as well as legacy inch Gerber and Excellon jobs using `M72` and `.drd`. All parsed coordinates and tool diameters are normalized to millimetres. Excellon export writes explicit decimal metric coordinates so consumers such as TrackMaker cannot infer an incompatible implicit precision.

## Panel model

A panel contains a machine work area, safety margins, placement rules, selected production side, optional explicit Bottom mirroring policy, imported board jobs, and their transforms. Translation, Mirror X/Mirror Y around the board centre, and quarter-turn rotation are applied uniformly to every selected layer, profile contour, drill hit, preview primitive, project placement, and machine path belonging to a board.

Panelization starts with deterministic rectangle packing using profile-derived bounds and required gaps. Its validator checks transformed geometry against the safe work area. Manual placement and more advanced nesting can build on the same transform model.

## Export invariant

Raw Gerber source files must never be concatenated. Different board jobs may assign the same D-code to different apertures or use different aperture macro definitions. The exporter writes one legal header per layer, creates a global aperture/macro table, remaps each board's commands, applies its transform, and writes exactly one end-of-file command.

The same invariant applies to Excellon drilling. The exporter groups equal tool diameters, remaps local tool numbers, transforms each drill hit, and writes one Excellon header with explicit decimal millimetre coordinates and one final program terminator. It preserves every profile contour rather than replacing the panel outline with a bounding rectangle.

The normal result is a ZIP package with separate selected Gerber layers plus one drill file. The independent Wegstr exporter polygonizes supported copper traces and flashes, unions overlaps with Clipper2, and offsets the resulting boundary for copper isolation. Assigned Excellon hits and cutter-compensated closed profile contours are written as separate TXT programs. Machine coordinates use a top-left origin with negative Y to match the supplied Wegstr examples. Gerber arcs and regions are rejected; unsupported trace apertures are skipped with a warning.

Direct Wegstr control reuses the exporter's in-memory operation set, compiles only the generated linear G-code subset into verified seven-byte Wegstr 3.2.0 frames, and rejects unsupported commands. Core owns the protocol codec, motion planner, compiler, path-scoped probing plan, surface map, and stepwise Z compensator without referencing WPF. The App layer owns the serial transport, uneven-surface simulator, probing sequence, acknowledgements, state machine, safety prompts, and control view. The transport permits exactly one in-flight packet and consumes the complete three-byte probe response before another frame is sent.

## Rendering and printing

The WPF renderer consumes the normalized geometry model and renders only visible layers. View transforms are separate from manufacturing transforms, so pan, zoom, color, and print scaling cannot alter export coordinates.

Printing uses the same rendering pipeline, with the Windows print system and an explicit scale indicator. It is a review feature, not a CNC control subsystem.

## Version stamp

Every build emits an internal build stamp with the product version, UTC build date, and current Git revision. The app header displays this together with `Martin Pihrt | www.pihrt.com`. This makes the displayed revision traceable to the repository state used for a release.

## Security and resilience

Archives are treated as untrusted input. The reader must reject path traversal entries, limit archive expansion and input size, avoid extracting into arbitrary folders, and return actionable errors for malformed production files. Persistent settings use a user-local location and are validated after import.
