namespace WhisperFTPApp.Validation;

public static class FtpConnectionValidator
{
    public static ValidationResult Validate(string? address, string? username, string? password, int port)
    {
        if (string.IsNullOrWhiteSpace(address))
            return ValidationResult.Error("FTP address is required");

        if (string.IsNullOrWhiteSpace(username))
            return ValidationResult.Error("Username is required");

        if (string.IsNullOrWhiteSpace(password))
            return ValidationResult.Error("Password is required");

        if (port < 1 || port > 65535)
            return ValidationResult.Error("Port must be between 1 and 65535");

        var testAddress = address.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase)
            ? address
            : $"ftp://{address}";

        if (!Uri.TryCreate(testAddress, UriKind.Absolute, out var uri))
            return ValidationResult.Error("Invalid FTP address format");

        if (uri.Scheme != Uri.UriSchemeFtp)
            return ValidationResult.Error("Address must use FTP protocol");

        return ValidationResult.Success();
    }

    public static ValidationResult ValidateRemotePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return ValidationResult.Error("Remote path is required");

        if (path.Contains("..", StringComparison.Ordinal))
            return ValidationResult.Error("Path traversal not allowed");

        return ValidationResult.Success();
    }
}
