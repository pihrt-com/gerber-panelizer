# Uživatelská příručka Gerber Panelizer

## Účel

Gerber Panelizer spojí více výrobních zakázek DPS do jednoho panelu pro CNC frézku nebo jiný kompatibilní výrobní postup. Pracuje lokálně na počítači s Windows; zdrojové soubory se nikam neodesílají.

Výstupem je jedna výrobní ZIP sada. Obsahuje jeden Gerber soubor pro každou vybranou výrobní vrstvu a jeden Excellon soubor pro vrtání. Je to záměr: Gerber a Excellon popisují různé výrobní operace a musí zůstat samostatnými soubory.

## Založení panelu

1. Spusťte `GerberPanelizer.App.exe`.
2. V Nastavení otevřeném ikonou ozubeného kola nastavte šířku a výšku pracovní plochy frézky, například 200 × 130 mm.
3. Tlačítkem **Vložit soubory** vyberte ZIP z Fusionu/EAGLE, RAR nebo samostatné Gerber a Excellon soubory. Později jej můžete použít znovu pro přidání dalších desek.
4. Potvrďte, zda je panel určený pro Top, Bottom, nebo obě strany.
5. Zkontrolujte importované desky vedle pracovní plochy a zvolte **Panelizovat**.
6. Výsledek ověřte filtry vrstev a náhledem, potom tlačítkem **Export** vytvořte výrobní ZIP sadu.

V horní liště použijte **Nový projekt**, **Uložit projekt** a **Otevřít projekt**, chcete-li rozdělaný panel dokončit později. Projekt uloží relativní cesty ke zdrojovým archivům nebo souborům, zvolené desky i jejich ruční umístění; při přesunu na jiný počítač jej ponechte vedle zdrojových výrobních dat.

![Přehled panelizované pracovní plochy](../images/czech-panel-overview.png)

## Import výrobních dat

Export Autodesk Fusion/EAGLE obvykle obsahuje složky `GerberFiles`, `DrillFiles` a `Assembly`. Aplikace používá pro výrobu Gerber a vrtací data. Montážní BOM a pick-and-place soubory se záměrně ignorují.

Importer rozpoznává běžné názvy vrstev z Fusionu, například `copper_top`, `copper_bottom`, `profile`, `silkscreen_top`, `silkscreen_bottom`, `soldermask_top`, `soldermask_bottom`, `solderpaste_top` a `solderpaste_bottom`. Vrstva `profile` zachovává všechny kontury a výřezy; není nahrazena obdélníkem.

Pokud archiv obsahuje neznámý název vrstvy, před panelizací zkontrolujte souhrn importu. Neexportujte zakázku, dokud nejsou všechny vrstvy potřebné pro cílový stroj správně rozpoznané.

## Pracovní plocha a panelizace

Pracovní plocha představuje využitelnou oblast stroje, ne nutně fyzický rozměr podkladové desky. Nastavte bezpečný okraj vůči upínkám a limitům stroje a poté mezeru mezi deskami podle průměru frézy a způsobu oddělení.

Automatická panelizace začíná v bezpečné oblasti a umístí desky včetně úplného obrysu, vrtání a zvolených vrstev. První verze používá pouze posun a před exportem ověří, že transformovaná geometrie leží uvnitř nastavené pracovní plochy. Rotaci odmítne, místo aby potichu vytvořila nebezpečný výrobní soubor.

Po panelizaci klikněte na desku v pracovní ploše a přetáhněte ji pro jemné doladění polohy. Kolečko myši přibližuje v místě ukazatele; prostřední tlačítko nebo mezerník + tažení posouvá náhled a Home zobrazí celou pracovní plochu. V seznamu načtených desek použijte Duplikovat nebo Smazat, případně stiskněte Delete. Pokud chcete ruční umístění nahradit automatickým, spusťte Panelizovat znovu.

## Top, Bottom a zrcadlení

Gerberové vrstvy Top a Bottom obvykle používají stejný souřadnicový systém desky. Samotná volba Bottom proto nesmí data potichu zrcadlit. Vyhrazené pravidlo zrcadlení Bottom zůstává výslovným nastavením stroje, ale první verze zrcadlený export zablokuje, dokud nebudou implementované přesné zrcadlové transformace Gerberu a Excellonu.

Před výrobou neznámé zakázky vyexportujte zkušební panel a ověřte orientaci na obětované desce. Kontrolujte zejména texty, konektory a nesymetrické rozmístění otvorů.

## Náhled vrstev

Tlačítky vrstev nad pracovní plochou zobrazíte nebo skryjete vrtání, měď, profil, potisk, nepájivou masku a pájecí pastu. Barvy nastavíte v Nastavení; nemění výrobní data.

Náhled pomáhá s přípravou. Výrobní stroj je stále konečnou autoritou pro výběr nástroje, nulový bod, upnutí, vřeteno a bezpečnost materiálu.

## Nastavení a přenositelnost

Nastavení zahrnuje pracovní plochu, bezpečnostní okraje, mezery, barvy vrstev, výchozí export, jazyk a pravidlo zrcadlení Bottom. Aplikace tyto hodnoty uloží pro aktuálního uživatele Windows.

Použijte **Exportovat nastavení** pro uložení přenosné konfigurace a **Importovat nastavení** pro její obnovu na jiném počítači. Importované nastavení považujte za profil stroje: před prvním použitím vždy ověřte rozměry a pravidlo zrcadlení.

![Nastavení po uložení](../images/czech-settings-saved.png)

## Tisk

V Náhledu tisku ověřte měřítko a viditelné vrstvy, potom tiskněte přes standardní dialog Windows. Náhled i tisková úloha obsahují pouze pracovní plochu DPS, nikoli okno programu. Před tiskem vyberte Přizpůsobit stránce, skutečnou velikost, 75 % nebo 50 %, vypněte škálování tiskárny a změřte známou referenční vzdálenost. Výtisk slouží pro kontrolu nebo ruční šablonu, není to export pro stroj.

## Podrobnosti stavů

Stavový řádek zobrazuje poslední operaci. Tlačítkem Podrobnosti přímo nad ním otevřete jednotlivá varování importu nebo exportu včetně názvu zdrojového souboru a zprávy parseru či exportu. Varování před odesláním výrobních dat do stroje zkontrolujte.

![Panelizované desky a stavová informace](../images/czech-panelized-status.png)

## Řešení potíží

| Příznak | Doporučený postup |
| --- | --- |
| Chybí vrstva | Ověřte, že soubor obsahuje zdrojový archiv, a zkontrolujte jeho klasifikaci v souhrnu importu. |
| Umístění desky bylo odmítnuto | Zvětšete pracovní plochu nebo zmenšete okraje, mezery či omezení rotace. |
| Bottom strana je obrácená | Zkontrolujte výslovné pravidlo zrcadlení Bottom; jako rychlé řešení neupravujte Gerbery mimo aplikaci. |
| Stroj export odmítá | Zachovejte zdrojový archiv, ověřte názvy vrstev a vrtání očekávané strojem a proveďte malý testovací panel. |

## Bezpečnostní upozornění

Gerber Panelizer připravuje souřadnicová data, neovládá stroj. Před spuštěním CNC vždy ověřte rozměry, počátek, průměry nástrojů, materiál, upnutí a bezpečnost vřetena.
