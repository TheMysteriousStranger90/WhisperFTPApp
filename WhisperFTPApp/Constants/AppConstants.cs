namespace WhisperFTPApp.Constants;

internal static class AppConstants
{
    public const string DefaultBackground = "avares://AzioWhisperFTP/Assets/Image (3).jpg";
    public const string AvaresPrefix = "avares://AzioWhisperFTP";
    public const string DefaultLanguage = "en";
    public const int DefaultFtpPort = 21;
    public const int DefaultTimeout = 30000;
    public const int DefaultReadWriteTimeout = 60000;
    public const int DefaultBufferSize = 2097152;
    public const int DefaultMaxRetries = 3;
    public const int DefaultRetryDelay = 3000;
    public const int FtpScanConcurrency = 10;
    public const int ConnectionRetryBaseDelay = 2000;
}
