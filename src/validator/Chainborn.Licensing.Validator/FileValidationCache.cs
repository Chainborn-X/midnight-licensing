using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Chainborn.Licensing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Chainborn.Licensing.Validator;

/// <summary>
/// File-based implementation of IValidationCache that persists validation results to disk.
/// Provides durability across container restarts with configurable LRU eviction policy.
/// </summary>
public class FileValidationCache : IValidationCache
{
    private readonly string _cacheDirectory;
    private readonly int _maxEntries;
    private readonly ILogger<FileValidationCache>? _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    
    // Maps filename (hash) to metadata for LRU tracking
    private readonly ConcurrentDictionary<string, CacheMetadata> _metadata = new();
    private bool _isInitialized;
    private bool _isDisabled; // Set to true if cache initialization fails

    /// <summary>
    /// Creates a new FileValidationCache instance.
    /// </summary>
    /// <param name="cacheDirectory">Directory for cache storage. Defaults to /var/chainborn/cache.</param>
    /// <param name="maxEntries">Maximum number of cache entries. Defaults to 100. Uses LRU eviction.</param>
    /// <param name="logger">Optional logger for diagnostics.</param>
    public FileValidationCache(
        string? cacheDirectory = null,
        int maxEntries = 100,
        ILogger<FileValidationCache>? logger = null)
    {
        _cacheDirectory = cacheDirectory ?? "/var/chainborn/cache";
        _maxEntries = maxEntries > 0 ? maxEntries : 100;
        _logger = logger;
    }

    public async Task<LicenseValidationResult?> GetAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_isDisabled)
        {
            return null;
        }

        var fileName = GetFileName(cacheKey);
        var filePath = Path.Combine(_cacheDirectory, fileName);

        try
        {
            if (!File.Exists(filePath))
            {
                return null;
            }

            CacheEntry? entry;
            
            // Read and deserialize the entry
            try
            {
                await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                entry = await JsonSerializer.DeserializeAsync<CacheEntry>(stream, cancellationToken: cancellationToken);
            }
            catch (JsonException ex)
            {
                _logger?.LogWarning(ex, "Corrupted cache entry at {FilePath}, deleting", filePath);
                // Delete corrupted file to prevent repeated errors
                try
                {
                    File.Delete(filePath);
                    _metadata.TryRemove(fileName, out _);
                }
                catch (Exception deleteEx)
                {
                    _logger?.LogWarning(deleteEx, "Failed to delete corrupted cache entry at {FilePath}", filePath);
                }
                return null;
            }

            if (entry == null)
            {
                _logger?.LogWarning("Failed to deserialize cache entry from {FilePath}, deleting", filePath);
                // Delete corrupted file to prevent repeated errors
                try
                {
                    File.Delete(filePath);
                    _metadata.TryRemove(fileName, out _);
                }
                catch (Exception deleteEx)
                {
                    _logger?.LogWarning(deleteEx, "Failed to delete corrupted cache entry at {FilePath}", filePath);
                }
                return null;
            }

            // Check TTL - remove expired entries (file is now closed, safe to delete)
            if (entry.ExpiresAt <= DateTimeOffset.UtcNow)
            {
                _logger?.LogDebug("Cache entry expired for key {CacheKey}", cacheKey);
                await InvalidateAsync(cacheKey, cancellationToken);
                return null;
            }

            // Update access time for LRU tracking (use filename as key)
            if (_metadata.TryGetValue(fileName, out var metadata))
            {
                metadata.LastAccessedAt = DateTimeOffset.UtcNow;
            }

            _logger?.LogDebug("Cache hit for key {CacheKey}", cacheKey);
            return entry.Result;
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error reading cache entry for key {CacheKey}", cacheKey);
            return null;
        }
    }

    public async Task SetAsync(string cacheKey, LicenseValidationResult result, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_isDisabled)
        {
            return;
        }

        var fileName = GetFileName(cacheKey);
        var filePath = Path.Combine(_cacheDirectory, fileName);
        var expiresAt = DateTimeOffset.UtcNow + ttl;
        var entry = new CacheEntry(result, expiresAt);

        string? tempPath = null;
        try
        {
            // Determine whether this is a new cache entry or an update to an existing one
            // Check if file exists as it's more reliable than metadata (which may not be loaded yet)
            var isNewEntry = !File.Exists(filePath);

            // Only evict if adding a new entry could increase the total count
            if (isNewEntry)
            {
                await EvictIfNeededAsync(cancellationToken);
            }

            // Write to a temporary file first, then move to avoid partial writes
            tempPath = filePath + ".tmp";
            await using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None, 4096, useAsync: true))
            {
                await JsonSerializer.SerializeAsync(stream, entry, cancellationToken: cancellationToken);
            }

            // Atomic move
            File.Move(tempPath, filePath, overwrite: true);
            tempPath = null; // Successfully moved, no need to clean up

            // Update metadata (use filename as key for consistent lookup)
            _metadata[fileName] = new CacheMetadata
            {
                FileName = fileName,
                CreatedAt = DateTimeOffset.UtcNow,
                LastAccessedAt = DateTimeOffset.UtcNow,
                ExpiresAt = expiresAt
            };

            _logger?.LogDebug("Cache entry stored for key {CacheKey}", cacheKey);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error writing cache entry for key {CacheKey}", cacheKey);
            // Fail gracefully - cache is best-effort
        }
        finally
        {
            // Clean up temp file if it still exists (write failed)
            if (tempPath != null)
            {
                try
                {
                    if (File.Exists(tempPath))
                    {
                        File.Delete(tempPath);
                    }
                }
                catch (Exception cleanupEx)
                {
                    _logger?.LogWarning(cleanupEx, "Failed to clean up temporary file {TempPath}", tempPath);
                }
            }
        }
    }

    public async Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);

        if (_isDisabled)
        {
            return;
        }

        var fileName = GetFileName(cacheKey);
        var filePath = Path.Combine(_cacheDirectory, fileName);

        try
        {
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
            }

            // Remove from metadata (use filename as key)
            _metadata.TryRemove(fileName, out _);
            _logger?.LogDebug("Cache entry invalidated for key {CacheKey}", cacheKey);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error deleting cache entry for key {CacheKey}", cacheKey);
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_isInitialized)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (_isInitialized)
            {
                return;
            }

            // Create directory if it doesn't exist
            if (!Directory.Exists(_cacheDirectory))
            {
                try
                {
                    Directory.CreateDirectory(_cacheDirectory);
                    _logger?.LogInformation("Created cache directory at {CacheDirectory}", _cacheDirectory);
                }
                catch (Exception ex)
                {
                    // Directory creation failed; log and gracefully disable file-based caching
                    _logger?.LogError(ex, "Failed to create cache directory at {CacheDirectory}. File-based cache will be disabled.", _cacheDirectory);
                    _isDisabled = true;
                    _isInitialized = true;
                    return;
                }
            }

            // Load existing cache metadata and clean up stale temp files
            await LoadMetadataAsync(cancellationToken);

            _isInitialized = true;
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task LoadMetadataAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Clean up stale temporary files from interrupted writes
            var tempFiles = Directory.GetFiles(_cacheDirectory, "*.tmp");
            foreach (var tempFile in tempFiles)
            {
                try
                {
                    File.Delete(tempFile);
                    _logger?.LogDebug("Deleted stale temporary file {TempFile}", tempFile);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to delete stale temporary file {TempFile}", tempFile);
                }
            }

            var files = Directory.GetFiles(_cacheDirectory, "*.json");
            
            foreach (var filePath in files)
            {
                try
                {
                    var fileInfo = new FileInfo(filePath);
                    CacheEntry? entry;
                    
                    // Read and close the stream before any file operations
                    try
                    {
                        await using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
                        entry = await JsonSerializer.DeserializeAsync<CacheEntry>(stream, cancellationToken: cancellationToken);
                    }
                    catch (JsonException ex)
                    {
                        _logger?.LogWarning(ex, "Corrupted cache entry at {FilePath}, deleting", filePath);
                        // Delete corrupted file
                        try
                        {
                            File.Delete(filePath);
                        }
                        catch (Exception deleteEx)
                        {
                            _logger?.LogWarning(deleteEx, "Failed to delete corrupted cache entry at {FilePath}", filePath);
                        }
                        continue;
                    }

                    if (entry != null)
                    {
                        // Check if entry is still valid (stream is now closed, safe to delete)
                        if (entry.ExpiresAt > DateTimeOffset.UtcNow)
                        {
                            // Use filename as the key in metadata for consistent lookup
                            _metadata[fileInfo.Name] = new CacheMetadata
                            {
                                FileName = fileInfo.Name,
                                CreatedAt = fileInfo.CreationTimeUtc,
                                LastAccessedAt = fileInfo.LastAccessTimeUtc,
                                ExpiresAt = entry.ExpiresAt
                            };
                        }
                        else
                        {
                            // Delete expired entry (stream is now closed, safe to delete)
                            try
                            {
                                File.Delete(filePath);
                            }
                            catch (Exception deleteEx)
                            {
                                _logger?.LogWarning(deleteEx, "Failed to delete expired cache entry at {FilePath}", filePath);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to load cache entry from {FilePath}", filePath);
                }
            }

            _logger?.LogInformation("Loaded {Count} cache entries from disk", _metadata.Count);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading cache metadata from {CacheDirectory}", _cacheDirectory);
        }
    }

    private async Task EvictIfNeededAsync(CancellationToken cancellationToken)
    {
        if (_metadata.Count < _maxEntries)
        {
            return;
        }

        await _lock.WaitAsync(cancellationToken);
        try
        {
            // Recheck after acquiring lock
            if (_metadata.Count < _maxEntries)
            {
                return;
            }

            // Find least recently used entry
            var lruEntry = _metadata
                .OrderBy(kvp => kvp.Value.LastAccessedAt)
                .FirstOrDefault();

            if (!string.IsNullOrEmpty(lruEntry.Key))
            {
                var filePath = Path.Combine(_cacheDirectory, lruEntry.Key);
                
                try
                {
                    if (File.Exists(filePath))
                    {
                        File.Delete(filePath);
                    }

                    _metadata.TryRemove(lruEntry.Key, out _);
                    _logger?.LogDebug("Evicted LRU cache entry: {FileName}", lruEntry.Key);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Error evicting cache entry: {FileName}", lruEntry.Key);
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static string GetFileName(string cacheKey)
    {
        // Use SHA256 hash to generate a safe filename
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(cacheKey));
        var hash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        return $"{hash}.json";
    }

    private record CacheEntry(LicenseValidationResult Result, DateTimeOffset ExpiresAt);

    private class CacheMetadata
    {
        public required string FileName { get; init; }
        public required DateTimeOffset CreatedAt { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public DateTimeOffset LastAccessedAt { get; set; }
    }
}
