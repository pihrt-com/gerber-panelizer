# Přímé řízení Wegstr

## Rozsah

Záložka **Přímé řízení Wegstr** připraví výrobní operace přímo z aktuálního panelu a nastavení profilu Wegstr. Používá stejnou geometrii a stejné pořadí operací jako exportní ZIP, ale G-code se nepředává přes soubor: lineární příkazy se v paměti přeloží na pakety řadiče a odešlou přes sériový port. Výchozí režim je simulátor, který neposílá nic do hardwaru.

## Co bylo ověřeno z Wegstr 3.2.0

Analyzovaným artefaktem byl původní soubor `app/wegstr.jar` s SHA-256 `26b05fc0f74429c8e5183c15325093429f668d9648940735b21501956860d0cd`. Statická analýza tříd a nezávislý běh původního kodéru potvrdily sériové nastavení 9600 baud, 8 datových bitů, bez parity, jeden stop bit a bez řízení toku, sedmibajtový paket se třemi vzorky pohybu, hlavičku `FF` pro program a `3F` pro ruční režim, maximálně 14 kroků na osu v jednom vzorku a odpovědi řadiče `1` připraveno, `2` přetížení vřetena, `3` uvolnění/nouzový stav a `4` dotyk sondy.

Referenční vstup tří vzorků `(1,-2,3,on)`, `(-14,14,-8,off)`, `(0,0,14,on)` vytvořil v původním JARu i v Gerber Panelizeru shodný paket `FF 61 92 1E 6E C0 A0`. Paket pro vypnuté vřeteno bez pohybu je `3F 00 00 00 00 00 00`. Automatické testy navíc ověřují zpětné dekódování znamének, osy Z a bitu vřetena.

Každý paket čeká na jednu odpověď; další paket se neodešle, dokud není první potvrzen. Dvou­sekundový timeout, přetížení vřetena, uvolnění stroje nebo neočekávaná odpověď zastaví danou operaci a zablokují pokračování bez nového připojení. STOP se provede až po dokončení právě probíhající výměny paketu a následně se odešle paket pro vypnutí vřetena.

## Funkce a shoda s původní aplikací

| Funkce Wegstr 3.2.0 | Přímé řízení v Gerber Panelizeru |
| --- | --- |
| Připojení k řadiči 9600 8N1 | Ano, uživatel vybere konkrétní port a připojení výslovně potvrdí. Porty se při startu automaticky nezkoušejí. |
| Offline režim | Ano, bezpečný výchozí simulátor používá časování potvrzení 59 ms odpozorované v původní aplikaci. |
| Načtení a zavření souboru G-code | Nahrazeno přímou přípravou izolace, vrtání a obrysu z aktuálního panelu; není potřeba mezilehlý soubor. |
| Start, pauza, pokračování a STOP | Ano, pauza a STOP nastanou na hranici potvrzeného paketu. |
| Výměna nástroje | Ano, mezi bloky nástrojů se úloha zastaví a vyžádá pokračování. |
| Zobrazení průběhu a aktuálního nástroje | Ano. |
| Ruční X/Y/Z při držení tlačítka | Ano, stejné zrychlování `3, 6, 9, 10, 11, 12, 13, 14` kroků a potom maximum 14 kroků na vzorek při 100 %. |
| Krokovací režim 1 % | Ano, jedno stisknutí odešle tři jednokrokové vzorky; při kalibraci 2500 kroků/10 mm je to 0,012 mm. |
| Rychlé najetí na pozici | Ano, tlačítko **Rychloposuv 100 %** nastaví pouze ruční polohování. Nemění posuv výrobní operace. |
| Pracovní souřadnice a nulování X/Y/Z | Ano, souřadnice lze upravit nebo vynulovat jednotlivě i společně bez pohybu stroje. |
| Návrat na nulu | Ano, nejprve Z do nastavené bezpečné výšky, potom X/Y na nulu a nakonec Z na nulu. |
| Zapnutí/vypnutí vřetena | Ano, samostatný paket bez pohybu. |
| Automatické měření s dotykovou sondou | Ano. Po odpovědi `4` se načte následující znaménková absolutní poloha Z v pořadí big-endian. Plán obsahuje jen buňky rastru projeté připravenými drahami se zapnutým vřetenem, první dotyk je reference a korekce se přibližuje po jednom kroku Z v každém vzorku pohybu včetně nastavitelné kompenzace vůle. |

## Automatické měření povrchu

Připravte všechny operace panelu, připojte simulátor a zvolte **Změřit povrch**. Plán zachová pořadí prvního průjezdu buňkami rastru pod izolačními, vrtacími a obrysovými drahami, takže neměří nevyužitou obdélníkovou plochu. V každém bodě se Z zvedne do nastavené bezpečné výšky měření, X/Y přejede na uloženou pozici dráhy a Z klesá do odpovědi sondy `4` nebo pevného omezení Z -10 mm. Po posledním dotyku se stroj přes bezpečné Z vrátí na pracovní nulu a kompenzace se automaticky zapne.

První změřené Z je reference. Každá další buňka uloží hodnotu `reference - změřené Z`; při obrábění se odesílaný pohyb Z přibližuje ke korekci vybrané buňky nejvýše o jeden kladný nebo záporný krok v každém vzorku. Nastavitelná vůle Z odpovídá chování původní aplikace při změně směru. Nová příprava panelu nebo úprava či vynulování pracovních souřadnic měření vymaže, protože jeho souřadnicový základ už není platný. Předvídatelný nerovný povrch simulátoru umožňuje projít celý postup bez hardwaru.

## Ruční polohování

Pro rychlé najetí nad desku připojte nejprve simulátor, ověřte směr všech os a potom zvolte **Rychloposuv 100 %**. Pohyb při 2–100 % trvá po dobu držení tlačítka; uvolnění se projeví po potvrzení právě odeslaného paketu. Hodnota 1 % je záměrně krokovací a neposílá souvislý pohyb. Toto nastavení se vztahuje pouze na ruční ovládání a návrat na nulu; řezné a vrtací posuvy zůstávají podle profilu Wegstr a připraveného programu.

## Doporučený postup

1. V záložce Panel načtěte data, zvolte pracovní plochu, umístěte celé desky a zkontrolujte vrstvy, orientaci a obrysy.
2. V Nastavení ověřte kalibraci X/Y/Z v rozsahu 2400–2600 kroků na 10 mm, bezpečné Z, hloubky, nástroje, výrobní posuvy, rastr měření 0,5–70 mm, bezpečné Z měření 0,1–40 mm a vůli Z 0–30 kroků.
3. Otevřete **Přímé řízení Wegstr**, ponechte zapnutý simulátor, zvolte **Připravit data panelu** a projděte všechny operace, počty paketů a výměny nástrojů.
4. V simulátoru ověřte krokování, souvislý pohyb, STOP, nulování, vřeteno, návrat na nulu, celé měření povrchu, počet bodů, rozsah korekcí a běh programu se zapnutou kompenzací.
5. Teprve potom vypněte simulátor, vyberte správný COM port a potvrďte připojení. Aplikace odešle identifikační paket bez pohybu a musí zobrazit stav připraveno.
6. S odpojeným vřetenem nebo vyjmutým nástrojem a osou Z bezpečně nad materiálem ověřte znaménka X/Y/Z, měřenou vzdálenost 10 mm, krok 0,012 mm při výchozí kalibraci, STOP a fyzické nouzové zastavení.
7. Nezávisle ověřte zapojení a polaritu sondy, použijte hrubý rastr nad obětovaným materiálem, ponechte přístup k nouzovému zastavení a před povolením kompenzace zkontrolujte, že každý dotyk hlásí věrohodnou absolutní polohu Z.
8. Proveďte celý zkušební chod nad obětovaným materiálem. Výrobu spusťte až po potvrzení nulového bodu, upnutí, nástroje, bezpečné výšky, případného odpojení sondy a volného prostoru vřetena.

## Rozsah ověření

Kodér paketu, načtení odpovědi sondy, plánování rastru pod dráhou, reference prvního dotyku, plynulá omezená kompenzace, ruční zrychlování, krokování, přesné součty pohybů, omezení ±14 kroků, překládání generovaného lineárního G-code, simulované potvrzování, stavové přechody a grafické ovládání byly automaticky nebo interaktivně ověřeny. Testovací prostředí nebylo připojeno k fyzickému stroji Wegstr, proto dokumentace netvrdí, že konkrétní řadič, kabeláž, směry os, sonda, kalibrace ani vřeteno prošly výrobní přejímkou.
