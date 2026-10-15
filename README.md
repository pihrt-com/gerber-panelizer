# Gerber Panelizer

**English** | [Česky](README.cs.md)

Gerber Panelizer is a lightweight Windows desktop application for preparing printed-circuit-board manufacturing panels. It imports manufacturing data exported from tools such as Autodesk Fusion, renders and filters the board layers, arranges multiple boards on a milling-machine work area, and exports one production ZIP package containing Gerber and Excellon files.

## Project status

The first working release is implemented and published locally as a self-contained Windows x64 executable. It safely processes common metric RS-274X Gerber and Excellon input without requiring a cloud service.

## Core capabilities

- Import ZIP, RAR, and individual Gerber/Excellon files.
- Interactive layer preview with visibility filtering, work-area mouse-wheel zoom, manual board dragging, continuous 45-degree trace rendering, and configurable HEX colours with a click-to-select palette.
- Configurable milling work area, margins, spacing, and placement rules.
- Top, Bottom, or both-side production selection; Bottom mirroring is an explicit workflow setting rather than an automatic and potentially incorrect transformation.
- Panel export as one production ZIP package containing per-layer Gerbers and merged Excellon drilling data.
- Incremental import, board duplication/deletion, portable saved projects, detailed status diagnostics, Czech, English, German, and French UI, panel-only print preview, and printing.
- Custom PCB application icon embedded in the executable, title bar, and taskbar, plus a localized confirmation before exit.

## Documentation

- [User guide](docs/en/USER_GUIDE.md)
- [Uživatelská příručka](docs/cs/PRIRUCKA_UZIVATELE.md)
- [Technical architecture](docs/en/ARCHITECTURE.md)
- [Technická architektura](docs/cs/ARCHITEKTURA.md)

## Build

The project requires the .NET 8 SDK on Windows.

```powershell
dotnet build .\GerberPanelizer.sln
dotnet publish .\src\GerberPanelizer.App\GerberPanelizer.App.csproj -c Release -r win-x64 --self-contained true -o .\publish\win-x64
```

After publishing, run `GerberPanelizer.App.exe` from `publish\win-x64`.

## License

See [LICENSE](LICENSE).

