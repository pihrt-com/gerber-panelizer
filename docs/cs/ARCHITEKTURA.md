# Technická architektura
## Struktura řešení

`GerberPanelizer.Core` je na platformě nezávislá knihovna výrobních dat. Obsahuje klasifikaci vstupů, parser Gerberu a Excellonu, geometrii, rozložení panelu, validaci a vytváření výstupu.
`GerberPanelizer.App` je aplikace Windows Presentation Foundation. Obsahuje lokalizované uživatelské rozhraní, trvalé nastavení uživatele, dialogy souborů a tiskárny, zobrazení a vykreslovací adaptér.

## Vstupní řetězec

1. Uživatel vybere archiv nebo samostatný soubor vrstev.
2. Čtečka projde vstupy a vynechá nevýrobní data, například BOM a pick-and-place soubory.
3. Cesta, název souboru, obsah a případná metadata z Fusion/EAGLE `.gbrjob` určí vrstvu.
4. Gerber RS-274X a Excellon se převedou do normalizovaného geometrického modelu v milimetrech při zachování původního počátku a informací o zdrojovém formátu.
5. Importer vytvoří desky s hranicemi odvozenými z profilu, je-li dostupný.

Vstupní řetězec zpracuje metrická data i starší palcová data Gerber a Excellon s `M72` a příponou `.drd`. Všechny načtené souřadnice a průměry nástrojů se normalizují do milimetrů. Excellon export používá explicitní desetinné metrické souřadnice.

## Model panelu

Panel obsahuje pracovní plochu stroje, bezpečnostní okraje, pravidla rozmístění, zvolenou výrobní stranu, případné výslovné pravidlo zrcadlení spodní strany, importované desky a jejich transformace. Posun, zrcadlení X/Y kolem středu desky a čtvrtotáčková rotace se používají shodně pro všechny zvolené vrstvy, kontury profilu, vrtací body, náhled, uložené umístění i strojové dráhy dané desky. Panelizace začíná obdélníkovým skládáním, které používá hranice profilů a požadované mezery. Validátor kontroluje transformovanou geometrii vůči bezpečné pracovní ploše.

## Neměnné pravidlo exportu

Zdrojové Gerbery se nesmí pouze spojit za sebe. Různé desky mohou stejnému D-kódu přiřadit odlišnou aperturu nebo používat jiné definice maker apertur. Exportér zapíše jednu platnou hlavičku vrstvy, vytvoří globální tabulku aperture/maker, přečísluje příkazy každé desky, aplikuje transformaci a zapíše přesně jeden ukončovací příkaz souboru. Stejné pravidlo platí pro vrtání Excellon. Exportér seskupí nástroje podle geometrie, přečísluje lokální čísla nástrojů, posune každý vrtací bod a zapíše jednu Excellon hlavičku a jedno závěrečné ukončení programu. Zachová všechny kontury profilu, místo aby obrys panelu nahradil ohraničujícím obdélníkem.

Výstup je ZIP sada s manifestem, samostatnými zvolenými Gerber vrstvami a jedním vrtacím souborem. Validace probíhá před zabalením a odmítne panel s geometrií mimo plochu nebo nevyřešenými vstupními daty.

Přímé řízení Wegstr znovu používá sadu operací exportéru v paměti, překládá pouze generovanou lineární podmnožinu G-code do ověřených sedmibajtových rámců Wegstr 3.2.0 a nepodporované příkazy odmítne. Core obsahuje kodér protokolu, plánovač pohybu, překladač, plán měření pod připravenými drahami, mapu povrchu a postupnou kompenzaci Z bez závislosti na WPF. Vrstva App obsahuje sériový přenos, simulátor nerovného povrchu, sekvenci měření, potvrzování, stavový automat, bezpečnostní dialogy a ovládací zobrazení. Přenos povolí právě jeden nepotvrzený paket a před odesláním dalšího rámce načte celou tříbajtovou odpověď sondy.

## Vykreslení a tisk

WPF renderer používá normalizovaný geometrický model a vykreslí pouze viditelné vrstvy. Transformace náhledu je oddělená od výrobní transformace, proto posun, přiblížení, barva ani měřítko tisku nemohou změnit exportní souřadnice. Tisk používá stejný vykreslovací řetězec, systém tisku Windows a zobrazenou informaci o měřítku. Je to kontrolní funkce, nikoli subsystém pro řízení CNC.

## Bezpečnost a odolnost

Archivy jsou nedůvěryhodný vstup. Čtečka odmítne položky s path traversal, omezuje rozbalení archivu a velikost vstupu, nerozbaluje do libovolných složek a vrací srozumitelné chyby při poškozených výrobních souborech. Uživatelské nastavení gerber panelizeru používá místní úložiště uživatele a po importu se validuje.
