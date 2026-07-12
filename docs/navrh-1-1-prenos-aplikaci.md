# Návrh: experimentální 1:1 přenos aplikací (fáze 3)

> Stav: **návrh k rozhodnutí**, zatím neimplementováno. Cílem je poctivě
> popsat, co je dosažitelné, kde to selže a jak to označit, aby uživatel
> nikdy nebyl uveden v omyl.

## Cíl

Umožnit přenos *jednoduchého* nainstalovaného programu ze starého PC na nový
tak, aby ho šlo na novém počítači spustit **bez reinstalace** — pro případy,
kdy program není ve wingetu (starší nástroje, firemní utility, hry z GOG apod.).

Toto je vědomě „nejkřehčí" funkce celého Migrata. Není náhradou za instalátor;
je to záchrana pro programy, které se jinak přenést nedají.

## Co se přenáší

1. **Instalační složka** — `C:\Program Files\<App>` nebo `Program Files (x86)`
   na stejné umístění na cíli. (Zápis do Program Files vyžaduje správce —
   příjemce se už dnes spouští elevovaně přes jednu UAC výzvu.)
2. **Registr programu** — jeho vlastní větve pod `HKLM\Software\<Vendor>\<App>`
   a `HKCU\Software\<Vendor>\<App>` (přes existující `RegistryModule`).
3. **Zástupce v nabídce Start** (`.lnk`) — aby program šel spustit obvyklou cestou.

## Co se NEpřenáší (a aplikace to musí říct předem)

- **Služby Windows a ovladače (.sys)** — program s nimi nenaběhne.
- **Registrace COM / rozšíření shellu** (položky v kontextovém menu) — jen zčásti.
- **Aktivace vázaná na hardware nebo online** — nutná nová aktivace.
- **Záznam v „Přidat/odebrat programy"** — program se v seznamu odinstalace
  nemusí objevit (jde jen o kosmetiku, funkci to nebrání).
- **Závislosti** (VC++ Redistributable, .NET) — předpokládá se, že na cíli jsou;
  když ne, program spadne při startu.

## Jak uživatel vybírá programy

Seznam nainstalovaných programů už čteme z registru (`InstalledApps` — klíče
`Uninstall`). Ke každému doplníme `InstallLocation` a **heuristický odhad rizika**:

| Signál (z registru + skenu složky) | Vyhodnocení |
|---|---|
| Složka je pod Program Files, žádná služba/ovladač | ✅ „vypadá jednoduše" |
| Ve složce jsou `.sys` soubory | ⚠ ovladač — nejspíš nepojede |
| Existuje služba odkazující do složky (`Services` v registru) | ⚠ služba — nejspíš nepojede |
| Vydavatel/název odpovídá známým výjimkám (Adobe, antivirus, VPN, virtualizace) | ⚠ nedoporučeno |

V UI vznikne skupina **„🧪 Programy 1:1 (experimentální)"**, rozbalitelná do
seznamu programů se zaškrtávátky a odznaky rizika. **Ve výchozím stavu vše
odškrtnuté** (opt-in) a nad seznamem výrazné upozornění, že jde o experiment.

## Napojení na stávající kód (málo nového)

- **Instalační složka** = jako vlastní složka, ale s *absolutním* cílem.
  Nová kategorie `apppayload:<ProgramFiles|ProgramFilesX86>` v `DestinationResolver`
  (kořen se na cíli rozloží přes CSIDL, ne natvrdo — kvůli x86/x64 a lokalizaci).
  Zápis je **striktně omezený** jen na kořeny Program Files.
- **Registr** = hotový `RegistryModule` (export rozšířit o čtení HKLM; import
  HKLM vyžaduje správce, kterého příjemce má).
- **Zástupce** = na cíli vygenerovat `.lnk` na přenesené exe (přes `WScript.Shell`
  / malé COM volání) jako nová post-akce `CreateShortcut`.

## Pořadí na cíli (elevovaný příjemce)

1. Fáze souborů: instalační složka → Program Files\App; `.reg` → staging.
2. Post-akce: `reg import` (HKLM + HKCU) → vytvoření zástupce.

## Bezpečnost a poctivost

- Nikdy nepřepisovat existující instalační složku bez upozornění v souhrnu.
- Kategorie `apppayload` smí zapisovat **jen** pod Program Files (obdoba ochrany
  proti „..“, kterou už má `DestinationResolver`).
- V protokolu o přenosu u každého programu výsledek: „přeneseno — spusťte a ověřte"
  vs. „přeneseno, ale obsahuje službu — nemusí fungovat".
- Titulek i popis skupiny nesou slovo **experimentální**; nic se netváří jako jisté.

## Co zůstává mimo (a proč)

- **Přenos všech uživatelských účtů** (#2) je samostatný, ještě těžší problém
  (zamčený `NTUSER.dat` přihlášeného uživatele, `reg load` cizích hive pod
  správcem, EFS, nutnost účty na cíli nejdřív vytvořit). Nepatří do fáze 3.

## Otevřené otázky k rozhodnutí

1. Vyžadovat, aby **odesílatel** taky běžel jako správce (kvůli čtení HKLM a
   některých chráněných složek Program Files)? Zvyšuje spolehlivost, přidává
   druhou UAC výzvu.
2. Nabízet i **přenos zástupců z plochy**?
3. Kolik heuristik rizika je „dost" pro první verzi — stačí detekce služeb a
   `.sys`, nebo rovnou i seznam známých problémových vydavatelů?
