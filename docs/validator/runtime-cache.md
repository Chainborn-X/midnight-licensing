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
    // Enable file-based cache
    options.CacheDirectory = "/var/chainborn/cache";

    // Set maximum cache entries (default: 100)
    options.MaxCacheEntries = 100;

    // To disable file cache and use in-memory instead:
    // options.CacheDirectory = null;
});
````

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

* Product ID
* Challenge/Nonce
* Binding data (if any)
* Strictness mode

The key is then hashed using SHA256 to create a safe filename.

## Docker Integration

### Docker Run

Run with a volume mount to persist cache:

```bash
docker run -d \
  -v license-cache:/var/chainborn/cache \
  -p 8080:8080 \
  your-app:latest
```

### Docker Compose Example

```yaml
version: "3.8"

services:
  sample-app:
    build:
      context: .
      dockerfile: src/sample-app/Chainborn.Licensing.SampleApp/Dockerfile
    image: chainborn-sample-app:latest
    container_name: chainborn-sample-app
    ports:
      - "8080:8080"
    environment:
      - ASPNETCORE_URLS=http://+:8080
      - ASPNETCORE_ENVIRONMENT=Production
      - Licensing__CacheDirectory=/var/chainborn/cache
      - Licensing__MaxCacheEntries=100
      - Licensing__PolicyDirectory=/etc/chainborn/policies
    volumes:
      - license-cache:/var/chainborn/cache
      - ./policies:/etc/chainborn/policies:ro
    restart: unless-stopped

volumes:
  license-cache:
    driver: local
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

Important: the file-based cache is designed for single-instance deployments. For multi-replica deployments, consider:

1. Shared storage: use ReadWriteMany (RWX) storage with proper locking (NFS, CephFS, Azure Files, etc.)
2. Redis cache: implement a distributed cache using Redis
3. Per-pod cache: use ReadWriteOnce (RWO) with pod affinity for session stickiness

## Comparison: In-Memory vs File-Based Cache

| Feature        | InMemoryValidationCache  | FileValidationCache                  |
| -------------- | ------------------------ | ------------------------------------ |
| Persistence    | No                       | Yes                                  |
| Performance    | Fastest                  | Fast (async I/O)                     |
| Thread-Safe    | Yes                      | Yes                                  |
| Max Entries    | Unlimited (memory bound) | Configurable (LRU)                   |
| Docker Ready   | No                       | Yes                                  |
| Multi-Instance | No                       | No (unless shared storage + locking) |
| Best For       | Development, testing     | Production, containers               |

## Switching Between Cache Implementations

### Use In-Memory Cache

```csharp
services.AddLicenseValidation(options =>
{
    options.CacheDirectory = null;
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
services.AddLicenseValidation(options => { });
```