# G-code pro Wegstr

Tento dokument popisuje přesně tu podmnožinu G-code, kterou vytváří Gerber Panelizer pro Wegstr. Nejde o úplný seznam všech příkazů, které může řídicí systém Wegstr podporovat. Výstup byl navržen podle ukázkových programů dodaných s Wegstr 3.2.0.

## Soubory a pořadí operací

Export vytvoří ZIP s následujícími textovými programy:

1. `01-copper-isolation.txt` — izolace měděných spojů a padů.
2. `02-drill.txt` — vrtání, pokud je povolené a panel obsahuje otvory.
3. `03-board-outline.txt` — vyříznutí obrysu, pokud je výslovně povolené.

Programy spouštějte v uvedeném pořadí. Vyříznutí obrysu je ve výchozím nastavení vypnuté, protože export zatím nevytváří můstky pro držení desky ani více průchodů do hloubky.

## Souřadný systém

- Jednotky jsou milimetry (`G21`).
- Souřadnice jsou absolutní (`G90`).
- Počátek X0 Y0 je v levém horním rohu pracovní plochy.
- X roste doprava a Y je směrem dolů záporné, například `X25.000Y-40.000`.
- Z0 je povrch materiálu, bezpečná výška Z je kladná a pracovní hloubka je záporná.
- Spodní strana používá zrcadlení zvolené v Nastavení; stejná transformace se použije v náhledu i exportu.

## Generované příkazy

| Příkaz | Význam ve výstupu |
| --- | --- |
| `N...` | Vzestupné číslování řádků po deseti. |
| `( ... )` | Komentář s názvem programu, rozměry materiálu nebo nástrojem. |
| `G00` | Rychloposuv v osách X, Y nebo Z. |
| `G1` | Pracovní pohyb nebo zanoření. |
| `G17` | Pracovní rovina XY. |
| `G21` | Metrické jednotky. |
| `G40` | Zrušení korekce poloměru v řídicím systému; kompenzaci obrysu počítá export. |
| `G43 ... Hn` | Zapnutí délkové korekce zvoleného nástroje. |
| `G49` | Zrušení předchozí délkové korekce při inicializaci. |
| `G71 G91.1` | Inicializační příkazy zachované podle dodaných vzorků Wegstr. |
| `G80` | Zrušení aktivního pevného cyklu. |
| `G90` | Absolutní souřadnice. |
| `G94` | Posuv v milimetrech za minutu. |
| `Tn M06` | Volba a výměna nástroje. |
| `S... M03` | Nastavení otáček a spuštění vřetena. |
| `M09` | Vypnutí chlazení před ukončením. |
| `M30` | Konec programu. |
| `%` | Poslední oddělovač programu. |

Export nevytváří palcový režim `G20`, relativní souřadnice `G91`, oblouky `G02`/`G03`, vrtací cykly `G81`/`G83` ani příkaz `M05`. Konec programu odpovídá dodaným ukázkám Wegstr: návrat do bezpečné výšky, návrat na X0 Y0, `M09`, `M30` a `%`.

## Příklad izolace mědi

```gcode
( Gerber Panelizer - copper isolation )
( for Wegstr from Gerber Panelizer )
N120G00G21G17G90G40G49G80
N130G71G91.1
N140T1M06
N150 (30 deg. spiral carbide engraving bit)
N160G00G43Z2.000H1
N170S12000M03
N180G94
N190X0.000Y0.000F170.0
N200G00X12.450Y-25.300Z2.000
N210G00Z0.000
N220G1Z-0.080F170.0
N230G1X13.100Y-25.300
N240G1X13.100Y-26.000
N250G1X12.450Y-25.300
N260G00Z2.000
```

Izolační dráha není prostým obtažením každé čáry Gerberu. Export nejprve spojí překrývající se podporované spoje a pady do jedné plochy a teprve její hranici odsadí o nastavenou vzdálenost středu nástroje.

## Příklad vrtání

```gcode
N140T1M06
N150 (drill 0.8 mm)
N160G00G43Z2.000H1
N170S12000M03
N180G94
N190G00X18.000Y-32.000Z2.000
N200G00Z0.000
N210G1Z-1.800F170.0
N220G00Z2.000
```

Každý průměr vrtáku dostane vlastní číslo nástroje. Před spuštěním zkontrolujte, že čísla a délkové korekce `H` odpovídají skutečně založeným nástrojům.

## Podporovaná vstupní geometrie

Pro izolaci mědi se převádějí přímé spoje s kruhovou aperturou a kruhové, obdélníkové nebo oválné pady. Překrývající se objekty se geometricky sjednotí. Gerber oblouky, regiony, makra a nekruhové apertury spojů se nepřevádějí bez upozornění: rizikový soubor export zastaví nebo se nepodporovaný objekt přeskočí s varováním v Podrobnostech.

Obrys musí tvořit uzavřenou dráhu z přímých segmentů. Export ji odsadí o polovinu nastaveného průměru frézy. Vrtání se načítá z Excellonu a seskupí podle průměru nástroje.

## Bezpečný první test

1. Zkontrolujte rozměr pracovní plochy, počátek X0 Y0 a orientaci os.
2. Ověřte bezpečnou výšku, hloubky, posuvy, otáčky, čísla nástrojů a délkové korekce.
3. Otevřete každý TXT program v řídicím systému a prohlédněte celou dráhu.
4. Proveďte zkušební chod se zvednutým nástrojem nad obětovaným materiálem.
5. Nejprve ověřte malý panel; obrys povolte až po ověření izolace a vrtání.

Výchozí hodnoty jsou pouze počáteční profil podle dodaných vzorků. Nejsou zárukou bezpečných parametrů pro konkrétní nástroj, materiál nebo stroj.
