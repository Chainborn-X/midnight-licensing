# Spike #1: License Policy Schema and Validation Framework

**Status:** Complete  
**Issue:** #1  
**Date:** 2026-02-07

---

## Problem Statement

Design a flexible, product-agnostic policy schema that defines license validation rules without requiring code changes. The schema must support multiple products, tiers, features, binding modes, and compliance constraints while remaining simple enough for non-developers to configure.

---

## Findings

### Requirements from PRS

From the Product Requirements Specification, the policy framework must support:

1. **Product identification** - Multiple products in one platform
2. **Tier-based licensing** - Starter, Professional, Enterprise, etc.
3. **Feature flags** - Granular capability control
4. **Validity windows** - Time-based expiration
5. **Revocation and renewal** - Subscription model support
6. **Binding modes** - Machine-specific, transferable, floating
7. **Seat count enforcement** - Concurrent user limits
8. **Compliance constraints** - Enterprise requirements

### Configuration-Driven Validation

**Core principle:** New products should onboard without code changes.

**Approach:** JSON-based policy files that define:
- What constitutes a valid license for this product
- Which proof public outputs are required
- How to interpret tier levels
- Feature ID mappings
- TTL and caching behavior

### Policy Storage and Distribution

**Options evaluated:**

1. **Embedded in application** (Recommended for M1)
   - Policy JSON files bundled in Docker image
   - Loaded at startup from `/app/policies/`
   - Simple, no external dependencies
   - Requires redeployment to update policies

2. **Environment variable** (Good for testing)
   - Entire policy as JSON in ENV var
   - Useful for CI/CD and dynamic configuration
   - Limited by ENV var size constraints

3. **External configuration service** (Future: M4+)
   - Fetch policy from API at startup
   - Enables dynamic updates without redeployment
   - Adds network dependency (defeats offline goal for initial load)
   - Could cache locally after first fetch

**Recommendation:** Start with embedded JSON files, add ENV var override for testing.

---

## Recommended Policy Schema

### Example Policy JSON

```json
{
  "version": "1.0",
  "product": {
    "id": "myapp-pro",
    "name": "MyApp Professional",
    "vendor": "Acme Corp"
  },
  "contract": {
    "address": "0x1234567890abcdef...",
    "network": "midnight-testnet",
    "verificationKeyPath": "/app/vkeys/myapp-pro-vk.bin"
  },
  "validation": {
    "requiredProofType": "validate_license",
    "publicInputs": {
      "productId": "myapp-pro",
      "minimumTier": 2
    },
    "allowedBindingModes": ["machine", "floating"],
    "ttl": "1h",
    "cacheEnabled": true
  },
  "tiers": {
    "1": {
      "name": "Starter",
      "features": ["core", "api"],
      "seats": 1
    },
    "2": {
      "name": "Professional",
      "features": ["core", "api", "advanced"],
      "seats": 5
    },
    "3": {
      "name": "Enterprise",
      "features": ["core", "api", "advanced", "sso", "audit"],
      "seats": 100
    }
  },
  "features": {
    "core": {
      "id": 1,
      "name": "Core Features",
      "description": "Basic application functionality"
    },
    "api": {
      "id": 2,
      "name": "API Access",
      "description": "REST API and integrations"
    },
    "advanced": {
      "id": 3,
      "name": "Advanced Tools",
      "description": "Advanced analytics and reporting"
    },
    "sso": {
      "id": 4,
      "name": "Single Sign-On",
      "description": "SAML/OAuth SSO integration"
    },
    "audit": {
      "id": 5,
      "name": "Audit Logging",
      "description": "Compliance audit trail"
    }
  },
  "enforcement": {
    "strictMode": true,
    "gracePeriodDays": 7,
    "revocationCheckInterval": "24h"
  }
}
```

### Schema Sections Explained

#### 1. Product Identity
```json
"product": {
  "id": "myapp-pro",
  "name": "MyApp Professional",
  "vendor": "Acme Corp"
}
```
Identifies which product this policy governs. The `id` must match the `product_id` claim in generated proofs.

#### 2. Contract Configuration
```json
"contract": {
  "address": "0x1234...",
  "network": "midnight-testnet",
  "verificationKeyPath": "/app/vkeys/myapp-pro-vk.bin"
}
```
Points to the Midnight smart contract and verification key. Allows deploying new contract versions without code changes.

#### 3. Validation Rules
```json
"validation": {
  "requiredProofType": "validate_license",
  "publicInputs": {
    "productId": "myapp-pro",
    "minimumTier": 2
  },
  "allowedBindingModes": ["machine", "floating"],
  "ttl": "1h",
  "cacheEnabled": true
}
```
Defines:
- Which circuit function was used to generate the proof
- Expected public outputs from the proof
- Binding mode constraints
- Cache behavior and TTL

#### 4. Tier Definitions
```json
"tiers": {
  "2": {
    "name": "Professional",
    "features": ["core", "api", "advanced"],
    "seats": 5
  }
}
```
Maps tier numbers (from proof) to human-readable names, feature sets, and seat counts.

#### 5. Feature Catalog
```json
"features": {
  "api": {
    "id": 2,
    "name": "API Access",
    "description": "REST API and integrations"
  }
}
```
Defines available features with IDs matching the smart contract's feature encoding.

#### 6. Enforcement Policies
```json
"enforcement": {
  "strictMode": true,
  "gracePeriodDays": 7,
  "revocationCheckInterval": "24h"
}
```
Controls validation strictness and grace periods for expired licenses.

---

## Validation Flow Using Policy

```
1. Application starts
   ↓
2. Load policy JSON from /app/policies/{product}.json
   ↓
3. Customer provides license proof (ENV var or file)
   ↓
4. ILicenseValidator.ValidateAsync() called
   ↓
5. Check cache (if enabled and within TTL)
   ↓
6. If not cached:
   a. Load verification key from policy.contract.verificationKeyPath
   b. Call IProofVerifier.VerifyAsync() → THE BRIDGE POINT
   c. Parse public inputs from proof
   d. Validate public inputs match policy.validation.publicInputs
   e. Check tier ≥ policy.validation.minimumTier
   f. Validate nonce freshness (anti-replay)
   g. Check binding mode in policy.validation.allowedBindingModes
   h. Cache result with policy.validation.ttl
   ↓
7. Return LicenseValidationResult (valid/invalid + features)
   ↓
8. Application uses result.Features to enable/disable functionality
```

---

## .NET Implementation Design

### IPolicyProvider Interface

```csharp
public interface IPolicyProvider
{
    Task<ProductPolicy> GetPolicyAsync(
        string productId, 
        CancellationToken cancellationToken = default);
}
```

### Policy Model Classes

```csharp
public class ProductPolicy
{
    public string Version { get; set; }
    public ProductInfo Product { get; set; }
    public ContractInfo Contract { get; set; }
    public ValidationRules Validation { get; set; }
    public Dictionary<int, TierInfo> Tiers { get; set; }
    public Dictionary<string, FeatureInfo> Features { get; set; }
    public EnforcementPolicy Enforcement { get; set; }
}

public class ProductInfo
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Vendor { get; set; }
}

public class ContractInfo
{
    public string Address { get; set; }
    public string Network { get; set; }
    public string VerificationKeyPath { get; set; }
}

public class ValidationRules
{
    public string RequiredProofType { get; set; }
    public Dictionary<string, object> PublicInputs { get; set; }
    public string[] AllowedBindingModes { get; set; }
    public TimeSpan Ttl { get; set; }
    public bool CacheEnabled { get; set; }
}

public class TierInfo
{
    public string Name { get; set; }
    public string[] Features { get; set; }
    public int Seats { get; set; }
}

public class FeatureInfo
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Description { get; set; }
}

public class EnforcementPolicy
{
    public bool StrictMode { get; set; }
    public int GracePeriodDays { get; set; }
    public TimeSpan RevocationCheckInterval { get; set; }
}
```

### File-Based Policy Provider

```csharp
public class FilePolicyProvider : IPolicyProvider
{
    private readonly string _policyDirectory;
    private readonly ILogger<FilePolicyProvider> _logger;
    private readonly ConcurrentDictionary<string, ProductPolicy> _cache;

    public FilePolicyProvider(
        string policyDirectory, 
        ILogger<FilePolicyProvider> logger)
    {
        _policyDirectory = policyDirectory;
        _logger = logger;
        _cache = new ConcurrentDictionary<string, ProductPolicy>();
    }

    public async Task<ProductPolicy> GetPolicyAsync(
        string productId, 
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(productId, out var cached))
        {
            return cached;
        }

        var policyPath = Path.Combine(_policyDirectory, $"{productId}.json");
        if (!File.Exists(policyPath))
        {
            throw new PolicyNotFoundException(productId);
        }

        var json = await File.ReadAllTextAsync(policyPath, cancellationToken);
        var policy = JsonSerializer.Deserialize<ProductPolicy>(json);
        
        _cache.TryAdd(productId, policy);
        return policy;
    }
}
```

### Environment Variable Override

```csharp
public class ConfigurablePolicyProvider : IPolicyProvider
{
    private readonly FilePolicyProvider _fileProvider;
    private readonly IConfiguration _configuration;

    public async Task<ProductPolicy> GetPolicyAsync(
        string productId, 
        CancellationToken cancellationToken = default)
    {
        // Check for ENV var override first (useful for testing)
        var envVarName = $"LICENSE_POLICY_{productId.ToUpperInvariant()}";
        var policyJson = _configuration[envVarName];
        
        if (!string.IsNullOrEmpty(policyJson))
        {
            return JsonSerializer.Deserialize<ProductPolicy>(policyJson);
        }

        // Fall back to file-based policy
        return await _fileProvider.GetPolicyAsync(productId, cancellationToken);
    }
}
```

---

## Recommendations

### 1. Start with File-Based Policies

**For M1 and M2:**
- Embed policy JSON files in Docker image at `/app/policies/`
- Simple, no external dependencies
- Easy to version control alongside code
- Policies are immutable per deployment (good for reproducibility)

### 2. Support ENV Variable Override

**For testing and CI/CD:**
```bash
docker run \
  -e LICENSE_POLICY_MYAPP='{"version":"1.0",...}' \
  myapp:latest
```

Enables dynamic policy injection without rebuilding images.

### 3. Validate Policy Schema at Startup

**Fail fast if policy is invalid:**
```csharp
public class PolicyValidator
{
    public void Validate(ProductPolicy policy)
    {
        if (string.IsNullOrEmpty(policy.Product.Id))
            throw new InvalidPolicyException("Product ID is required");
        
        if (policy.Tiers == null || policy.Tiers.Count == 0)
            throw new InvalidPolicyException("At least one tier must be defined");
        
        // ... more validation
    }
}
```

Better to crash at startup than fail at runtime.

### 4. Version Policy Schema

Include `"version": "1.0"` in policy JSON to support future schema evolution:
```csharp
if (policy.Version != "1.0")
{
    throw new UnsupportedPolicyVersionException(policy.Version);
}
```

### 5. Document Policy Creation

Provide a **policy authoring guide** with:
- Schema reference documentation
- Example policies for common scenarios
- Validation tool (CLI) to check policy correctness
- Template generator for new products

---

## Open Questions

1. **Policy versioning and migration:** How do we update policies in running containers?
   - Current answer: Redeploy (policies are immutable per deployment)
   - Future: Consider reload-on-signal or external config service

2. **Policy testing:** How do vendors validate policies before deployment?
   - Need policy linter/validator tool
   - Need test harness that simulates validation with sample proofs

3. **Multi-product deployments:** How to handle applications with multiple product licenses?
   - Option A: One policy per product, load dynamically
   - Option B: Composite policy with multiple product sections
   - Recommend Option A for simplicity

4. **Policy size limits:** Are there practical limits on policy JSON size?
   - Large feature catalogs could bloat policy files
   - May need external feature database reference

5. **Security:** Should policy files be signed to prevent tampering?
   - Paranoid mode: Include SHA-256 hash of expected policy
   - Mitigated by container image integrity (image signing)

---

## Example Use Cases

### Use Case 1: SaaS Application with 3 Tiers

```json
{
  "product": {"id": "cloudapp"},
  "tiers": {
    "1": {"name": "Basic", "seats": 1, "features": ["core"]},
    "2": {"name": "Pro", "seats": 10, "features": ["core", "api"]},
    "3": {"name": "Enterprise", "seats": -1, "features": ["core", "api", "sso", "audit"]}
  },
  "validation": {
    "minimumTier": 1,
    "ttl": "1h",
    "cacheEnabled": true
  }
}
```

### Use Case 2: Desktop Application with Floating Licenses

```json
{
  "product": {"id": "desktop-tool"},
  "validation": {
    "allowedBindingModes": ["floating"],
    "ttl": "5m",
    "cacheEnabled": true
  },
  "enforcement": {
    "strictMode": false,
    "gracePeriodDays": 14
  }
}
```

### Use Case 3: Embedded System with Machine Binding

```json
{
  "product": {"id": "iot-firmware"},
  "validation": {
    "allowedBindingModes": ["machine"],
    "ttl": "24h",
    "cacheEnabled": true
  },
  "enforcement": {
    "strictMode": true,
    "gracePeriodDays": 0
  }
}
```

---

## Next Steps

1. **M1:** Define formal JSON schema for policy format (JSON Schema or C# models)
2. **M1:** Implement `IPolicyProvider` interface and file-based provider
3. **M1:** Create example policy files for 2-3 reference products
4. **M2:** Build policy validator CLI tool
5. **M2:** Integrate policy loading into `LicenseValidator`
6. **M3:** Add policy documentation generator (schema → markdown)
7. **M4:** Consider external configuration service for dynamic updates

---

## References

- [PRS Document](../prs.md) - Product requirements and license attributes
- [Architecture Document](../architecture.md) - Bridge point and validation flow
- [Spike #2: Wallet Interaction](spike-002-wallet-interaction.md) - Proof generation side
- [Spike #3: ZK Proof .NET Verification](spike-003-zk-proof-dotnet.md) - Verification interface
- [Spike #4: Compact Contracts](spike-004-compact-contracts.md) - Contract-side data model
