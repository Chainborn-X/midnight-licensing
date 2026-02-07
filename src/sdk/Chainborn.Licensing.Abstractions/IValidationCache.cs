namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Caches license validation results with TTL support.
/// </summary>
public interface IValidationCache
{
    Task<LicenseValidationResult?> GetAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task SetAsync(string cacheKey, LicenseValidationResult result, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default);
}
