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
    /// Directory for caching validation results. Set to null to use in-memory cache.
    /// </summary>
    public string? CacheDirectory { get; set; } = "/var/chainborn/cache";

    /// <summary>
    /// Maximum number of cache entries for file-based cache. Uses LRU eviction policy.
    /// Default is 100 entries.
    /// </summary>
    public int MaxCacheEntries { get; set; } = 100;

    /// <summary>
    /// Default strictness mode for validation.
    /// </summary>
    public StrictnessMode DefaultStrictnessMode { get; set; } = StrictnessMode.Strict;
}
