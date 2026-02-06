namespace WhisperFTPApp.Constants;

public static class AppConstants
{
    public const string DefaultBackground = "avares://AzioWhisperFTP/Assets/Image (3).jpg";
    public const string AvaresPrefix = "avares://AzioWhisperFTP";
    public const string DefaultLanguage = "en";
    public const int DefaultFtpPort = 21;
    public const int DefaultTimeout = 10000;
    public const int DefaultReadWriteTimeout = 30000;
    public const int DefaultBufferSize = 131072;
    public const int DefaultMaxRetries = 3;
    public const int DefaultRetryDelay = 2000;
    public const int FtpScanConcurrency = 10;
    public const int ConnectionRetryBaseDelay = 2000;
}
