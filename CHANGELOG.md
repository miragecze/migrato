# Changelog

Formát vychází z [Keep a Changelog](https://keepachangelog.com/cs/), verzování je [SemVer](https://semver.org/lang/cs/).
Verze se mění na jediném místě: `Directory.Build.props`.

## [0.8.0] — 2026-07-10

### Přidáno
- **Pevný port 53425** pro přenos (fallback na náhodný, když je obsazený) —
  firewall se dá povolit jednou provždy; hledání běží dál na UDP 42424.
- **Volné místo nového PC viditelné předem** — ohlašuje se při hledání v síti,
  zobrazuje se u zařízení i na obrazovce výběru obsahu.
- README: srovnávací tabulka (LocalSend, Windows Přenos na nový PC, EaseUS)
  a sekce Řešení potíží (firewall, podsítě, virtuální stroje, izolace klientů).

## [0.7.1] — 2026-07-09

### Změněno
- **Nový kabát** — barvy ikony (modrofialový gradient) prostupují celou aplikací:
  tónované pozadí místo bílé, gradientový nadpis a akční tlačítka, bílé karty
  voleb a seznamů, PIN ve značkovém boxu, obarvený ukazatel průběhu.

## [0.7.0] — 2026-07-09

### Přidáno
- **Výběr podsložek** — známé složky (Dokumenty, Plocha…) se dají rozbalit a
  odškrtnout podsložky 1. úrovně, s velikostmi („Dokumenty bez Archivu“).
- **Automatická aktualizace** — tlačítko novou verzi stáhne, vymění exe a
  restartuje aplikaci; ruční stahování odpadá.
- **Druhé kolo neúspěšných souborů** — soubory zamčené při prvním průchodu se
  na konci přenosu zkusí automaticky ještě jednou.
- **Pauza přenosu** — tlačítko Pozastavit/Pokračovat; spojení zůstává otevřené.
- **Modul Vzhled** — přenese tapetu plochy a uživatelská písma; na novém PC se
  tapeta rovnou nastaví a písma zaregistrují.
- Screenshoty v README (generované nástrojem tools/Migrato.Screenshots).

## [0.6.3] — 2026-07-09

### Opraveno
- **Přidání vlastní složky nefungovalo.** Dialog byl otevřený s vícenásobným
  výběrem, který na Windows vrací prázdný výsledek — složka se pak nepřidala.
  Nově jednovýběrový dialog (víc složek = opakované kliknutí na tlačítko) a
  srozumitelná zpětná vazba, když je složka prázdná, duplicitní nebo mimo disk.

## [0.6.2] — 2026-07-09

### Změněno
- Vydání obsahují vedle `Migrato.exe` i `Migrato-win-x64.zip` — pro prohlížeče,
  které odmítají stahovat spustitelné soubory.

## [0.6.1] — 2026-07-09

### Změněno
- **Jediná UAC výzva na začátku místo výzev během instalací.** Po kliknutí na
  „Tento počítač je NOVÝ“ se aplikace (po potvrzení UAC) restartuje jako správce
  a instalace programů na konci přenosu pak běží bez dalších výzev — nepotvrzená
  UAC výzva se totiž po ~2 minutách sama zavře a instalace jí selže, což je
  problém u přenosů, u kterých nikdo nesedí. Odmítnutí UAC = chování postaru
  (výzvy u jednotlivých instalací).

## [0.6.0] — 2026-07-09

### Přidáno
- **Výběr jednotlivých programů** — položka „Nainstalované programy“ se dá rozbalit
  a u každého programu zvlášť rozhodnout, zda se má na novém PC nainstalovat.
  Textový seznam všech programů se na plochu ukládá vždy celý.

## [0.5.1] — 2026-07-09

### Opraveno
- **Instalace programů už není němá.** Výstup wingetu se průběžně propisuje do
  stavového řádku příjemce (který balíček se právě stahuje/instaluje), takže je
  vidět, že se pracuje. Hláška navíc předem upozorní na výzvy UAC — nepotvrzená
  výzva se po ~2 minutách sama zavře a instalace daného programu tím selže.

## [0.5.0] — 2026-07-09

### Přidáno
- **Vlastní složky** — tlačítko „➕ Přidat vlastní složku…“ na obrazovce výběru
  (lze vybrat i více složek najednou, odkudkoli včetně jiných disků).
  Na novém PC přistanou na ploše ve složce „Přenesené složky“.

## [0.4.3] — 2026-07-09

### Opraveno
- Přepnutí jazyka na úvodní obrazovce přeloží i úvodní obrazovku samotnou —
  dříve se přeložily až další obrazovky a úvod zůstal v původním jazyce.

## [0.4.2] — 2026-07-09

### Opraveno
- **Navazování už nevypadá jako zamrzlá aplikace.** Ověřování dříve přenesených
  dat (čtení a hashování z disku, u desítek GB i několik minut) teď obě strany
  hlásí stavem „Ověřuji dříve přenesená data…“ a průběžně obnovují ukazatel.
- Odesílatel po odeslání manifestu hlásí „Přenáším soubory…“ místo zastaralého
  „Odesílám manifest…“.

## [0.4.1] — 2026-07-09

### Opraveno
- **Mrtvé spojení už nevisí donekonečna.** TCP keepalive: když protistrana usne
  (zavřené víko notebooku) nebo zmizí ze sítě, obě strany to do ~30 sekund poznají
  a zobrazí chybu s pokynem k navázání — místo věčného „Přijímám data…“.

### Přidáno
- Upozornění na obrazovkách čekání a přenosu: u notebooků nechte otevřené víko
  (zavření víka uspí počítač i přes blokování uspání — systémové chování Windows).

## [0.4.0] — 2026-07-09

### Přidáno
- **Ikona aplikace** — v exe, na hlavním panelu i v okně.
- **Přepínač jazyka** (Česky | English) na úvodní obrazovce; volba se pamatuje.
- **Kontrola místa na disku** — příjemce ohlásí volné místo a odesílatel přenos
  srozumitelně odmítne dřív, než poteče první bajt, pokud by se data nevešla.
- **Protokol o přenosu** — tlačítko „Uložit protokol na plochu" na závěrečných
  obrazovkách (souhrn, dokončovací akce, kompletní seznam chyb).
- Odkaz **„Nahlásit problém"** (GitHub Issues) na závěrečných obrazovkách.

## [0.3.1] — 2026-07-09

### Přidáno
- Úvodní obrazovka: odkaz na autora a GitHub (miragecze/migrato).
- Tichá kontrola aktualizací — když na GitHubu existuje novější vydání,
  zobrazí se odkaz ke stažení. Bez telemetrie: jediný dotaz na veřejné API GitHubu.

## [0.3.0] — 2026-07-09

### Změněno
- **Aplikace se jmenuje Migrato** (dříve Přenos) — exe, okno, protokol (verze protokolu 2,
  starší verze se srozumitelně odmítnou).

### Přidáno
- **Čeština i angličtina** — jazyk se volí automaticky podle jazyka Windows;
  chyby mezi počítači se posílají jako kódy a každá strana si je zobrazí ve svém jazyce.
- **Ruční připojení přes IP:port** — adresa se zobrazuje na obrazovce příjmu;
  řeší sítě, kde se broadcast neprošíří (NAT, virtuální stroje, oddělené podsítě).
- Zveřejnění na GitHubu s automatickým sestavením Migrato.exe při vydání.

## [0.2.1] — 2026-07-09

### Opraveno
- Chyba zápisu jednoho souboru (zamčený soubor, nevytvořitelná složka) už neshodí
  celé spojení — soubor se přeskočí, nahlásí v souhrnu a přenos pokračuje.
- Aplikace se už nepokouší přenést sama sebe: složka, ze které Přenos běží,
  se na starém PC vynechává a na novém PC se do ní odmítá zapisovat.

## [0.2.0] — 2026-07-09

### Přidáno
- Verzování: verze v úvodní obrazovce, na obrazovce příjmu i u nalezených zařízení;
  obě strany si verzi vymění při spojení a nekompatibilní protokol se srozumitelně odmítne.
- Blokování uspání (`SetThreadExecutionState`) po dobu čekání i přenosu na obou stranách.

### Opraveno
- Navázání po přerušení: soubory přenesené celé se už neposílají znovu —
  jen se ověří kontrolním součtem SHA-256; rozpracovaný soubor pokračuje od místa přerušení.

## [0.1.0] — 2026-07-09

První verze: objevení v síti (UDP broadcast), PIN párování vázané na otisk TLS
certifikátu, šifrovaný přenos s navazováním a SHA-256 ověřením, přenos známých
složek, profilů Mozilla Thunderbird/Firefox a dalších aplikací z katalogu,
winget export/import, Wi-Fi profily, GUI v češtině (Avalonia).
