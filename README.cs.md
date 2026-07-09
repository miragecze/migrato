# Migrato

*English: [README.md](README.md)*

Otevřený nástroj pro přestěhování dat na nový počítač s Windows — přes Wi-Fi (místní síť), bez kabelů, bez cloudu a bez placení.

Spustíte aplikaci na obou počítačích, na novém se zobrazí PIN, na starém vyberete, co přenést, a je to. Aplikace mluví česky nebo anglicky podle jazyka Windows.

## Co umí přenést

- **Soubory** — Plocha, Dokumenty, Stažené soubory, Obrázky, Hudba, Videa, Oblíbené položky. Složky se hledají přes Windows API, takže funguje i přesměrování do OneDrive.
- **Mozilla Thunderbird** — kompletní profil: účty, pošta, hesla, kontakty, kalendář, filtry, rozšíření.
- **Mozilla Firefox** — kompletní profil: záložky, hesla, historie, rozšíření, otevřené panely.
- **Nastavení dalších aplikací** — Notepad++, VLC, FileZilla, OBS Studio, GIMP, LibreOffice, KeePass, VS Code, Windows Terminal, IrfanView. Chybějící aplikace se doinstalují přes winget.
- **Záložky Chrome a Edge** — výchozí profil.
- **Nainstalované programy** — na novém PC se automaticky doinstalují přes `winget import`; čitelný seznam programů se uloží na plochu.
- **Wi-Fi sítě včetně hesel** — nový počítač se připojí sám.

## Co záměrně neslibuje

Poctivost je základní vlastnost tohoto nástroje:

- **Hesla z Chrome a Edge přenést nelze.** Šifruje je Windows DPAPI klíčem vázaným na konkrétní počítač a účet. Použijte synchronizaci přes účet Google/Microsoft. (Hesla Firefoxu a Thunderbirdu se přenášejí — jejich klíč cestuje s profilem.)
- **Nainstalované programy se nepřenášejí 1:1**, ale znovu se nainstalují přes winget a přenesou se jejich data. Programy mimo katalog wingetu je třeba doinstalovat ručně (seznam najdete na ploše).
- **Licence vázané na hardware** (aktivace Office, Adobe apod.) je nutné převést u výrobce.

## Rychlé spuštění (PowerShell)

Vložte do PowerShellu na **obou** počítačích — stáhne nejnovější vydání a spustí ho:

```powershell
$dir = "$env:LOCALAPPDATA\Migrato"
New-Item $dir -ItemType Directory -Force | Out-Null
Invoke-WebRequest https://github.com/miragecze/migrato/releases/latest/download/Migrato.exe -OutFile "$dir\Migrato.exe"
Unblock-File "$dir\Migrato.exe"
& "$dir\Migrato.exe"
```

(Aplikace se schválně ukládá mimo přenášené složky — Migrato vynechává složku, ze které běží, proto ji nenechávejte na Ploše ani ve Stažených souborech.)

## Jak na to

1. Stáhněte `Migrato.exe` z [Releases](../../releases) na **oba** počítače (nebo použijte PowerShell příkaz výše).
2. Na **novém** počítači spusťte Migrato a zvolte **„Tento počítač je NOVÝ“** — zobrazí se 6místný PIN.
3. Na **starém** počítači zvolte **„Tento počítač je STARÝ“** — nový počítač se objeví v seznamu (oba musí být ve stejné síti). Když se neobjeví (NAT, virtuální stroje, oddělené podsítě), použijte **ruční připojení IP:port** — adresa je na obrazovce nového počítače.
4. Zadejte PIN, vyberte co přenést a spusťte přenos.
5. Před přenosem **zavřete přenášené aplikace** (Thunderbird, Firefox…) na obou počítačích — Migrato na běžící procesy upozorní.

Během přenosu aplikace oběma počítačům brání v uspání. Přenos lze přesto kdykoli přerušit — při dalším spuštění naváže tam, kde skončil: hotové soubory se jen ověří kontrolním součtem a přeskočí, rozpracovaný soubor pokračuje od posledního bajtu. Každý soubor se ověřuje přes SHA-256.

Existující soubory na stejném cílovém místě se přepíší — nástroj je určen pro migraci na čerstvý počítač.

> **Poznámka ke SmartScreen:** exe zatím není podepsané certifikátem, Windows proto při prvním spuštění zobrazí varování „neznámý vydavatel“. Klikněte na *Další informace → Přesto spustit*. Podpis kódu je v plánu, až projekt vyzraje.

## Zabezpečení

- Veškerý přenos jde šifrovaným TLS spojením (jednorázový certifikát pro každou relaci).
- Spárování chrání PIN: odesílatel prokazuje znalost PINu pomocí HMAC navázané na otisk certifikátu příjemce, takže PIN neprozradí a útočník uprostřed neprojde ani s odposlechem. Po 5 chybných pokusech příjemce relaci ukončí.
- Data neopouštějí místní síť. Žádný cloud, žádná telemetrie.

## Sestavení ze zdrojáků

Vyžaduje [.NET SDK 10](https://dotnet.microsoft.com/download).

```bash
dotnet test                                  # testy jádra
dotnet run --project src/Migrato.App        # spuštění pro vývoj
dotnet publish src/Migrato.App -c Release -r win-x64 --self-contained \
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o publish
```

Vývoj je možný i na macOS/Linuxu (Avalonia UI); moduly závislé na Windows (winget, netsh, registr) se jinde automaticky vypnou.

## Architektura

```
src/Migrato.Core   knihovna: discovery (UDP broadcast), TLS + PIN párování,
                   přenosový protokol s navazováním a SHA-256 ověřením,
                   migrační moduly (známé složky, profily aplikací, winget, Wi-Fi)
src/Migrato.App    GUI (Avalonia, MVVM), česky/anglicky
tests/             unit testy + integrační loopback přenos přes skutečné TCP/TLS
```

Podpora další aplikace = nový záznam v `src/Migrato.Core/Modules/app-profiles.json` (kde má aplikace data, název procesu, winget id) — žádný nový kód.

## Licence

[MIT](LICENSE)
