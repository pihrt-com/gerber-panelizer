# Gerber Panelizer

[![Latest release](https://img.shields.io/github/v/release/pihrt-com/gerber-panelizer?display_name=tag&sort=semver&label=release)](https://github.com/pihrt-com/gerber-panelizer/releases/latest)
[![Windows x64](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20x64-0078D4?logo=windows&logoColor=white)](https://github.com/pihrt-com/gerber-panelizer/releases/latest)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/en-us/download/dotnet/8.0)
[![License: GPL v3](https://img.shields.io/badge/License-GPL%20v3-blue.svg)](LICENSE)
[![Unsigned builds](https://img.shields.io/badge/Windows%20builds-unsigned%20%2F%20SmartScreen%20notice-orange)](SECURITY.md)

**English** | [Česky](README.cs.md)

Gerber Panelizer is a lightweight Windows desktop application for preparing printed-circuit-board manufacturing panels. It imports manufacturing data exported from tools such as Autodesk Fusion, renders and filters the board layers, arranges multiple boards on a milling-machine work area, and exports one production ZIP package containing Gerber and Excellon files.

> [!WARNING]
> **Current releases are not code-signed.** Windows SmartScreen may therefore display a warning for both the portable EXE and the MSI installer. Download only from this repository's [GitHub Releases](https://github.com/pihrt-com/gerber-panelizer/releases), verify the published SHA-256 checksum, and run it only if you trust that release. A publicly trusted code-signing certificate is required to resolve this properly; even a newly signed publisher can need time to establish SmartScreen reputation. See the [security and installation notice](SECURITY.md).

## Project status

The first working release is implemented and published locally as a self-contained Windows x64 executable. It safely processes common metric RS-274X Gerber and Excellon input without requiring a cloud service.

## Latest update — 2026-10-16

Changes in `C:\Users\marti\OneDrive\Dokumenty\GitHub\gerber-panelizer`: replaced the MSI license placeholder with the complete GPL-3.0 text from `LICENSE`, added the PCB icon to MSI metadata and installer pages, and made the installer explicitly offer transactional replacement of an already installed version.

## Core capabilities

- Import ZIP, RAR, and individual Gerber/Excellon files.
- Interactive layer preview with visibility filtering, work-area mouse-wheel zoom, manual board dragging, continuous 45-degree trace rendering, circular, rectangular, and obround SMT pad flashes, and configurable HEX colours with a click-to-select palette.
- Configurable milling work area, margins, spacing, and placement rules.
- Top, Bottom, or both-side production selection; Bottom mirroring is an explicit workflow setting rather than an automatic and potentially incorrect transformation.
- Panel export as one production ZIP package containing per-layer Gerbers and merged Excellon drilling data, with safe confirmation before replacing an existing ZIP and a success dialog showing its final location.
- Incremental import, board duplication/deletion, portable saved projects, detailed status diagnostics, Czech, English, German, and French UI, panel-only print preview, and printing.
- Custom PCB application icon embedded in the executable, title bar, and taskbar, a localized confirmation before exit, and a one-click GitHub Release update check beside Settings.

## Documentation

- [User guide](docs/en/USER_GUIDE.md)
- [Uživatelská příručka](docs/cs/PRIRUCKA_UZIVATELE.md)
- [Technical architecture](docs/en/ARCHITECTURE.md)
- [Technická architektura](docs/cs/ARCHITEKTURA.md)

## Build

The project requires the .NET 8 SDK on Windows.

```powershell
dotnet build .\GerberPanelizer.sln
dotnet publish .\src\GerberPanelizer.App\GerberPanelizer.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o .\publish\win-x64
.\installer\build-installer.ps1
```

After publishing, run `GerberPanelizer.App.exe` from `publish\win-x64`. The installer script produces an x64 MSI in `artifacts\installer`; it first publishes the same self-contained EXE. The MSI shows the complete GPL-3.0 license from [LICENSE](LICENSE), exposes the PCB icon in Installed Apps and installer pages, offers Desktop/Start-menu shortcut choices, and offers transactional replacement of a previous Gerber Panelizer version.

## License

See [LICENSE](LICENSE).

