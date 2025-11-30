using System.Net;

namespace WhisperFTPApp.Configurations;

public sealed class FtpConfiguration
{
    public required string FtpAddress { get; init; }
    public required string Username { get; init; }
    public required string Password { get; init; }
    public int Port { get; init; } = 21;
    public int Timeout { get; init; } = 10000;
    public bool EnableSsl { get; init; }
    public bool UsePassive { get; init; } = true;
    public bool UseBinary { get; init; } = true;
    public bool KeepAlive { get; init; } = true;
    public int ReadWriteTimeout { get; init; } = 30000;
    public IWebProxy? Proxy { get; init; }
    public bool UseDefaultCredentials { get; init; }
    public string? ConnectionGroupName { get; init; }

    public System.Net.Security.AuthenticationLevel AuthenticationLevel { get; init; } =
        System.Net.Security.AuthenticationLevel.MutualAuthRequested;

    public System.Security.Principal.TokenImpersonationLevel ImpersonationLevel { get; init; } =
        System.Security.Principal.TokenImpersonationLevel.Delegation;

    public int BufferSize { get; init; } = 131072;

    public int MaxRetries { get; init; } = 3;

    public int RetryDelay { get; init; } = 2000;

    public FtpConfiguration()
    {
    }

    public FtpConfiguration(
        string ftpAddress,
        string username,
        string password,
        int port = 21,
        int timeout = 10000,
        bool enableSsl = false,
        bool usePassive = true,
        bool keepAlive = true)
    {
        FtpAddress = ftpAddress;
        Username = username;
        Password = password;
        Port = port;
        Timeout = timeout;
        EnableSsl = enableSsl;
        UsePassive = usePassive;
        KeepAlive = keepAlive;
    }

    public static FtpConfiguration CreateSecure(
        string ftpAddress,
        string username,
        string password,
        int port = 21)
    {
        return new FtpConfiguration
        {
            FtpAddress = ftpAddress,
            Username = username,
            Password = password,
            Port = port,
            EnableSsl = true,
            UsePassive = true,
            KeepAlive = true,
            UseBinary = true
        };
    }

    public static FtpConfiguration CreateAnonymous(string ftpAddress, int port = 21)
    {
        return new FtpConfiguration
        {
            FtpAddress = ftpAddress,
            Username = "anonymous",
            Password = "anonymous@example.com",
            Port = port,
            EnableSsl = false,
            UsePassive = true,
            KeepAlive = false,
            UseBinary = true
        };
    }

    public bool Validate(out string errorMessage)
    {
        if (string.IsNullOrWhiteSpace(FtpAddress))
        {
            errorMessage = "FTP address is required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Username))
        {
            errorMessage = "Username is required";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            errorMessage = "Password is required";
            return false;
        }

        if (Port < 1 || Port > 65535)
        {
            errorMessage = "Port must be between 1 and 65535";
            return false;
        }

        if (Timeout < 1000)
        {
            errorMessage = "Timeout must be at least 1000ms";
            return false;
        }

        errorMessage = string.Empty;
        return true;
    }

    public FtpConfiguration WithSsl(bool enable)
    {
        return new FtpConfiguration
        {
            FtpAddress = this.FtpAddress,
            Username = this.Username,
            Password = this.Password,
            Port = this.Port,
            Timeout = this.Timeout,
            EnableSsl = enable,
            UsePassive = this.UsePassive,
            UseBinary = this.UseBinary,
            KeepAlive = this.KeepAlive,
            ReadWriteTimeout = this.ReadWriteTimeout,
            Proxy = this.Proxy,
            UseDefaultCredentials = this.UseDefaultCredentials,
            ConnectionGroupName = this.ConnectionGroupName,
            AuthenticationLevel = this.AuthenticationLevel,
            ImpersonationLevel = this.ImpersonationLevel,
            BufferSize = this.BufferSize,
            MaxRetries = this.MaxRetries,
            RetryDelay = this.RetryDelay
        };
    }

    public FtpConfiguration WithProxy(IWebProxy? proxy)
    {
        return new FtpConfiguration
        {
            FtpAddress = this.FtpAddress,
            Username = this.Username,
            Password = this.Password,
            Port = this.Port,
            Timeout = this.Timeout,
            EnableSsl = this.EnableSsl,
            UsePassive = this.UsePassive,
            UseBinary = this.UseBinary,
            KeepAlive = this.KeepAlive,
            ReadWriteTimeout = this.ReadWriteTimeout,
            Proxy = proxy,
            UseDefaultCredentials = this.UseDefaultCredentials,
            ConnectionGroupName = this.ConnectionGroupName,
            AuthenticationLevel = this.AuthenticationLevel,
            ImpersonationLevel = this.ImpersonationLevel,
            BufferSize = this.BufferSize,
            MaxRetries = this.MaxRetries,
            RetryDelay = this.RetryDelay
        };
    }

    public FtpConfiguration WithTimeout(int timeout)
    {
        return new FtpConfiguration
        {
            FtpAddress = this.FtpAddress,
            Username = this.Username,
            Password = this.Password,
            Port = this.Port,
            Timeout = timeout,
            EnableSsl = this.EnableSsl,
            UsePassive = this.UsePassive,
            UseBinary = this.UseBinary,
            KeepAlive = this.KeepAlive,
            ReadWriteTimeout = this.ReadWriteTimeout,
            Proxy = this.Proxy,
            UseDefaultCredentials = this.UseDefaultCredentials,
            ConnectionGroupName = this.ConnectionGroupName,
            AuthenticationLevel = this.AuthenticationLevel,
            ImpersonationLevel = this.ImpersonationLevel,
            BufferSize = this.BufferSize,
            MaxRetries = this.MaxRetries,
            RetryDelay = this.RetryDelay
        };
    }
}
