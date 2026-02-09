# License Policy Schema

## Purpose

The License Policy Schema is the foundational artifact that drives all licensing logic in the Chainborn Licensing Platform. It defines how licenses are validated, cached, bound, and revoked. The policy is a JSON configuration file that controls validator behavior without requiring code changes.

**Key Benefits:**
- **Declarative Configuration**: Define validation rules in JSON, not code
- **Version Control**: Track policy changes alongside code
- **Runtime Flexibility**: Update validation behavior by changing configuration
- **Schema Validation**: Catch configuration errors before deployment
- **Vendor Customization**: Extend with custom properties for product-specific needs

## Schema Location

- **Schema Definition**: `policies/schemas/license-policy.schema.json`
- **JSON Schema Version**: Draft-07
- **Schema ID**: `https://chainborn.io/schemas/license-policy.schema.json`

## Fields Reference

### Required Fields

#### `productId` (string, required)
Unique identifier for the product being licensed.

**Type**: `string`  
**Constraints**: Minimum length of 1 character  
**Example**: `"elsa-core"`, `"elsa-enterprise"`, `"elsa-cloud"`

**Usage Notes**:
- Must match the `productId` embedded in the license proof
- Used to prevent proof reuse across different products
- Should be consistent across all policy versions for the same product

---

#### `version` (string, required)
Semantic version of the policy schema.

**Type**: `string`  
**Format**: Semantic versioning (e.g., `"1.0.0"`, `"2.1.3"`)  
**Pattern**: `^\d+\.\d+\.\d+$`

**Usage Notes**:
- Follows semantic versioning (MAJOR.MINOR.PATCH)
- Increment MAJOR for breaking changes
- Increment MINOR for backward-compatible additions
- Increment PATCH for backward-compatible fixes
- Used for policy evolution and compatibility checks

---

#### `bindingMode` (string enum, required)
Defines how the license is bound to prevent unauthorized sharing.

**Type**: `string`  
**Valid Values**:
- `"none"` — No binding. License can be used anywhere.
- `"organization"` — License is bound to an organization identifier (e.g., company domain, tenant ID)
- `"environment"` — License is bound to a specific environment (e.g., machine fingerprint, container ID)

**Usage Notes**:
- **`none`**: Suitable for open-source or permissive licenses where sharing is acceptable
- **`organization`**: Prevents license sharing outside the licensed company/tenant. Validator checks that the runtime organization ID matches the proof's organization binding.
- **`environment`**: Prevents license sharing across machines or containers. Validator checks that the runtime environment fingerprint matches the proof's environment binding.
- Binding enforcement is performed during validation by comparing runtime context (from `IBindingProvider`) with proof metadata

---

#### `cacheTtl` (integer, required)
Cache time-to-live in seconds for successful validation results.

**Type**: `integer`  
**Constraints**: 
- Minimum: `60` (1 minute)
- Maximum: `604800` (7 days)

**Example**: `86400` (24 hours), `3600` (1 hour)

**Usage Notes**:
- Successful validations are cached to avoid repeated proof verification overhead
- After TTL expires, proof must be re-verified
- Balances performance (longer TTL) vs. revocation responsiveness (shorter TTL)
- Should be shorter than typical proof expiry times

**Interaction with Proof Expiry**:

The validator enforces a critical **cache TTL invariant**: cached validation results expire at the **minimum** of:
1. Proof challenge expiry: `challenge.expiresAt`
2. Cache TTL bound: `validatedAt + policy.cacheTtl`

This ensures that:
- **Short-lived proofs** are never cached longer than they're valid
- **Long-lived proofs** still get re-validated periodically per policy

**Example 1**: Proof expires in 10 minutes, `cacheTtl` is 30 minutes → cached result expires in 10 minutes  
**Example 2**: Proof expires in 2 hours, `cacheTtl` is 15 minutes → cached result expires in 15 minutes

See [Runtime Cache Architecture](runtime-cache.md) for detailed cache behavior.

**Revocation Model Considerations**:
- `revocationModel: "none"` → Can use longer TTL (e.g., 24 hours)
- `revocationModel: "on-chain"` → Should use shorter TTL (e.g., 1 hour)
- `revocationModel: "periodic-check"` → Moderate TTL (e.g., 12 hours)

---

#### `revocationModel` (string enum, required)
Defines how license revocation is handled.

**Type**: `string`  
**Valid Values**:
- `"none"` — No revocation checks. Once issued, licenses are valid until expiry.
- `"on-chain"` — Real-time revocation via on-chain oracle or contract query.
- `"periodic-check"` — Periodic revocation list download and validation.

**Usage Notes**:
- **`none`**: Simplest model. No revocation infrastructure needed. Suitable for perpetual licenses or air-gapped deployments.
- **`on-chain`**: Requires network connectivity to query blockchain revocation state. Validator checks revocation before accepting proof. Most secure but requires online access.
- **`periodic-check`**: Validator periodically fetches a revocation list (CRL-style). Balances offline operation with revocation support. Requires background sync mechanism.
- Revocation model affects cache TTL strategy and grace period configuration

---

### Optional Fields

#### `requiredTier` (string enum, optional)
Minimum license tier required to satisfy the policy.

**Type**: `string`  
**Valid Values**: `"community"`, `"professional"`, `"enterprise"`

**Usage Notes**:
- If specified, the proof must contain a tier equal to or higher than this value
- Tier hierarchy: `community` < `professional` < `enterprise`
- Omit this field if the product doesn't use tiered licensing
- Validator rejects proofs with insufficient tier level

---

#### `requiredFeatures` (array of strings, optional)
List of feature flags that must be present in the license proof.

**Type**: `array` of `string`  
**Default**: `[]` (empty array)  
**Constraints**: Unique items, each item minimum length of 1

**Example**: `["advanced-reporting", "multi-tenant", "api-access"]`

**Usage Notes**:
- All specified features must be present in the proof's feature list
- Feature names are case-sensitive
- Supports fine-grained feature gating within a product
- Empty array or omitted field means no feature requirements
- Used by application code to conditionally enable functionality

---

#### `gracePeriod` (integer, optional)
Grace period in seconds after validation failure before enforcing a hard block.

**Type**: `integer`  
**Constraints**: Minimum value of `0`  
**Default**: `0` (no grace period)

**Example**: `86400` (24 hours grace), `604800` (7 days grace)

**Usage Notes**:
- Allows temporary operation after proof validation fails
- Useful for handling temporary network outages or renewal delays
- Application continues to operate but may log warnings
- After grace period expires, validator returns hard failure
- Use conservatively to avoid encouraging non-compliance
- Not applicable if validation never succeeded (cold start)

---

#### `customProperties` (object, optional)
Extensible key-value pairs for vendor-specific metadata.

**Type**: `object`  
**Constraints**: Allows any additional properties

**Example**:
```json
{
  "customProperties": {
    "billingCycle": "annual",
    "salesforceOpportunityId": "006XXXXXXXX",
    "supportLevel": "premium",
    "customerId": "cust_12345"
  }
}
```

**Usage Notes**:
- Allows vendors to store product-specific configuration
- Not validated by core validator logic
- Can be used by custom policy providers or application code
- Useful for integration with CRM, billing, or support systems
- Keep sensitive data out of policies (they may be committed to version control)

---

## How Policy Drives Validator Behavior

The `LicenseValidator` consumes the policy at runtime to enforce validation rules:

1. **Load Policy**: Policy is loaded from JSON file via `IPolicyProvider`
2. **Proof Verification**: Call `IProofVerifier.VerifyAsync()` with proof bytes
3. **Product ID Match**: Verify `proof.productId == policy.productId`
4. **Tier Validation**: If `policy.requiredTier` is set, check `proof.tier >= policy.requiredTier`
5. **Feature Validation**: If `policy.requiredFeatures` is set, check all required features are present in `proof.features`
6. **Binding Validation**: If `policy.bindingMode != "none"`, validate runtime binding context matches proof binding
7. **Revocation Check**: Based on `policy.revocationModel`, check if license is revoked
8. **Caching**: Cache successful result with `policy.cacheTtl`

---

## Binding Mode Deep Dive

### `bindingMode: "none"`
- **Use Case**: Open-source, permissive, or development licenses
- **Behavior**: No runtime binding validation
- **Proof Requirements**: No binding metadata needed in proof
- **Risk**: License can be shared freely

### `bindingMode: "organization"`
- **Use Case**: B2B enterprise licenses sold per company/tenant
- **Behavior**: Validator calls `IBindingProvider.GetOrganizationIdAsync()` and checks it matches `proof.organizationId`
- **Proof Requirements**: Proof must include `organizationId` (e.g., company domain, tenant GUID)
- **Risk**: If organization ID is guessable, attackers could forge binding. Use strong, hard-to-predict identifiers.

### `bindingMode: "environment"`
- **Use Case**: Node-locked licenses, container-specific licensing
- **Behavior**: Validator calls `IBindingProvider.GetEnvironmentFingerprintAsync()` and checks it matches `proof.environmentFingerprint`
- **Proof Requirements**: Proof must include `environmentFingerprint` (e.g., hardware ID, container hash)
- **Risk**: Environment changes (hardware replacement, container rebuild) invalidate license. Consider grace period.

---

## Cache TTL and Proof Expiry Interaction

### Key Principles
- **Proof Expiry**: Proofs contain an `expiresAt` timestamp set by the proof generator
- **Cache TTL**: Policy-defined duration for caching validation results
- **Rule**: Effective expiry is `min(proof.expiresAt, now + policy.cacheTtl)`

### Example Scenarios

**Scenario 1: Long-lived proof, short cache TTL**
- Proof valid for 30 days
- `cacheTtl: 3600` (1 hour)
- **Result**: Validator re-checks proof every hour (good for on-chain revocation model)

**Scenario 2: Short-lived proof, long cache TTL**
- Proof valid for 1 hour
- `cacheTtl: 86400` (24 hours)
- **Result**: Validator re-checks when proof expires (after 1 hour)

**Scenario 3: Balanced**
- Proof valid for 7 days
- `cacheTtl: 43200` (12 hours)
- **Result**: Validator re-checks every 12 hours (good for periodic revocation checks)

### Best Practices
- Set `cacheTtl` shorter than typical proof expiry
- Align cache TTL with revocation model responsiveness needs
- For air-gapped deployments, use longer proof validity and longer cache TTL
- For cloud/SaaS, use shorter proof validity and shorter cache TTL

---

## Revocation Model Comparison

| Model | Network Required | Responsiveness | Complexity | Use Case |
|-------|------------------|----------------|------------|----------|
| `none` | No | N/A | Lowest | Perpetual licenses, air-gapped, trusted customers |
| `on-chain` | Yes (always) | Real-time | High | SaaS, high-value licenses, subscription |
| `periodic-check` | Yes (background) | Minutes to hours | Medium | Hybrid cloud, offline-tolerant |

### `none`
- **Infrastructure**: None required
- **Validator Behavior**: Skip revocation checks entirely
- **Recommendation**: Set longer `cacheTtl` (e.g., 24 hours)

### `on-chain`
- **Infrastructure**: Blockchain node access, revocation contract or oracle
- **Validator Behavior**: Query revocation status before accepting proof (may cache negative results)
- **Recommendation**: Set shorter `cacheTtl` (e.g., 1 hour) for faster revocation enforcement

### `periodic-check`
- **Infrastructure**: Background service to fetch revocation list (CRL), shared cache or database
- **Validator Behavior**: Check local revocation list before accepting proof
- **Recommendation**: Set moderate `cacheTtl` (e.g., 12 hours) aligned with revocation list refresh frequency

---

## Example Policies

### Example 1: Single-Product License (Simple)

**File**: `policies/single-product.json`

```json
{
  "$schema": "schemas/license-policy.schema.json",
  "productId": "elsa-core",
  "version": "1.0.0",
  "bindingMode": "none",
  "cacheTtl": 86400,
  "revocationModel": "none"
}
```

**Explanation**:
- **Product**: `elsa-core` (basic workflow engine)
- **Tiers**: Not used (no `requiredTier`)
- **Features**: Not used (no `requiredFeatures`)
- **Binding**: None — license can be used anywhere
- **Cache**: 24 hours (long TTL since no revocation checks)
- **Revocation**: None — once issued, valid until expiry
- **Use Case**: Open-source adjacent product, permissive licensing, trusted customers

---

### Example 2: Enterprise Tiered License

**File**: `policies/tiered.json`

```json
{
  "$schema": "schemas/license-policy.schema.json",
  "productId": "elsa-enterprise",
  "version": "1.0.0",
  "requiredTier": "enterprise",
  "requiredFeatures": ["advanced-reporting", "multi-tenant"],
  "bindingMode": "organization",
  "cacheTtl": 43200,
  "revocationModel": "periodic-check"
}
```

**Explanation**:
- **Product**: `elsa-enterprise` (enterprise edition)
- **Tiers**: Requires `enterprise` tier (community/professional proofs rejected)
- **Features**: Requires both `advanced-reporting` and `multi-tenant` features
- **Binding**: Organization — license tied to company/tenant ID
- **Cache**: 12 hours (moderate TTL for balance)
- **Revocation**: Periodic check — background process fetches revocation list
- **Use Case**: B2B enterprise sales, multi-tenant SaaS, feature-gated product

---

### Example 3: Subscription-Based License

**File**: `policies/subscription.json`

```json
{
  "$schema": "schemas/license-policy.schema.json",
  "productId": "elsa-cloud",
  "version": "1.0.0",
  "requiredTier": "professional",
  "requiredFeatures": ["cloud-sync"],
  "bindingMode": "environment",
  "cacheTtl": 3600,
  "revocationModel": "on-chain"
}
```

**Explanation**:
- **Product**: `elsa-cloud` (cloud-native version)
- **Tiers**: Requires `professional` tier (community proofs rejected)
- **Features**: Requires `cloud-sync` feature
- **Binding**: Environment — license tied to specific container/VM
- **Cache**: 1 hour (short TTL for fast revocation enforcement)
- **Revocation**: On-chain — real-time revocation checks via blockchain
- **Use Case**: Cloud/SaaS subscription, container-based deployment, pay-as-you-go

---

## Schema Versioning Strategy

### Versioning Principles
1. **Schema Version** (`$schema` in JSON files): Points to the JSON Schema definition version
2. **Policy Version** (`version` field): Tracks the policy content version using semver

### Version Evolution

#### Adding Optional Fields (MINOR version bump)
- Add new optional field to schema
- Update policy `version` from `1.0.0` to `1.1.0`
- Old validators ignore unknown fields (if `additionalProperties: false` is relaxed)
- New validators support new field

#### Changing Field Constraints (MAJOR version bump)
- Change enum values (e.g., add new `bindingMode`)
- Change validation rules (e.g., increase `cacheTtl` minimum)
- Update policy `version` from `1.0.0` to `2.0.0`
- Old validators may reject new policies
- Coordinate validator and policy updates

#### Bug Fixes (PATCH version bump)
- Fix typos in documentation
- Clarify field descriptions
- Update policy `version` from `1.0.0` to `1.0.1`
- No behavior changes

### Compatibility Matrix

| Policy Version | Validator Version | Compatible? |
|----------------|-------------------|-------------|
| 1.0.0 | 1.0.0 | ✅ Yes |
| 1.1.0 | 1.0.0 | ⚠️ Partial (new fields ignored) |
| 2.0.0 | 1.0.0 | ❌ No (breaking changes) |
| 1.0.0 | 1.1.0 | ✅ Yes (backward compatible) |

### Migration Strategy
1. Deploy new validator version
2. Test with old and new policy versions
3. Update policy versions incrementally
4. Monitor validation logs for errors
5. Roll back if compatibility issues arise

---

## Validation

### Using AJV (recommended)

Install AJV CLI:
```bash
npm install -g ajv-cli
```

Validate a single policy:
```bash
npx ajv validate -s policies/schemas/license-policy.schema.json -d policies/single-product.json
```

Validate all policies:
```bash
npx ajv validate -s policies/schemas/license-policy.schema.json -d "policies/*.json"
```

### Using Online Validators

1. Open [JSON Schema Validator](https://www.jsonschemavalidator.net/)
2. Paste schema from `policies/schemas/license-policy.schema.json` into left pane
3. Paste policy from `policies/single-product.json` (or other) into right pane
4. Check for validation errors

### Validation in CI/CD

Add to GitHub Actions or GitLab CI:
```yaml
- name: Validate License Policies
  run: |
    npm install -g ajv-cli
    npx ajv validate -s policies/schemas/license-policy.schema.json -d "policies/*.json"
```

### Common Validation Errors

| Error | Cause | Fix |
|-------|-------|-----|
| `missing required property` | Required field not provided | Add missing field |
| `must be equal to one of enum values` | Invalid enum value (e.g., `"None"` vs `"none"`) | Use exact enum value from schema |
| `must be >= 60` | `cacheTtl` too low | Increase to at least 60 seconds |
| `must match pattern` | Invalid `version` format | Use semver (e.g., `"1.0.0"`) |
| `must be integer` | Used string for `cacheTtl` | Use integer without quotes |

---

## Integration with Validator

### Policy Loading

The validator loads policies via `IPolicyProvider`:
```csharp
public interface IPolicyProvider
{
    Task<LicensePolicy> GetPolicyAsync(string productId, CancellationToken cancellationToken);
}
```

### File-Based Provider (default)
- Reads policies from `policies/` directory
- Matches `productId` to filename (e.g., `elsa-core` → `policies/elsa-core.json`)
- Caches parsed policies in memory
- Validates against schema on load

### Custom Providers
- Database-backed policies
- HTTP API policies
- Encrypted/signed policies
- Multi-tenancy (per-tenant policies)

---

## Best Practices

1. **Start Simple**: Begin with `bindingMode: "none"` and `revocationModel: "none"` for initial deployments
2. **Version Control**: Store policies in Git alongside code
3. **Schema Validation**: Always validate policies in CI/CD before deployment
4. **Security Review**: Review policies before production (especially `customProperties`)
5. **Document Changes**: Use commit messages to explain policy changes
6. **Test Coverage**: Write unit tests for policy validation logic
7. **Monitor Failures**: Log validation failures for troubleshooting
8. **Gradual Rollout**: Test new policies in staging before production
9. **Cache Tuning**: Monitor cache hit rates and adjust `cacheTtl` accordingly
10. **Revocation Planning**: Plan revocation infrastructure before using `on-chain` or `periodic-check`

---

## Future Enhancements

Potential future additions to the schema:
- **Multi-product policies**: Support for product families/bundles
- **Usage limits**: Enforce API rate limits or seat counts
- **Expiry warnings**: Configure warning thresholds before expiry
- **Attestation modes**: TPM/SGX-based binding for high-security scenarios
- **Hierarchical policies**: Policy inheritance for product variants
- **Conditional features**: Feature flags based on runtime conditions

---

## Related Documentation

- [Architecture](architecture.md) — System architecture and design decisions
- [Product Requirements](prs.md) — Full product requirements specification
- [README](../README.md) — Getting started guide

---

## Support

For questions or issues with policy configuration:
- Open an issue on [GitHub](https://github.com/Chainborn-X/midnight-licensing/issues)
- Review [architecture.md](architecture.md) for validator behavior details
- Check [prs.md](prs.md) for original requirements
