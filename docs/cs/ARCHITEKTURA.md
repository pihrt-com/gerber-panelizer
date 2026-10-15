# Technická architektura

## Struktura řešení

`GerberPanelizer.Core` je na platformě nezávislá knihovna výrobních dat. Obsahuje klasifikaci vstupů, parser Gerberu a Excellonu, geometrii, rozložení panelu, validaci a vytváření výstupu.

`GerberPanelizer.App` je aplikace Windows Presentation Foundation. Obsahuje lokalizované uživatelské rozhraní, trvalé nastavení uživatele, dialogy souborů a tiskárny, view modely a vykreslovací adaptér. Aplikace odkazuje na Core; Core nikdy nesmí záviset na WPF.

## Vstupní řetězec

1. Uživatel vybere archiv nebo samostatný soubor vrstvy.
2. Čtečka projde kandidáty a vynechá nevýrobní data, například BOM a pick-and-place soubory.
3. Cesta, název souboru, obsah a případná metadata Fusion/EAGLE `.gbrjob` určí vrstvu.
4. Gerber RS-274X a Excellon se převedou do normalizovaného geometrického modelu v milimetrech při zachování původního počátku a informací o zdrojovém formátu.
5. Importer vytvoří zakázku desky s hranicemi odvozenými z profilu, je-li dostupný.

Oba dodané vzorky z Fusionu používají metrický RS-274X s `FSLAX34Y34` a metrický Excellon s potlačenými koncovými nulami a rozlišením 0,001 mm. Implementace nesmí předpokládat stejný formát u všech budoucích vstupů.

## Model panelu

Panel obsahuje pracovní plochu stroje, bezpečnostní okraje, pravidla rozmístění, zvolenou výrobní stranu, případné výslovné pravidlo zrcadlení Bottom, importované zakázky desek a jejich transformace. Transformace se použije stejně na všechny zvolené vrstvy, kontury profilu i vrtací body dané desky. První verze exportuje pouze posun a rotaci nebo zrcadlení Bottom odmítne, dokud pro ně nejsou implementované přesné výrobně bezpečné transformace.

Panelizace začíná deterministickým obdélníkovým skládáním, které používá hranice profilů a požadované mezery. Validátor kontroluje transformovanou geometrii vůči bezpečné pracovní ploše. Ruční umístění a pokročilejší nesting mohou stavět na stejném modelu transformací.

## Neměnné pravidlo exportu

Zdrojové Gerbery se nesmí pouze spojit za sebe. Různé zakázky mohou stejnému D-kódu přiřadit odlišnou aperturu nebo používat jiné definice maker apertur. Exportér zapíše jednu platnou hlavičku vrstvy, vytvoří globální tabulku aperture/maker, přečísluje příkazy každé desky, aplikuje transformaci a zapíše přesně jeden ukončovací příkaz souboru.

Stejné pravidlo platí pro vrtání Excellon. Exportér seskupí nástroje podle geometrie, přečísluje lokální čísla nástrojů, posune každý vrtací bod a zapíše jednu Excellon hlavičku a jedno závěrečné ukončení programu. Zachová všechny kontury profilu, místo aby obrys panelu nahradil ohraničujícím obdélníkem.

Výstup je ZIP sada s manifestem, samostatnými zvolenými Gerber vrstvami a jedním vrtacím souborem. Validace probíhá před zabalením a odmítne panel s geometrií mimo plochu nebo nevyřešenými vstupními daty.

## Vykreslení a tisk

WPF renderer používá normalizovaný geometrický model a vykreslí pouze viditelné vrstvy. Transformace náhledu je oddělená od výrobní transformace, proto posun, přiblížení, barva ani měřítko tisku nemohou změnit exportní souřadnice.

Tisk používá stejný vykreslovací řetězec, systém tisku Windows a zobrazenou informaci o měřítku. Je to kontrolní funkce, nikoli subsystém pro řízení CNC.

## Označení verze

Každé sestavení vytvoří interní označení s verzí produktu, UTC datem sestavení a aktuální Git revizí. Hlavička aplikace jej zobrazí společně s textem `Martin Pihrt | www.pihrt.com`. Zobrazenou revizi tak lze dohledat ke stavu repozitáře použitému pro vydání.

## Bezpečnost a odolnost

Archivy jsou nedůvěryhodný vstup. Čtečka musí odmítnout položky s path traversal, omezit rozbalení archivu a velikost vstupu, nerozbalovat do libovolných složek a vracet srozumitelné chyby při poškozených výrobních souborech. Trvalé nastavení používá lokální úložiště uživatele a po importu se validuje.
