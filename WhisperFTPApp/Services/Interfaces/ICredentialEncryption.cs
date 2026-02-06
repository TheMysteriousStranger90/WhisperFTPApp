namespace WhisperFTPApp.Services.Interfaces;

public interface ICredentialEncryption
{
    string Encrypt(string plainText);
    string Decrypt(string encryptedText);
}
