using System.Security.Cryptography;
using System.Text;

namespace AuditLog.API.Auth;

public static class ComplianceApiKeyValidator
{
    public static bool IsValid(
        string? providedKey,
        string? configuredKey)
    {
        if (string.IsNullOrWhiteSpace(providedKey) ||
            string.IsNullOrWhiteSpace(configuredKey) ||
            configuredKey.Length < 32)
        {
            return false;
        }

        var providedHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(providedKey));
        var configuredHash = SHA256.HashData(
            Encoding.UTF8.GetBytes(configuredKey));

        return CryptographicOperations.FixedTimeEquals(
            providedHash,
            configuredHash);
    }
}
