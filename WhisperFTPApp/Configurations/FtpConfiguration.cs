using WhisperFTPApp.Configurations.Abstract;

namespace WhisperFTPApp.Configurations;

public class FtpConfiguration : ConfigurationBase
{
    public FtpConfiguration(string ftpAddress, string username, string password, int port, int timeout) 
        : base(timeout)
    {
        FtpAddress = ftpAddress;
        Username = username;
        Password = password;
        Port = port;
    }

    public string FtpAddress { get; }
    public string Username { get; }
    public string Password { get; }
    public int Port { get; }
}