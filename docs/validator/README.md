# Validator Architecture

This document describes the architecture, interfaces, and implementation strategy for the Chainborn Licensing Validator library.

## Overview

The validator library is responsible for validating zero-knowledge proofs of license ownership against product policies. It provides a clean separation between the validation orchestration logic and the cryptographic proof verification, allowing for flexible implementation strategies.

## Core Interfaces

### ILicenseValidator

The main entry point for license validation.

**Responsibility**: Orchestrates the entire validation flow including cache lookup, policy retrieval, nonce validation, proof verification, and result caching.

**Method**:
```csharp
Task<LicenseValidationResult> ValidateAsync(
    LicenseProof proof,
    ValidationContext context,
    CancellationToken cancellationToken = default)
```

**Input**:
- `LicenseProof`: Contains the ZK proof bytes, verification key, product ID, and challenge/nonce
- `ValidationContext`: Provides validation parameters including product ID, strictness mode, and binding data

**Output**:
- `LicenseValidationResult`: Contains validation status, errors (if any), timestamps, and cache key

### IProofVerifier

The bridge between .NET and the Midnight ZK proof system.

**Responsibility**: Verifies a zero-knowledge proof cryptographically against a verification key and challenge.

**Method**:
```csharp
Task<ProofVerificationResult> VerifyAsync(
    byte[] proof,
    byte[] verificationKey,
    ProofChallenge challenge,
    CancellationToken cancellationToken = default)
```

**Current Implementation**: `MockProofVerifier` (stub that accepts all valid-looking proofs)

**Future Implementation**: 
- Real ZK proof verification will be implemented after spike-003-zk-proof-dotnet is complete
- Will use WASM-based Midnight proof verification or native interop
- See TODOs in `Mocks/MockProofVerifier.cs`

### IPolicyProvider

Provides license policies for products.

**Responsibility**: Retrieves the license policy configuration for a given product ID.

**Method**:
```csharp
Task<LicensePolicy?> GetPolicyAsync(
    string productId, 
    CancellationToken cancellationToken = default)
```

**Output**:
- `LicensePolicy`: Defines required tier, features, binding mode, cache TTL, and revocation model

**Implementations**:
- `StubPolicyProvider`: Returns hardcoded example policies for testing (see `Mocks/StubPolicyProvider.cs`)
- `JsonPolicyProvider`: Loads policies from JSON files in a configured directory (production-ready)

### IValidationCache

Caches validation results to reduce expensive proof verifications.

**Responsibility**: Stores and retrieves validation results with TTL support.

**Methods**:
```csharp
Task<LicenseValidationResult?> GetAsync(
    string cacheKey, 
    CancellationToken cancellationToken = default)

Task SetAsync(
    string cacheKey, 
    LicenseValidationResult result, 
    TimeSpan ttl, 
    CancellationToken cancellationToken = default)

Task InvalidateAsync(
    string cacheKey, 
    CancellationToken cancellationToken = default)
```

**Implementations**:
- `InMemoryValidationCache` (thread-safe in-memory dictionary) - for development and single-instance deployments
- `FileValidationCache` (persistent file-based cache) - for production and containerized environments with durability across restarts

For detailed information on caching architecture, configuration, and Docker integration, see [Runtime Cache Documentation](./runtime-cache.md).

## Validation Flow

1. **Product ID Validation**: Verify that the proof's product ID matches the context's product ID
2. **Cache Lookup**: Check if a valid cached result exists
3. **Policy Retrieval**: Load the product's license policy
4. **Nonce Validation**: Verify the challenge hasn't expired and was issued in the past
5. **Proof Verification**: Cryptographically verify the ZK proof (expensive operation)
6. **Policy Enforcement**: Validate tier and feature requirements (pending Midnight proof format definition)
7. **Cache Storage**: Store the successful result with appropriate TTL
8. **Return Result**: Return validation result with status, errors, and metadata

## Directory Structure

```
src/validator/Chainborn.Licensing.Validator/
├── Mocks/                          # Mock/stub implementations for development
│   ├── MockProofVerifier.cs        # Stub proof verifier (always succeeds)
│   ├── InMemoryValidationCache.cs  # In-memory cache implementation
│   └── StubPolicyProvider.cs       # Hardcoded example policies
├── LicenseValidator.cs             # Main orchestration logic
├── LicenseValidationOptions.cs     # Configuration options
└── ServiceCollectionExtensions.cs  # DI registration

src/validator/Chainborn.Licensing.Validator.Tests/
├── LicenseValidatorTests.cs             # Integration tests for full flow
├── InMemoryValidationCacheTests.cs      # Unit tests for cache behavior
└── StubPolicyProviderTests.cs           # Unit tests for stub policies
```

## Dependency Injection

The library integrates with .NET's dependency injection through the `AddLicenseValidation` extension method:

```csharp
services.AddLicenseValidation(options =>
{
    options.PolicyDirectory = "/etc/chainborn/policies";
    options.CacheDirectory = "/var/chainborn/cache";
    options.DefaultStrictnessMode = StrictnessMode.Strict;
});
```

**Default Registrations**:
- `ILicenseValidator` → `LicenseValidator` (singleton)
- `IPolicyProvider` → `JsonPolicyProvider` (singleton)
- `IValidationCache` → `InMemoryValidationCache` (singleton, can be overridden)
- `IProofVerifier` → `MockProofVerifier` (singleton, can be overridden)

**Custom Implementations**:

You can override any interface by registering your own implementation before calling `AddLicenseValidation`:

```csharp
// Use Redis for caching
services.AddSingleton<IValidationCache, RedisValidationCache>();

// Use real ZK proof verifier
services.AddSingleton<IProofVerifier, MidnightProofVerifier>();

services.AddLicenseValidation(options => { /* ... */ });
```

## Plugging in Real Implementations

### Real ZK Proof Verification

**When**: After spike-003-zk-proof-dotnet is complete

**Steps**:
1. Create a new class implementing `IProofVerifier` (e.g., `MidnightProofVerifier`)
2. Place it in a new directory outside of `Mocks/` (e.g., `Verification/`)
3. Implement WASM interop or native binding to Midnight's proof verifier
4. Update `ServiceCollectionExtensions` to use the real verifier by default

### Real Cache Implementation

**When**: Production deployment requires persistence

**Steps**:
1. Create a new class implementing `IValidationCache` (e.g., `RedisValidationCache`, `FileSystemValidationCache`)
2. Place it outside of `Mocks/` directory
3. Implement cache eviction policies, memory limits, and persistence
4. Register it in DI to replace `InMemoryValidationCache`
5. Consider adding cache warming strategies for hot paths

## Security Considerations

- **Nonce Validation**: Always validate nonces before expensive proof verification to prevent replay attacks
- **Cache Keys**: Cache keys include product ID, nonce, binding data, and strictness mode to prevent collisions
- **Policy Loading**: JsonPolicyProvider validates product IDs to prevent path traversal attacks
- **Thread Safety**: InMemoryValidationCache uses `ConcurrentDictionary` for thread-safe operations

## Performance Considerations

- **Caching**: Validation results are cached with configurable TTL to reduce expensive proof verifications
- **Cache Key Design**: Cache keys are deterministic and include all factors that affect validation
- **Early Validation**: Nonce validation happens before proof verification to fail fast on expired challenges
- **Async/Await**: All operations are asynchronous to support high-concurrency scenarios

## Testing Strategy

- **Unit Tests**: Test individual components in isolation (cache, policy provider)
- **Integration Tests**: Test full validation flow with mocked dependencies
- **Cache Scenarios**: Test cache hits, misses, and expiration
- **Error Cases**: Test missing policies, expired nonces, invalid proofs
- **Product ID Mismatch**: Test validation fails when proof product ID doesn't match context

## Future Work

- [ ] Implement real ZK proof verification (blocked by spike-003-zk-proof-dotnet)
- [x] Implement persistent file-based caching solution (completed)
- [ ] Implement distributed cache solution (Redis)
- [ ] Add policy enforcement for tier and feature requirements (blocked by Midnight proof format)
- [ ] Add revocation checking (blocked by revocation model design)
- [ ] Add telemetry and metrics for validation operations
- [ ] Add rate limiting to prevent DoS attacks
- [ ] Implement cache warming strategies
- [ ] Add support for offline validation scenarios
