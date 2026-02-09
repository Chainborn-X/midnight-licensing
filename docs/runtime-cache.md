# Runtime Cache Architecture

## Overview

The Chainborn Licensing Platform uses an in-memory validation cache to reduce the overhead of repeated proof verifications. This document describes the cache architecture, the critical TTL invariant that ensures correctness, and best practices for cache configuration.

## Purpose

The validation cache serves two main purposes:

1. **Performance Optimization**: ZK proof verification is computationally expensive. Caching successful validation results avoids redundant cryptographic operations.
2. **Reduced Latency**: Applications can validate licenses in microseconds from cache instead of milliseconds from proof verification.

## Cache TTL Invariant

### The Core Guarantee

The most critical property of the validation cache is the **TTL Invariant**:

> **For any cached validation result, the `ExpiresAt` timestamp MUST be less than or equal to the minimum of:**
> 1. **Proof expiry**: `challenge.ExpiresAt` (when the proof's challenge nonce expires)
> 2. **Cache TTL bound**: `validatedAt + policy.CacheTtl` (when the cache entry should expire)

Formally:

```
cached.ExpiresAt ≤ min(challenge.ExpiresAt, cached.ValidatedAt + policy.CacheTtl)
```

### Why This Matters

This invariant prevents two classes of cache-related bugs:

1. **Serving expired proofs from cache**: If a proof's challenge has expired, the cached result must also be expired, preventing the application from using an invalid proof.
2. **Serving stale results beyond policy TTL**: If the policy requires frequent re-validation (e.g., for revocation checks), the cache must respect that TTL even if the proof itself is still valid.

### Enforcement Points

The invariant is enforced at two points in the validation pipeline:

#### 1. On Cache Write (`LicenseValidator.ValidateAsync`)

When storing a validation result in cache, the validator calculates `ExpiresAt` using `CalculateExpiresAt`:

```csharp
private static DateTimeOffset CalculateExpiresAt(
    DateTimeOffset challengeExpiry,
    DateTimeOffset now,
    TimeSpan cacheTtl)
{
    // Take the minimum of challenge expiry and cache TTL
    var cacheBound = now + cacheTtl;
    return challengeExpiry < cacheBound ? challengeExpiry : cacheBound;
}
```

This ensures that the cached result's `ExpiresAt` is set to the minimum of the two bounds.

#### 2. On Cache Read (`LicenseValidator.ValidateAsync`)

When retrieving a validation result from cache, the validator verifies the invariant:

```csharp
var cachedResult = await _validationCache.GetAsync(cacheKey, cancellationToken);
if (cachedResult != null && cachedResult.ExpiresAt > now)
{
    // Verify cache invariant before returning cached result
    var policyForCacheValidation = await _policyProvider.GetPolicyAsync(context.ProductId, cancellationToken);
    if (policyForCacheValidation == null)
    {
        // If policy is not found, treat as cache miss and fall through to normal validation
        // This ensures consistent behavior between cache hit and cache miss paths
        _logger.LogWarning("Policy not found during cache validation, treating as cache miss");
    }
    else
    {
        var maxAllowedExpiry = CalculateExpiresAt(
            proof.Challenge.ExpiresAt,
            cachedResult.ValidatedAt,
            policyForCacheValidation.CacheTtl
        );
        
        if (cachedResult.ExpiresAt > maxAllowedExpiry)
        {
            _logger.LogError("Cache invariant violation detected...");
            
            // Invalidate the corrupted cache entry to prevent repeated failures
            await _validationCache.InvalidateAsync(cacheKey, cancellationToken);
            
            return new LicenseValidationResult(IsValid: false, ...);
        }
        
        return cachedResult;
    }
}
```

**Key behaviors:**

1. **Policy lookup failure**: If the policy cannot be found during cache validation, the system treats it as a cache miss and falls through to normal validation. This ensures consistent behavior between cache hit and cache miss paths.

2. **Invariant violation**: If the invariant is violated (which should never happen in normal operation), the validator:
   - Logs an error with full diagnostic information
   - **Invalidates the corrupted cache entry** to prevent repeated failures and allow the system to self-heal
   - Returns an invalid validation result with a clear error message

This defensive check protects against:

- Bugs in cache implementation
- Manual cache corruption
- Clock skew issues
- Race conditions during policy updates

## Cache Architecture

### Interface: `IValidationCache`

The cache is accessed through a simple interface:

```csharp
public interface IValidationCache
{
    Task<LicenseValidationResult?> GetAsync(string cacheKey, CancellationToken cancellationToken = default);
    Task SetAsync(string cacheKey, LicenseValidationResult result, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task InvalidateAsync(string cacheKey, CancellationToken cancellationToken = default);
}
```

### Default Implementation: `InMemoryValidationCache`

The default implementation is an in-memory cache backed by `ConcurrentDictionary`:

```csharp
private readonly ConcurrentDictionary<string, CacheEntry> _cache = new();

private record CacheEntry(LicenseValidationResult Result, DateTimeOffset ExpiresAt);
```

Key properties:

- **Thread-safe**: Uses `ConcurrentDictionary` for safe concurrent access
- **Automatic expiry**: Expired entries are removed on `GetAsync`
- **In-memory only**: Cache does not survive application restarts
- **No size limits**: Currently unbounded (should be addressed in production)

### Cache Key Generation

Cache keys are deterministic and include:

- Product ID
- Challenge nonce
- Strictness mode
- Binding data (serialized and Base64-encoded if present)

This ensures that different validation contexts produce different cache keys, preventing incorrect cache hits.

## Configuration

### Policy-Level Cache TTL

Each product policy defines a `cacheTtl` (in seconds):

```json
{
  "productId": "my-product",
  "cacheTtl": 3600,
  ...
}
```

**Guidelines for setting `cacheTtl`:**

- **High-revocation risk**: Use short TTL (e.g., 300-900 seconds / 5-15 minutes)
- **Low-revocation risk**: Use longer TTL (e.g., 3600-14400 seconds / 1-4 hours)
- **No revocation**: Can use very long TTL (e.g., 86400 seconds / 24 hours)

The cache TTL should be shorter than typical proof expiry times to ensure frequent re-validation.

### Proof-Level Challenge Expiry

Each proof includes a challenge with an `expiresAt` timestamp:

```json
{
  "challenge": {
    "nonce": "...",
    "issuedAt": "2026-02-07T10:00:00Z",
    "expiresAt": "2026-02-07T11:00:00Z"
  }
}
```

Challenge expiry is typically 1-24 hours from issuance. This is the absolute maximum time a proof can be considered valid.

### Interaction Between Cache TTL and Proof Expiry

The effective expiry of a cached validation is always the **minimum** of:

1. When the proof's challenge expires (`challenge.expiresAt`)
2. When the cache TTL expires (`validatedAt + policy.cacheTtl`)

**Example 1: Proof expires first**

- Proof challenge expires: 10 minutes from now
- Cache TTL: 30 minutes
- **Result**: Cached validation expires in 10 minutes

**Example 2: Cache TTL expires first**

- Proof challenge expires: 2 hours from now
- Cache TTL: 15 minutes
- **Result**: Cached validation expires in 15 minutes

This ensures that:

- Short-lived proofs are never cached longer than they're valid
- Long-lived proofs still get re-validated periodically per policy

## Testing

The cache TTL invariant is verified by integration tests in `LicenseValidatorTests.cs`:

### Test: Proof Expires Before Cache TTL

```csharp
[Fact]
public async Task ValidateAsync_CacheExpiresAt_RespectsChallengeExpiry()
{
    // Challenge expires in 10 minutes, cache TTL is 30 minutes
    // Result: ExpiresAt should be ~10 minutes (challenge expiry)
}
```

### Test: Cache TTL Expires Before Proof

```csharp
[Fact]
public async Task ValidateAsync_CacheExpiresAt_RespectsCacheTtl()
{
    // Challenge expires in 2 hours, cache TTL is 15 minutes
    // Result: ExpiresAt should be ~15 minutes (cache TTL)
}
```

### Test: Cache Round-Trip Maintains Invariant

```csharp
[Fact]
public async Task ValidateAsync_CacheRoundTrip_ExpiresAtInvariantMaintained()
{
    // Validates that cached results maintain the same ExpiresAt on retrieval
}
```

### Test: Invalid Cached Result Fails Validation

```csharp
[Fact]
public async Task ValidateAsync_CachedResult_WithInvalidExpiresAt_Fails()
{
    // Simulates a corrupted cache entry with ExpiresAt > maxAllowedExpiry
    // Result: Validation fails with "Cache invariant violation" error
    // Verifies that the corrupted cache entry is invalidated to prevent repeated failures
}
```

### Test: Policy Not Found During Cache Hit

```csharp
[Fact]
public async Task ValidateAsync_WithCachedResult_PolicyNotFound_TreatsAsCacheMiss()
{
    // Simulates policy lookup failure during cache hit
    // Result: Falls through to normal validation (cache miss behavior)
    // Ensures consistent handling between cache hit and cache miss paths
}
```

## Operational Considerations

### Cache Size Management

The current `InMemoryValidationCache` is unbounded. For production use, consider:

1. **LRU eviction**: Remove least-recently-used entries when cache grows large
2. **Max size limit**: Cap cache at N entries to prevent memory exhaustion
3. **Periodic cleanup**: Background task to remove expired entries
4. **Memory monitoring**: Alert when cache size exceeds thresholds

### Cache Warming

For high-traffic applications, consider pre-warming the cache at startup by validating commonly-used proofs.

### Distributed Caching

For multi-instance deployments, replace `InMemoryValidationCache` with a distributed cache:

- **Redis**: Fast, supports TTL, widely adopted
- **Memcached**: Simple, high-performance
- **SQL Database**: Persistent, queryable, but slower

When implementing distributed caching:

- Ensure the cache implementation respects TTL
- Use atomic operations to prevent race conditions
- Consider cache invalidation strategies for policy updates

### Clock Skew

The cache invariant depends on accurate clocks. In distributed systems:

- Use NTP to synchronize clocks across instances
- Monitor clock drift and alert on significant skew
- Consider using a centralized time source

### Policy Updates

When a policy's `cacheTtl` is reduced:

1. **Cache invalidation**: Invalidate all cache entries for that product
2. **Graceful expiry**: Allow old cache entries to expire naturally (may serve stale results)
3. **Version-based keys**: Include policy version in cache key to auto-invalidate

The current implementation does not automatically invalidate cache on policy changes. Applications should call `InvalidateAsync` or restart when policies are updated.

## Security Considerations

### Cache Poisoning

The cache is not directly accessible to users, but be aware of:

- **Proof replay**: If an attacker can replay a cached proof, they can bypass validation. This is prevented by nonce uniqueness.
- **Time-of-check to time-of-use**: Ensure cached results are used within a small window to prevent stale data exploitation.

### Information Leakage

Cache keys include product ID, nonce, and binding data. Do not expose cache keys in logs or error messages visible to end users.

### Denial of Service

An attacker could attempt to fill the cache with fake validation requests. Mitigations:

- **Rate limiting**: Limit validation requests per client
- **Cache size limits**: Prevent unbounded cache growth
- **Cache key validation**: Ensure cache keys are well-formed

## Future Enhancements

1. **Persistent cache**: Survive application restarts (file-based or database-backed)
2. **Cache metrics**: Track hit rate, eviction count, size over time
3. **Adaptive TTL**: Dynamically adjust cache TTL based on revocation rate
4. **Partial invalidation**: Invalidate cache entries by product ID or binding criteria
5. **Cache preloading**: Load frequently-used proofs at startup

## Related Documentation

- [Architecture Overview](architecture.md) - High-level system architecture
- [Policy Schema](policy-schema.md) - Policy configuration including `cacheTtl`
- [Proof Envelope](proof-envelope.md) - Proof format including challenge expiry
- [Validator Implementation](../src/validator/Chainborn.Licensing.Validator/LicenseValidator.cs) - Validator source code

## References

- Issue #17: Add integration tests and guard logic for cache TTL invariant
