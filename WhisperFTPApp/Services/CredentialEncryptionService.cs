using System.Security.Cryptography;
using System.Text;
using WhisperFTPApp.Logger;
using WhisperFTPApp.Services.Interfaces;

namespace WhisperFTPApp.Services;

internal sealed class CredentialEncryptionService : ICredentialEncryption
{
    private static readonly byte[] Entropy = "WhisperFTP_Salt_2024"u8.ToArray();

    public string Encrypt(string plainText)
    {
        if (string.IsNullOrEmpty(plainText))
            return string.Empty;

        try
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = ProtectedData.Protect(plainBytes, Entropy, DataProtectionScope.CurrentUser);
            return Convert.ToBase64String(encryptedBytes);
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Encryption failed: {ex.Message}");
            return plainText;
        }
    }

    public string Decrypt(string encryptedText)
    {
        if (string.IsNullOrEmpty(encryptedText))
            return string.Empty;

        try
        {
            var encryptedBytes = Convert.FromBase64String(encryptedText);
            var plainBytes = ProtectedData.Unprotect(encryptedBytes, Entropy, DataProtectionScope.CurrentUser);
            return Encoding.UTF8.GetString(plainBytes);
        }
        catch (FormatException)
        {
            return encryptedText;
        }
        catch (CryptographicException)
        {
            return encryptedText;
        }
        catch (Exception ex)
        {
            StaticFileLogger.LogError($"Decryption failed: {ex.Message}");
            return encryptedText;
        }
    }
}
