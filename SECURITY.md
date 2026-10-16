# Security and installation notice

## Current code-signing status

Gerber Panelizer release files are currently **not signed with a publicly trusted Windows code-signing certificate**. This applies to both distribution formats:

- `GerberPanelizer.App.exe` — portable, self-contained Windows x64 application.
- `GerberPanelizer-<version>-win-x64-setup.exe` — branded Windows setup executable containing the MSI.
- `GerberPanelizer-<version>-win-x64.msi` — per-machine Windows installer.

Windows SmartScreen or another Windows security control may show a warning before either file is opened. This is expected for the current unsigned releases; an MSI installer does not remove that warning.

A test or self-signed certificate would not make the application trusted on other computers. A publicly trusted certificate and a protected signing process are required; a newly signed publisher can still need time to establish SmartScreen reputation.

Do not download application files from file-sharing services, mirrors, or links in unsolicited messages. Use only the project's [GitHub Releases](https://github.com/pihrt-com/gerber-panelizer/releases) page.

## Verify a release before running it

Each release publishes a SHA-256 checksum next to its EXE and MSI assets. In PowerShell, calculate the checksum of a downloaded file and compare it with the value in that release:

```powershell
Get-FileHash -Algorithm SHA256 .\GerberPanelizer.App.exe
Get-FileHash -Algorithm SHA256 .\GerberPanelizer-<version>-win-x64.msi
```

Only continue if the checksum exactly matches the release notes for the version you selected and you trust the source repository. The project will clearly mark the releases as unsigned until a publicly trusted certificate and a protected signing process are in place.

## MSI behaviour

Use the branded Setup EXE for normal installations: it uses the PCB icon in its executable and Installed Apps record and shows the complete GPL-3.0 text from [LICENSE](LICENSE). The MSI installs Gerber Panelizer for all users in `Program Files\Martin Pihrt\Gerber Panelizer` and remains available for managed deployments. It offers Desktop and Start-menu shortcut choices; Windows controls taskbar pinning, so users can pin the Start-menu shortcut after installation. If an earlier Gerber Panelizer version is detected, the installer offers to replace it and Windows Installer removes the old version transactionally before installing the new one. It contains the same self-contained program as the portable EXE; no online installer or cloud dependency is used.

## Reporting security issues

Please do not publish a suspected security vulnerability as a public issue before it can be assessed. Contact Martin Pihrt through [www.pihrt.com](https://www.pihrt.com).
