# Runtime Cache Architecture

This document describes the caching architecture for license validation in the Chainborn Licensing platform, including both in-memory and file-based cache implementations.

## Overview

The validation cache reduces the cost of expensive zero-knowledge proof verifications by storing successful validation results with configurable time-to-live (TTL). The platform supports two cache implementations:

- **InMemoryValidationCache**: Fast, in-memory caching suitable for development and single-instance deployments
- **FileValidationCache**: Persistent, file-based caching designed for production and containerized environments

## File-Based Cache (FileValidationCache)

The `FileValidationCache` provides durable caching that persists across application restarts, container restarts, and pod rescheduling in Kubernetes environments.

### Features

- **Persistent Storage**: Cache entries are stored as JSON files on disk
- **TTL Enforcement**: Expired entries are never returned and are automatically cleaned up
- **LRU Eviction**: Least Recently Used policy with configurable maximum entries (default: 100)
- **Thread-Safe**: Concurrent read/write operations are safely handled
- **Async I/O**: All file operations use async I/O for optimal performance
- **Graceful Degradation**: Falls back on errors without crashing the application
- **SHA256 Hashed Filenames**: Cache keys are hashed for safe filesystem storage

### Configuration

Configure the file cache through `LicenseValidationOptions`:

```csharp
services.AddLicenseValidation(options =>
{
    // Enable file-based cache (default behavior)
    options.CacheDirectory = "/var/chainborn/cache";
    
    // Set maximum cache entries (default: 100)
    options.MaxCacheEntries = 100;
    
    // To disable file cache and use in-memory instead:
    // options.CacheDirectory = null;
});
```

### Directory Structure

The cache directory contains JSON files named using SHA256 hashes:

```
/var/chainborn/cache/
├── 3a8f7b2c1d4e5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a.json
├── 5f6a7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6.json
└── 7b8c9d0e1f2a3b4c5d6e7f8a9b0c1d2e3f4a5b6c7d8e9f0a1b2c3d4e5f6a7b8.json
```

Each file contains a cache entry:

```json
{
  "Result": {
    "IsValid": true,
    "Errors": [],
    "ValidatedAt": "2026-02-08T19:00:00.000Z",
    "ExpiresAt": "2026-02-08T20:00:00.000Z",
    "CacheKey": "product-123:nonce-abc:strict"
  },
  "ExpiresAt": "2026-02-08T19:30:00.000Z"
}
```

### Cache Key Generation

Cache keys are deterministically generated from:
- Product ID
- Challenge/Nonce
- Binding data (if any)
- Strictness mode

The key is then hashed using SHA256 to create a safe filename.

## Docker Integration

### Basic Docker Setup

When running in Docker, mount a volume to persist the cache directory:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app

# Create cache directory
RUN mkdir -p /var/chainborn/cache && \
    chmod 755 /var/chainborn/cache

# Copy application
COPY --from=build /app/publish .

# Set cache environment variable (optional)
ENV Licensing__CacheDirectory=/var/chainborn/cache
ENV Licensing__MaxCacheEntries=100

ENTRYPOINT ["dotnet", "YourApp.dll"]
```

### Docker Run

Run with a volume mount to persist cache:

```bash
docker run -d \
  -v license-cache:/var/chainborn/cache \
  -p 8080:8080 \
  your-app:latest
```

### Docker Compose Example

Complete example showing persistent cache across restarts:

```yaml
version: '3.8'

services:
  sample-app:
    build:
      context: .
      dockerfile: src/sample-app/Chainborn.Licensing.SampleApp/Dockerfile
    ports:
      - "8080:8080"
    environment:
      - Licensing__CacheDirectory=/var/chainborn/cache
      - Licensing__MaxCacheEntries=100
      - Licensing__PolicyDirectory=/etc/chainborn/policies
    volumes:
      # Persistent cache volume
      - license-cache:/var/chainborn/cache
      # Policy files (read-only)
      - ./policies:/etc/chainborn/policies:ro
    restart: unless-stopped
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:8080/health"]
      interval: 30s
      timeout: 10s
      retries: 3

volumes:
  # Named volume persists cache across container restarts
  license-cache:
    driver: local
```

**Testing Persistence:**

```bash
# Start the application
docker-compose up -d

# Perform some validations (cache will be populated)
curl http://localhost:8080/validate

# Restart the container
docker-compose restart sample-app

# Validations should now be served from cache
# (no expensive proof verification)
curl http://localhost:8080/validate
```

## Kubernetes Integration

### PersistentVolumeClaim

For Kubernetes deployments, use a PersistentVolumeClaim:

```yaml
apiVersion: v1
kind: PersistentVolumeClaim
metadata:
  name: license-cache-pvc
spec:
  accessModes:
    - ReadWriteOnce
  resources:
    requests:
      storage: 1Gi
  storageClassName: standard
---
apiVersion: apps/v1
kind: Deployment
metadata:
  name: sample-app
spec:
  replicas: 1
  selector:
    matchLabels:
      app: sample-app
  template:
    metadata:
      labels:
        app: sample-app
    spec:
      containers:
      - name: app
        image: your-registry/sample-app:latest
        env:
        - name: Licensing__CacheDirectory
          value: /var/chainborn/cache
        - name: Licensing__MaxCacheEntries
          value: "100"
        volumeMounts:
        - name: cache
          mountPath: /var/chainborn/cache
        - name: policies
          mountPath: /etc/chainborn/policies
          readOnly: true
      volumes:
      - name: cache
        persistentVolumeClaim:
          claimName: license-cache-pvc
      - name: policies
        configMap:
          name: license-policies
```

### Multiple Replicas

**Important**: The file-based cache is designed for single-instance deployments. For multi-replica deployments, consider:

1. **Shared Storage**: Use ReadWriteMany (RWX) storage with proper locking (e.g., NFS, EFS)
2. **Redis Cache**: Implement a distributed cache using Redis
3. **Per-Pod Cache**: Use ReadWriteOnce (RWO) with pod affinity for session stickiness

## Operational Considerations

### Directory Permissions

The application needs read/write permissions for the cache directory:

```bash
# Set ownership
chown -R app:app /var/chainborn/cache

# Set permissions
chmod 755 /var/chainborn/cache
```

### Directory Creation

The cache automatically creates the directory if it doesn't exist. If creation fails (e.g., due to permissions), the application will throw an exception on first cache access.

### Graceful Degradation

If the cache directory becomes unavailable (e.g., disk full, permissions changed), individual cache operations will log errors but won't crash the application. Validation will continue without caching.

### Monitoring

Key metrics to monitor:

- **Cache Hit Rate**: Percentage of validations served from cache
- **Cache Size**: Number of entries in the cache
- **Eviction Count**: Number of LRU evictions
- **Disk Usage**: Space used by cache directory
- **File I/O Errors**: Failed read/write operations

### Capacity Planning

Calculate cache storage requirements:

```
Average Entry Size: ~1 KB (JSON file)
Max Entries: 100 (configurable)
Total Storage: ~100 KB
Recommended: 10 MB (for overhead and growth)
```

For larger deployments:

```
Max Entries: 1000
Total Storage: ~1 MB
Recommended: 50 MB
```

## Cache Lifecycle

### Startup

1. Application starts
2. Cache directory is checked/created
3. Existing cache files are loaded
4. Expired entries are removed
5. Metadata index is built in memory

### Runtime

1. **Cache Miss**: Validation performs proof verification, result is cached
2. **Cache Hit**: Validation returns cached result (if not expired)
3. **TTL Expiry**: Expired entries are removed on read
4. **LRU Eviction**: When max entries reached, least recently used entry is evicted

### Shutdown

Cache files remain on disk. No explicit cleanup needed.

## Comparison: In-Memory vs File-Based Cache

| Feature | InMemoryValidationCache | FileValidationCache |
|---------|------------------------|---------------------|
| **Persistence** | No | Yes |
| **Performance** | Fastest | Fast (async I/O) |
| **Thread-Safe** | Yes (ConcurrentDictionary) | Yes (SemaphoreSlim) |
| **Max Entries** | Unlimited (memory bound) | Configurable (LRU) |
| **Docker Ready** | No | Yes |
| **Multi-Instance** | No | No (requires shared storage) |
| **Best For** | Development, testing | Production, containers |

## Switching Between Cache Implementations

### Use In-Memory Cache

```csharp
services.AddLicenseValidation(options =>
{
    options.CacheDirectory = null; // Disables file cache
});
```

### Use File Cache

```csharp
services.AddLicenseValidation(options =>
{
    options.CacheDirectory = "/var/chainborn/cache";
    options.MaxCacheEntries = 100;
});
```

### Custom Cache Implementation

Implement `IValidationCache` and register before calling `AddLicenseValidation`:

```csharp
services.AddSingleton<IValidationCache, RedisValidationCache>();
services.AddLicenseValidation(options => { /* ... */ });
```

## Security Considerations

### File System Security

- Cache directory should not be world-readable
- Use appropriate user/group ownership
- Consider encrypted storage for sensitive environments

### Cache Key Collisions

Cache keys include all validation factors (product ID, nonce, binding data, strictness mode) to prevent unauthorized cache hits.

### TTL Enforcement

TTL is enforced on every read operation. Even if a file exists, expired entries are never returned and are automatically deleted.

## Troubleshooting

### Cache Not Persisting

**Symptom**: Cache is empty after container restart

**Solutions**:
- Verify volume mount is configured correctly
- Check directory permissions
- Ensure volume is not being deleted on restart

### Permission Denied Errors

**Symptom**: `System.UnauthorizedAccessException` on cache operations

**Solutions**:
- Verify application has write permissions to cache directory
- Check SELinux/AppArmor policies in containerized environments
- Ensure volume mount has correct permissions

### Disk Full Errors

**Symptom**: `System.IO.IOException: There is not enough space on the disk`

**Solutions**:
- Increase volume size
- Reduce `MaxCacheEntries`
- Implement cache cleanup policies

### Slow Cache Operations

**Symptom**: High latency on cached validations

**Solutions**:
- Use faster storage (SSD vs HDD)
- Reduce `MaxCacheEntries` to minimize metadata overhead
- Consider using in-memory cache if persistence is not required

## Future Enhancements

Planned improvements for the caching system:

- [ ] Distributed cache implementation using Redis
- [ ] Cache warming strategies for frequently validated products
- [ ] Telemetry and metrics integration
- [ ] Automatic cache size management based on disk space
- [ ] Support for cache replication across instances
- [ ] Cache invalidation API endpoints

## Example: Complete ASP.NET Core Integration

```csharp
using Chainborn.Licensing.Validator;

var builder = WebApplication.CreateBuilder(args);

// Configure file-based caching
builder.Services.AddLicenseValidation(options =>
{
    options.PolicyDirectory = "/etc/chainborn/policies";
    options.CacheDirectory = "/var/chainborn/cache";
    options.MaxCacheEntries = 100;
});

var app = builder.Build();

app.MapGet("/health", () => 
    Results.Ok(new { status = "healthy", cache = "file-based" }));

app.Run();
```

## References

- [IValidationCache Interface](../../src/sdk/Chainborn.Licensing.Abstractions/IValidationCache.cs)
- [FileValidationCache Implementation](../../src/validator/Chainborn.Licensing.Validator/FileValidationCache.cs)
- [Validator Architecture](./README.md)
- [Sample Application](../../src/sample-app/Chainborn.Licensing.SampleApp/)
