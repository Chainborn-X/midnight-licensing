using System.Collections.Concurrent;
using Chainborn.Licensing.Abstractions;

namespace Chainborn.Licensing.Validator.Mocks;

/// <summary>
/// In-memory implementation of IValidationCache for development and testing.
/// TODO: Replace with persistent caching solution (Redis, distributed cache, or file-based cache).
/// TODO: Add cache eviction policies and memory limits for production use.
/// TODO: Consider implementing cache warming strategies for frequently validated products.
/// </summary>
public class InMemoryValidationCache : IValidationCache
{
    private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

    public Task<LicenseValidationResult?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow)
            {
                return Task.FromResult<LicenseValidationResult?>(entry.Result);
            }

            // Remove expired entry
            _cache.TryRemove(cacheKey, out _);
        }

        return Task.FromResult<LicenseValidationResult?>(null);
    }

    public Task SetAsync(string cacheKey, LicenseValidationResult result, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        var expiresAt = DateTimeOffset.UtcNow + ttl;
        _cache[cacheKey] = new CacheEntry(result, expiresAt);
        return Task.CompletedTask;
    }

    public Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        _cache.TryRemove(cacheKey, out _);
        return Task.CompletedTask;
    }

    private record CacheEntry(LicenseValidationResult Result, DateTimeOffset ExpiresAt);
}
