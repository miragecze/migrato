using static Migrato.Core.S;

namespace Migrato.App.ViewModels;

/// <summary>Texty XAML pohledů — jazyk se volí podle jazyka Windows (viz Migrato.Core.Lang).</summary>
public sealed class UI
{
    // Úvod / Home
    public string HomeSubtitle => T(
        "Přestěhujte soubory, poštu, nastavení i programy na nový počítač přes Wi-Fi.",
        "Move your files, mail, settings and programs to a new computer over Wi-Fi.");
    public string HomeOldTitle => T("📤  Tento počítač je STARÝ", "📤  This is the OLD computer");
    public string HomeOldSub => T("Vybrat, co odeslat do nového počítače", "Choose what to send to the new computer");
    public string HomeNewTitle => T("📥  Tento počítač je NOVÝ", "📥  This is the NEW computer");
    public string HomeNewSub => T("Přijmout data ze starého počítače", "Receive data from the old computer");
    public string HomeFooter => T(
        "Spusťte aplikaci na obou počítačích připojených ke stejné síti.",
        "Run the app on both computers connected to the same network.");

    public string MadeBy => T(
        "vytvořil miragecze · zdrojáky na GitHubu", "made by miragecze · source on GitHub");
    public string SaveReport => T("💾 Uložit protokol na plochu", "💾 Save report to desktop");
    public string ReportIssue => T("Nahlásit problém", "Report an issue");
    public static Uri IssuesUri { get; } = new("https://github.com/miragecze/migrato/issues");

    // Příjem / Receive
    public string RcvTitle => T("📥  Tento počítač přijme data", "📥  This computer will receive data");
    public string RcvPinLabel => T("PIN pro spárování", "Pairing PIN");
    public string RcvInstruction => T(
        "Na starém počítači zvolte „Tento počítač je STARÝ“, vyberte tento počítač a zadejte PIN.",
        "On the old computer choose “This is the OLD computer”, select this computer and enter the PIN.");
    public string RcvReceiving => T("Přijímám data…", "Receiving data…");
    public string RcvDone => T("✅  Hotovo", "✅  Done");

    public string KeepLidOpen => T(
        "💡 U notebooků nechte během přenosu otevřené víko — jeho zavření počítač uspí a přenos přeruší.",
        "💡 On laptops, keep the lid open during the transfer — closing it puts the computer to sleep and interrupts the transfer.");

    // Odeslání / Send
    public string SndPickTitle => T("Vyberte nový počítač", "Select the new computer");
    public string SndPickHint => T(
        "Na novém počítači spusťte Migrato a zvolte „Tento počítač je NOVÝ“. Za chvíli se tu objeví.",
        "Run Migrato on the new computer and choose “This is the NEW computer”. It will appear here shortly.");
    public string SndManualHint => T(
        "Nevidíte ho? Připojte se ručně adresou z obrazovky nového počítače:",
        "Can't see it? Connect manually using the address shown on the new computer:");
    public string SndManualButton => T("Připojit ručně", "Connect manually");
    public string Back => T("Zpět", "Back");
    public string Continue => T("Pokračovat", "Continue");
    public string SndPinTitle => T("Zadejte PIN", "Enter the PIN");
    public string SndPinHint => T(
        "Šest číslic zobrazených na novém počítači", "The six digits shown on the new computer");
    public string SndScanTitle => T("Zjišťuji, co lze přenést…", "Finding what can be transferred…");
    public string SndSelectTitle => T("Co se má přenést?", "What should be transferred?");
    public string SndStart => T("🚀 Zahájit přenos", "🚀 Start transfer");
    public string SndTransferring => T("Přenáším…", "Transferring…");
    public string Cancel => T("Zrušit", "Cancel");
    public string SndDoneTitle => T("✅  Přenos dokončen", "✅  Transfer finished");
    public string SndHome => T("Na úvod", "Home");
}
