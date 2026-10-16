# Gerber Panelizer

[![Nejnovější vydání](https://img.shields.io/github/v/release/pihrt-com/gerber-panelizer?display_name=tag&sort=semver&label=release)](https://github.com/pihrt-com/gerber-panelizer/releases/latest)
[![Windows x64](https://img.shields.io/badge/platform-Windows%2010%20%7C%2011%20x64-0078D4?logo=windows&logoColor=white)](https://github.com/pihrt-com/gerber-panelizer/releases/latest)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/cs-cz/download/dotnet/8.0)
[![Licence: GPL v3](https://img.shields.io/badge/License-GPL%20v3-blue.svg)](LICENSE)
[![Nepodepsaná sestavení](https://img.shields.io/badge/Windows%20sestaven%C3%AD-nepodepsan%C3%A9%20%2F%20SmartScreen%20upozorn%C4%9Bn%C3%AD-orange)](SECURITY.cs.md)

[English](README.md) | **Česky**

Gerber Panelizer je odlehčená aplikace pro Windows pro přípravu výrobních panelů desek plošných spojů. Načte výrobní data exportovaná například z Autodesk Fusionu, zobrazí jejich vrstvy, rozmístí více desek na pracovní plochu frézky a vyexportuje jednu výrobní ZIP sadu Gerber a Excellon souborů.

> [!WARNING]
> **Aktuální vydání nejsou podepsaná certifikátem pro podepisování kódu.** Windows SmartScreen proto může ukázat upozornění jak u přenosného EXE, tak u instalačního MSI. Stahujte pouze z [GitHub Releases tohoto repozitáře](https://github.com/pihrt-com/gerber-panelizer/releases), ověřte zveřejněný SHA-256 kontrolní součet a spusťte soubor jen tehdy, když danému vydání důvěřujete. K řádnému odstranění upozornění je nutný veřejně důvěryhodný certifikát; i nově podepsaný vydavatel může potřebovat čas na vytvoření reputace SmartScreenu. Viz [bezpečnostní a instalační informace](SECURITY.cs.md).

## Stav projektu

První funkční verze je hotová a lokálně publikovaná jako samostatné Windows x64 EXE. Bez závislosti na cloudové službě bezpečně zpracovává běžné metrické vstupy RS-274X Gerber a Excellon.

## Poslední úprava — 2026-10-16

Změny ve složce `C:\Users\marti\OneDrive\Dokumenty\GitHub\gerber-panelizer`: přidán proces sestavení x64 MSI, Shields.io badge a jasná česká/anglická dokumentace nepodepsaných sestavení, SmartScreenu a ověření SHA-256.

## Klíčové vlastnosti

- Import ZIP, RAR a samostatných souborů Gerber/Excellon.
- Interaktivní náhled vrstev s filtrováním, přiblížením celé pracovní plochy kolečkem myši, ručním přesouváním desek, souvislým vykreslením spojů pod 45°, kruhovými, obdélníkovými i oválnými SMT pady a nastavitelnými HEX barvami s klikací paletou.
- Nastavitelná pracovní plocha frézky, okraje, mezery a pravidla rozložení.
- Volba Top, Bottom nebo obou stran, přičemž zrcadlení Bottom je výslovná volba podle technologie výroby.
- Export panelu do jedné výrobní ZIP sady s Gerbery jednotlivých vrstev a sloučeným Excellon vrtáním, včetně bezpečného potvrzení před přepsáním existujícího ZIPu a okna s cílovou cestou po úspěšném exportu.
- Postupný import, duplikování a mazání desek, přenositelné uložené projekty, podrobná diagnostika stavů, české, anglické, německé a francouzské rozhraní, náhled tisku samotného panelu a tisk.
- Vlastní ikona PCB vložená do EXE, titulku a hlavního panelu aplikace, včetně lokalizovaného potvrzení před ukončením a kontroly vydání na GitHubu jedním tlačítkem vedle Nastavení.

## Dokumentace

- [Uživatelská příručka](docs/cs/PRIRUCKA_UZIVATELE.md)
- [User guide](docs/en/USER_GUIDE.md)
- [Technická architektura](docs/cs/ARCHITEKTURA.md)
- [Technical architecture](docs/en/ARCHITECTURE.md)

## Sestavení

Projekt vyžaduje .NET 8 SDK na Windows.

```powershell
dotnet build .\GerberPanelizer.sln
dotnet publish .\src\GerberPanelizer.App\GerberPanelizer.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=None -p:DebugSymbols=false -o .\publish\win-x64
.\installer\build-installer.ps1
```

Po publikaci se spouští `GerberPanelizer.App.exe` ve složce `publish\win-x64`. Instalační skript vytvoří x64 MSI v `artifacts\installer`; nejdříve publikuje stejné samostatné EXE. MSI je běžný Windows Installer pro celý počítač se zástupcem v nabídce Start a možností opravy/odinstalace; vztahuje se na něj stejné upozornění o nepodepsaném sestavení.

## Licence

Viz [LICENSE](LICENSE).
