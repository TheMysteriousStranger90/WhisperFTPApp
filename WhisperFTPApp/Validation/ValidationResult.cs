namespace WhisperFTPApp.Validation;

public sealed class ValidationResult
{
    public bool IsValid { get; }
    public string ErrorMessage { get; }

    private ValidationResult(bool isValid, string errorMessage)
    {
        IsValid = isValid;
        ErrorMessage = errorMessage;
    }

    public static ValidationResult Success() => new(true, string.Empty);
    public static ValidationResult Error(string message) => new(false, message);
}
