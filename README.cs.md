# Gerber Panelizer

[English](README.md) | **Česky**

Gerber Panelizer je odlehčená aplikace pro Windows pro přípravu výrobních panelů desek plošných spojů. Načte výrobní data exportovaná například z Autodesk Fusionu, zobrazí jejich vrstvy, rozmístí více desek na pracovní plochu frézky a vyexportuje jednu výrobní ZIP sadu Gerber a Excellon souborů.

## Stav projektu

První funkční verze je hotová a lokálně publikovaná jako samostatné Windows x64 EXE. Bez závislosti na cloudové službě bezpečně zpracovává běžné metrické vstupy RS-274X Gerber a Excellon.

## Klíčové vlastnosti

- Import ZIP, RAR a samostatných souborů Gerber/Excellon.
- Interaktivní náhled vrstev s filtrováním, přiblížením celé pracovní plochy kolečkem myši, ručním přesouváním desek, souvislým vykreslením spojů pod 45° a nastavitelnými HEX barvami s klikací paletou.
- Nastavitelná pracovní plocha frézky, okraje, mezery a pravidla rozložení.
- Volba Top, Bottom nebo obou stran, přičemž zrcadlení Bottom je výslovná volba podle technologie výroby.
- Export panelu do jedné výrobní ZIP sady s Gerbery jednotlivých vrstev a sloučeným Excellon vrtáním.
- Postupný import, duplikování a mazání desek, přenositelné uložené projekty, podrobná diagnostika stavů, české, anglické, německé a francouzské rozhraní, náhled tisku samotného panelu a tisk.
- Vlastní ikona PCB vložená do EXE, titulku a hlavního panelu aplikace, včetně lokalizovaného potvrzení před ukončením.

## Dokumentace

- [Uživatelská příručka](docs/cs/PRIRUCKA_UZIVATELE.md)
- [User guide](docs/en/USER_GUIDE.md)
- [Technická architektura](docs/cs/ARCHITEKTURA.md)
- [Technical architecture](docs/en/ARCHITECTURE.md)

## Sestavení

Projekt vyžaduje .NET 8 SDK na Windows.

```powershell
dotnet build .\GerberPanelizer.sln
dotnet publish .\src\GerberPanelizer.App\GerberPanelizer.App.csproj -c Release -r win-x64 --self-contained true -o .\publish\win-x64
```

Po publikaci se spouští `GerberPanelizer.App.exe` ve složce `publish\win-x64`.

## Licence

Viz [LICENSE](LICENSE).
