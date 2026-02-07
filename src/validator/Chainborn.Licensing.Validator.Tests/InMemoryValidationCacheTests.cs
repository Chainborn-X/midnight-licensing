using Chainborn.Licensing.Abstractions;
using Chainborn.Licensing.Validator.Mocks;
using Xunit;

namespace Chainborn.Licensing.Validator.Tests;

public class InMemoryValidationCacheTests
{
    private readonly InMemoryValidationCache _cache;

    public InMemoryValidationCacheTests()
    {
        _cache = new InMemoryValidationCache();
    }

    [Fact]
    public async Task GetAsync_WithNonExistentKey_ReturnsNull()
    {
        // Act
        var result = await _cache.GetAsync("non-existent-key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAsync_ThenGetAsync_ReturnsStoredResult()
    {
        // Arrange
        var cacheKey = "test-key";
        var now = DateTimeOffset.UtcNow;
        var validationResult = new LicenseValidationResult(
            IsValid: true,
            Errors: Array.Empty<string>(),
            ValidatedAt: now,
            ExpiresAt: now.AddMinutes(30),
            CacheKey: cacheKey
        );
        var ttl = TimeSpan.FromMinutes(15);

        // Act
        await _cache.SetAsync(cacheKey, validationResult, ttl);
        var retrievedResult = await _cache.GetAsync(cacheKey);

        // Assert
        Assert.NotNull(retrievedResult);
        Assert.Equal(validationResult.IsValid, retrievedResult.IsValid);
        Assert.Equal(validationResult.CacheKey, retrievedResult.CacheKey);
    }

    [Fact]
    public async Task GetAsync_WithExpiredEntry_ReturnsNull()
    {
        // Arrange
        var cacheKey = "expired-key";
        var now = DateTimeOffset.UtcNow;
        var validationResult = new LicenseValidationResult(
            IsValid: true,
            Errors: Array.Empty<string>(),
            ValidatedAt: now.AddSeconds(-10),
            ExpiresAt: now.AddSeconds(-1), // Already expired
            CacheKey: cacheKey
        );
        var ttl = TimeSpan.FromMilliseconds(1); // Very short TTL

        // Act
        await _cache.SetAsync(cacheKey, validationResult, ttl);
        await Task.Delay(10); // Wait for expiry
        var retrievedResult = await _cache.GetAsync(cacheKey);

        // Assert
        Assert.Null(retrievedResult);
    }

    [Fact]
    public async Task InvalidateAsync_RemovesCachedEntry()
    {
        // Arrange
        var cacheKey = "test-key";
        var now = DateTimeOffset.UtcNow;
        var validationResult = new LicenseValidationResult(
            IsValid: true,
            Errors: Array.Empty<string>(),
            ValidatedAt: now,
            ExpiresAt: now.AddMinutes(30),
            CacheKey: cacheKey
        );
        var ttl = TimeSpan.FromMinutes(15);

        await _cache.SetAsync(cacheKey, validationResult, ttl);

        // Act
        await _cache.InvalidateAsync(cacheKey);
        var retrievedResult = await _cache.GetAsync(cacheKey);

        // Assert
        Assert.Null(retrievedResult);
    }

    [Fact]
    public async Task SetAsync_WithSameKey_OverwritesExistingEntry()
    {
        // Arrange
        var cacheKey = "test-key";
        var now = DateTimeOffset.UtcNow;
        var firstResult = new LicenseValidationResult(
            IsValid: true,
            Errors: Array.Empty<string>(),
            ValidatedAt: now,
            ExpiresAt: now.AddMinutes(30),
            CacheKey: "first"
        );
        var secondResult = new LicenseValidationResult(
            IsValid: false,
            Errors: new[] { "error" },
            ValidatedAt: now,
            ExpiresAt: now.AddMinutes(30),
            CacheKey: "second"
        );
        var ttl = TimeSpan.FromMinutes(15);

        // Act
        await _cache.SetAsync(cacheKey, firstResult, ttl);
        await _cache.SetAsync(cacheKey, secondResult, ttl);
        var retrievedResult = await _cache.GetAsync(cacheKey);

        // Assert
        Assert.NotNull(retrievedResult);
        Assert.Equal(secondResult.IsValid, retrievedResult.IsValid);
        Assert.Equal(secondResult.CacheKey, retrievedResult.CacheKey);
    }
}
