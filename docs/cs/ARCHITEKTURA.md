# Technická architektura

## Změny modelu ve verzi 0.2.0

Transformace umístění nyní zahrnuje posun, zrcadlení X/Y kolem středu desky a rotaci po 90 stupních. Jediný transformační model používá náhled, Gerber, Excellon, uložený projekt a Wegstr G-code. Import Excellonu rozpoznává jednotky `M71`/`M72` i příponu `.drd`; výstupní vrtání používá explicitní desetinné milimetry, aby TrackMaker nemusel odhadovat implicitní přesnost.

Samostatný exportér Wegstr převádí podporované spoje a pady měděné vrstvy na polygony, sjednotí jejich překryvy pomocí Clipper2 a vypočte izolační kontury s nastaveným odsazením. Vrtání Excellon a uzavřené profilové kontury exportuje do samostatných TXT programů; profilové kontury kompenzuje o polovinu průměru frézy. Souřadnice stroje používají levý horní počátek a zápornou osu Y podle dodaných vzorků Wegstr. Gerber oblouky a regiony se odmítnou a nepodporované apertury spojů se přeskočí s varováním.

## Struktura řešení

`GerberPanelizer.Core` je na platformě nezávislá knihovna výrobních dat. Obsahuje klasifikaci vstupů, parser Gerberu a Excellonu, geometrii, rozložení panelu, validaci a vytváření výstupu.

`GerberPanelizer.App` je aplikace Windows Presentation Foundation. Obsahuje lokalizované uživatelské rozhraní, trvalé nastavení uživatele, dialogy souborů a tiskárny, view modely a vykreslovací adaptér. Aplikace odkazuje na Core; Core nikdy nesmí záviset na WPF.

## Vstupní řetězec

1. Uživatel vybere archiv nebo samostatný soubor vrstvy.
2. Čtečka projde kandidáty a vynechá nevýrobní data, například BOM a pick-and-place soubory.
3. Cesta, název souboru, obsah a případná metadata Fusion/EAGLE `.gbrjob` určí vrstvu.
4. Gerber RS-274X a Excellon se převedou do normalizovaného geometrického modelu v milimetrech při zachování původního počátku a informací o zdrojovém formátu.
5. Importer vytvoří zakázku desky s hranicemi odvozenými z profilu, je-li dostupný.

Vstupní řetězec zpracuje metrická data i starší palcové zakázky Gerber a Excellon s `M72` a příponou `.drd`. Všechny načtené souřadnice a průměry nástrojů se normalizují do milimetrů. Excellon export používá explicitní desetinné metrické souřadnice, takže TrackMaker nemusí odhadovat nekompatibilní implicitní přesnost.

## Model panelu

Panel obsahuje pracovní plochu stroje, bezpečnostní okraje, pravidla rozmístění, zvolenou výrobní stranu, případné výslovné pravidlo zrcadlení Bottom, importované zakázky desek a jejich transformace. Posun, zrcadlení X/Y kolem středu desky a čtvrtotáčková rotace se používají shodně pro všechny zvolené vrstvy, kontury profilu, vrtací body, náhled, uložené umístění i strojové dráhy dané desky.

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
