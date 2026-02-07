namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Thrown when license validation fails in an unrecoverable way.
/// </summary>
public class LicenseValidationException : Exception
{
    public IReadOnlyList<string> ValidationErrors { get; }

    public LicenseValidationException(string message, IReadOnlyList<string>? errors = null)
        : base(message)
    {
        ValidationErrors = errors ?? Array.Empty<string>();
    }

    public LicenseValidationException(string message, Exception innerException, IReadOnlyList<string>? errors = null)
        : base(message, innerException)
    {
        ValidationErrors = errors ?? Array.Empty<string>();
    }
}
