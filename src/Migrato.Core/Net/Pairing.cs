using System.Security.Cryptography;
using System.Text;

namespace Migrato.Core.Net;

/// <summary>
/// PIN párování: příjemce zobrazí 6místný PIN, odesílatel ho zadá a pošle
/// HMAC-SHA256(klíč = PIN, data = otisk TLS certifikátu příjemce).
/// Vazba na otisk certifikátu zajišťuje, že útočník uprostřed (s vlastním
/// certifikátem, tedy jiným otiskem) neprojde ani se znalostí odposlechnuté HMAC.
/// </summary>
public static class Pairing
{
    public const int MaxAttempts = 5;

    public static string GeneratePin()
        => RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");

    public static string ComputeHmac(string pin, string serverFingerprint)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(pin));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(serverFingerprint)));
    }

    public static bool Verify(string pin, string serverFingerprint, string presentedHmacHex)
    {
        byte[] presented;
        try
        {
            presented = Convert.FromHexString(presentedHmacHex);
        }
        catch (FormatException)
        {
            return false;
        }

        byte[] expected = Convert.FromHexString(ComputeHmac(pin, serverFingerprint));
        return CryptographicOperations.FixedTimeEquals(expected, presented);
    }
}

public sealed class PairFailedException(string message) : Exception(message);
