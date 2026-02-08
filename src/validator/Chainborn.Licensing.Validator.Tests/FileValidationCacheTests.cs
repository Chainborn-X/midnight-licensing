using Chainborn.Licensing.Abstractions;
using Chainborn.Licensing.Validator;
using Xunit;

namespace Chainborn.Licensing.Validator.Tests;

public class FileValidationCacheTests : IDisposable
{
    private readonly string _testCacheDirectory;
    private readonly FileValidationCache _cache;

    public FileValidationCacheTests()
    {
        _testCacheDirectory = Path.Combine(Path.GetTempPath(), $"test-cache-{Guid.NewGuid()}");
        _cache = new FileValidationCache(_testCacheDirectory, maxEntries: 10);
    }

    public void Dispose()
    {
        // Clean up test directory
        if (Directory.Exists(_testCacheDirectory))
        {
            Directory.Delete(_testCacheDirectory, recursive: true);
        }
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
            ExpiresAt: now.AddSeconds(-1),
            CacheKey: cacheKey
        );
        var ttl = TimeSpan.Zero; // Immediately expired TTL

        // Act
        await _cache.SetAsync(cacheKey, validationResult, ttl);
        
        // Wait a bit to ensure expiry
        await Task.Delay(100);
        
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

    [Fact]
    public async Task SetAsync_CreatesDirectoryIfNotExists()
    {
        // Arrange
        var newCacheDirectory = Path.Combine(Path.GetTempPath(), $"new-cache-{Guid.NewGuid()}");
        var cache = new FileValidationCache(newCacheDirectory);
        
        try
        {
            var cacheKey = "test-key";
            var now = DateTimeOffset.UtcNow;
            var validationResult = new LicenseValidationResult(
                IsValid: true,
                Errors: Array.Empty<string>(),
                ValidatedAt: now,
                ExpiresAt: now.AddMinutes(30),
                CacheKey: cacheKey
            );

            // Act
            await cache.SetAsync(cacheKey, validationResult, TimeSpan.FromMinutes(15));

            // Assert
            Assert.True(Directory.Exists(newCacheDirectory));
        }
        finally
        {
            if (Directory.Exists(newCacheDirectory))
            {
                Directory.Delete(newCacheDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task LruEviction_EvictsLeastRecentlyUsedEntry()
    {
        // Arrange - cache with max 3 entries
        var smallCache = new FileValidationCache(_testCacheDirectory, maxEntries: 3);
        var now = DateTimeOffset.UtcNow;

        // Add 3 entries
        for (int i = 0; i < 3; i++)
        {
            var result = new LicenseValidationResult(
                IsValid: true,
                Errors: Array.Empty<string>(),
                ValidatedAt: now,
                ExpiresAt: now.AddHours(1),
                CacheKey: $"key-{i}"
            );
            await smallCache.SetAsync($"key-{i}", result, TimeSpan.FromHours(1));
            await Task.Delay(10); // Ensure different access times
        }

        // Access key-1 and key-2 to make key-0 the LRU
        await smallCache.GetAsync("key-1");
        await Task.Delay(10);
        await smallCache.GetAsync("key-2");
        await Task.Delay(10);

        // Act - add a 4th entry, should evict key-0
        var newResult = new LicenseValidationResult(
            IsValid: true,
            Errors: Array.Empty<string>(),
            ValidatedAt: now,
            ExpiresAt: now.AddHours(1),
            CacheKey: "key-3"
        );
        await smallCache.SetAsync("key-3", newResult, TimeSpan.FromHours(1));

        // Assert
        Assert.Null(await smallCache.GetAsync("key-0")); // Should be evicted
        Assert.NotNull(await smallCache.GetAsync("key-1")); // Should still exist
        Assert.NotNull(await smallCache.GetAsync("key-2")); // Should still exist
        Assert.NotNull(await smallCache.GetAsync("key-3")); // Should exist
    }

    [Fact]
    public async Task FileCache_PersistsAcrossInstances()
    {
        // Arrange
        var cacheKey = "persistent-key";
        var now = DateTimeOffset.UtcNow;
        var validationResult = new LicenseValidationResult(
            IsValid: true,
            Errors: Array.Empty<string>(),
            ValidatedAt: now,
            ExpiresAt: now.AddHours(1),
            CacheKey: cacheKey
        );

        // Act - Set with first cache instance
        await _cache.SetAsync(cacheKey, validationResult, TimeSpan.FromHours(1));

        // Create a new cache instance pointing to the same directory
        var newCache = new FileValidationCache(_testCacheDirectory);
        var retrievedResult = await newCache.GetAsync(cacheKey);

        // Assert
        Assert.NotNull(retrievedResult);
        Assert.Equal(validationResult.IsValid, retrievedResult.IsValid);
        Assert.Equal(validationResult.CacheKey, retrievedResult.CacheKey);
    }

    [Fact]
    public async Task FileCache_RemovesExpiredEntriesOnStartup()
    {
        // Arrange - Add an expired entry
        var cacheKey = "expired-key";
        var now = DateTimeOffset.UtcNow;
        var validationResult = new LicenseValidationResult(
            IsValid: true,
            Errors: Array.Empty<string>(),
            ValidatedAt: now.AddSeconds(-10),
            ExpiresAt: now.AddSeconds(-5),
            CacheKey: cacheKey
        );
        
        await _cache.SetAsync(cacheKey, validationResult, TimeSpan.Zero);

        // Act - Create new cache instance (should clean up expired entries during initialization)
        await Task.Delay(100);
        var newCache = new FileValidationCache(_testCacheDirectory);
        var retrievedResult = await newCache.GetAsync(cacheKey);

        // Assert
        Assert.Null(retrievedResult);
    }

    [Fact]
    public async Task FileCache_HandlesCorruptedFiles()
    {
        // Arrange - Write a corrupted file
        var cacheDirectory = Path.Combine(Path.GetTempPath(), $"corrupt-cache-{Guid.NewGuid()}");
        Directory.CreateDirectory(cacheDirectory);
        
        try
        {
            var corruptFile = Path.Combine(cacheDirectory, "corrupt.json");
            await File.WriteAllTextAsync(corruptFile, "{ invalid json }");

            // Act - Create cache and try to get from corrupted key
            var cache = new FileValidationCache(cacheDirectory);
            var result = await cache.GetAsync("any-key");

            // Assert - Should handle gracefully
            Assert.Null(result);
        }
        finally
        {
            if (Directory.Exists(cacheDirectory))
            {
                Directory.Delete(cacheDirectory, recursive: true);
            }
        }
    }

    [Fact]
    public async Task FileCache_ThreadSafe_ConcurrentAccess()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var tasks = new List<Task>();

        // Act - Perform concurrent reads and writes (within cache limit)
        for (int i = 0; i < 8; i++)
        {
            var index = i;
            tasks.Add(Task.Run(async () =>
            {
                var result = new LicenseValidationResult(
                    IsValid: true,
                    Errors: Array.Empty<string>(),
                    ValidatedAt: now,
                    ExpiresAt: now.AddHours(1),
                    CacheKey: $"concurrent-key-{index}"
                );
                await _cache.SetAsync($"key-{index}", result, TimeSpan.FromHours(1));
                await _cache.GetAsync($"key-{index}");
            }));
        }

        // Assert - Should complete without errors
        await Task.WhenAll(tasks);
        
        // Verify entries exist (should be under the max limit)
        var firstResult = await _cache.GetAsync("key-0");
        var lastResult = await _cache.GetAsync("key-7");
        Assert.NotNull(firstResult);
        Assert.NotNull(lastResult);
    }

    [Fact]
    public async Task FileCache_UsesHashedFileNames()
    {
        // Arrange
        var cacheKey = "my-cache-key-with-special-chars-!@#$%";
        var now = DateTimeOffset.UtcNow;
        var validationResult = new LicenseValidationResult(
            IsValid: true,
            Errors: Array.Empty<string>(),
            ValidatedAt: now,
            ExpiresAt: now.AddHours(1),
            CacheKey: cacheKey
        );

        // Act
        await _cache.SetAsync(cacheKey, validationResult, TimeSpan.FromHours(1));

        // Assert - Check that the file exists and has a safe filename (hex hash)
        var files = Directory.GetFiles(_testCacheDirectory, "*.json");
        Assert.Single(files);
        
        var fileName = Path.GetFileName(files[0]);
        // Should be a hex string (64 chars for SHA256) + .json
        Assert.Matches(@"^[a-f0-9]{64}\.json$", fileName);
    }
}
