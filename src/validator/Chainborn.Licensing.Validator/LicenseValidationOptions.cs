using Chainborn.Licensing.Abstractions;

namespace Chainborn.Licensing.Validator;

/// <summary>
/// Configuration options for license validation.
/// </summary>
public class LicenseValidationOptions
{
    /// <summary>
    /// Directory containing policy JSON files.
    /// </summary>
    public string PolicyDirectory { get; set; } = "/etc/chainborn/policies";

    /// <summary>
    /// Directory for caching validation results.
    /// </summary>
    public string? CacheDirectory { get; set; } = "/var/chainborn/cache";

    /// <summary>
    /// Default strictness mode for validation.
    /// </summary>
    public StrictnessMode DefaultStrictnessMode { get; set; } = StrictnessMode.Strict;
}
