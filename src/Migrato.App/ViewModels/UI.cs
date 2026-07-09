using static Migrato.Core.S;

namespace Migrato.App.ViewModels;

/// <summary>Texty XAML pohledů — jazyk se volí podle jazyka Windows (viz Migrato.Core.Lang).</summary>
public static class UI
{
    // Úvod / Home
    public static string HomeSubtitle => T(
        "Přestěhujte soubory, poštu, nastavení i programy na nový počítač přes Wi-Fi.",
        "Move your files, mail, settings and programs to a new computer over Wi-Fi.");
    public static string HomeOldTitle => T("📤  Tento počítač je STARÝ", "📤  This is the OLD computer");
    public static string HomeOldSub => T("Vybrat, co odeslat do nového počítače", "Choose what to send to the new computer");
    public static string HomeNewTitle => T("📥  Tento počítač je NOVÝ", "📥  This is the NEW computer");
    public static string HomeNewSub => T("Přijmout data ze starého počítače", "Receive data from the old computer");
    public static string HomeFooter => T(
        "Spusťte aplikaci na obou počítačích připojených ke stejné síti.",
        "Run the app on both computers connected to the same network.");

    public static string MadeBy => T(
        "vytvořil miragecze · zdrojáky na GitHubu", "made by miragecze · source on GitHub");

    // Příjem / Receive
    public static string RcvTitle => T("📥  Tento počítač přijme data", "📥  This computer will receive data");
    public static string RcvPinLabel => T("PIN pro spárování", "Pairing PIN");
    public static string RcvInstruction => T(
        "Na starém počítači zvolte „Tento počítač je STARÝ“, vyberte tento počítač a zadejte PIN.",
        "On the old computer choose “This is the OLD computer”, select this computer and enter the PIN.");
    public static string RcvReceiving => T("Přijímám data…", "Receiving data…");
    public static string RcvDone => T("✅  Hotovo", "✅  Done");

    // Odeslání / Send
    public static string SndPickTitle => T("Vyberte nový počítač", "Select the new computer");
    public static string SndPickHint => T(
        "Na novém počítači spusťte Migrato a zvolte „Tento počítač je NOVÝ“. Za chvíli se tu objeví.",
        "Run Migrato on the new computer and choose “This is the NEW computer”. It will appear here shortly.");
    public static string SndManualHint => T(
        "Nevidíte ho? Připojte se ručně adresou z obrazovky nového počítače:",
        "Can't see it? Connect manually using the address shown on the new computer:");
    public static string SndManualButton => T("Připojit ručně", "Connect manually");
    public static string Back => T("Zpět", "Back");
    public static string Continue => T("Pokračovat", "Continue");
    public static string SndPinTitle => T("Zadejte PIN", "Enter the PIN");
    public static string SndPinHint => T(
        "Šest číslic zobrazených na novém počítači", "The six digits shown on the new computer");
    public static string SndScanTitle => T("Zjišťuji, co lze přenést…", "Finding what can be transferred…");
    public static string SndSelectTitle => T("Co se má přenést?", "What should be transferred?");
    public static string SndStart => T("🚀 Zahájit přenos", "🚀 Start transfer");
    public static string SndTransferring => T("Přenáším…", "Transferring…");
    public static string Cancel => T("Zrušit", "Cancel");
    public static string SndDoneTitle => T("✅  Přenos dokončen", "✅  Transfer finished");
    public static string SndHome => T("Na úvod", "Home");
}
