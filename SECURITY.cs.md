# Bezpečnostní a instalační informace

## Aktuální stav podepsání

Soubory vydání Gerber Panelizeru zatím **nejsou podepsané veřejně důvěryhodným certifikátem pro podepisování kódu pro Windows**. Týká se to obou distribučních variant:

- `GerberPanelizer.App.exe` — přenosná samostatná aplikace pro Windows x64.
- `GerberPanelizer-<verze>-win-x64-setup.exe` — značkový instalační EXE obsahující MSI.
- `GerberPanelizer-<verze>-win-x64.msi` — instalační balíček pro celý počítač.

Windows SmartScreen nebo jiná ochrana Windows může před otevřením kteréhokoli z těchto souborů zobrazit upozornění. U současných nepodepsaných vydání je to očekávané; samotný instalátor MSI upozornění neodstraní.

Testovací nebo samopodepsaný certifikát by aplikaci na cizích počítačích nedokázal označit jako důvěryhodnou. Je nutný veřejně důvěryhodný certifikát a chráněný proces podepisování; i nově podepsaný vydavatel může potřebovat čas na vytvoření reputace SmartScreenu.

Nestahujte aplikaci z úložišť souborů, zrcadel ani z odkazů v nevyžádaných zprávách. Používejte pouze stránku [GitHub Releases projektu](https://github.com/pihrt-com/gerber-panelizer/releases).

## Ověření vydání před spuštěním

Každé vydání zveřejní SHA-256 kontrolní součet u EXE i MSI. V PowerShellu vypočtěte kontrolní součet staženého souboru a porovnejte jej s hodnotou u konkrétního vydání:

```powershell
Get-FileHash -Algorithm SHA256 .\GerberPanelizer.App.exe
Get-FileHash -Algorithm SHA256 .\GerberPanelizer-<verze>-win-x64.msi
```

Pokračujte jen tehdy, když kontrolní součet přesně souhlasí s poznámkami daného vydání a zdrojovému repozitáři důvěřujete. Dokud nebude zaveden veřejně důvěryhodný certifikát a chráněný proces podepisování, projekt bude vydání jasně označovat jako nepodepsaná.

## Chování MSI

Pro běžnou instalaci použijte značkový Setup EXE: používá ikonu PCB v samotném souboru i záznamu Nainstalovaných aplikací a zobrazí úplné znění GPL-3.0 ze souboru [LICENSE](LICENSE). MSI nainstaluje Gerber Panelizer pro všechny uživatele do `Program Files\Martin Pihrt\Gerber Panelizer` a zůstává k dispozici pro spravované nasazení. Nabídne volbu zástupce na ploše a v nabídce Start; připnutí na hlavní panel řídí Windows, proto lze zástupce z nabídky Start připnout až po instalaci. Pokud nalezne starší verzi Gerber Panelizeru, nabídne její nahrazení a Windows Installer ji před instalací nové verze transakčně odebere. Obsahuje tutéž samostatnou aplikaci jako přenosné EXE; nepoužívá online instalátor ani cloudovou závislost.

## Nahlášení bezpečnostní chyby

Podezření na bezpečnostní chybu nezveřejňujte nejdříve jako veřejný issue, aby jej bylo možné posoudit. Kontaktujte Martina Pihrta přes [www.pihrt.com](https://www.pihrt.com).
