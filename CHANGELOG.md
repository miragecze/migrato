# Changelog

Formát vychází z [Keep a Changelog](https://keepachangelog.com/cs/), verzování je [SemVer](https://semver.org/lang/cs/).
Verze se mění na jediném místě: `Directory.Build.props`.

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
