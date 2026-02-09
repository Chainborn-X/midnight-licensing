# Binding Mode Enforcement

## Overview

The Chainborn Licensing Platform enforces binding mode requirements to ensure licenses are valid only when correctly bound to the intended organization or environment. This document describes how binding mode enforcement works in the `LicenseValidator`.

## Purpose

Binding mode enforcement:
- **Prevents license misuse** by ensuring licenses are used only in authorized contexts
- **Validates binding data** against cryptographic proof public inputs
- **Enforces policy requirements** for organization-bound, environment-bound, or unbound licenses
- **Provides clear error messages** when binding validation fails

## How It Works

### Validation Flow

When `LicenseValidator.ValidateAsync()` is called, the following binding validation steps occur:

1. **Policy Check**: The validator retrieves the license policy to determine the `BindingMode`
2. **Binding Data Collection**: If binding mode is not `None`, binding data is automatically collected (if not already provided)
3. **Proof Verification**: The cryptographic proof is verified using the `IProofVerifier`
4. **Binding Validation**: The `IBindingComparator` validates binding data against proof public inputs according to the binding mode
5. **Result**: Validation succeeds only if all checks pass, including binding requirements

### Binding Modes

#### None

**Behavior**: No binding validation is performed

**Use Case**: 
- Permissive licenses
- Development/testing environments
- Open-source scenarios

**Validation Logic**:
```csharp
// Always returns valid, no checks performed
return new BindingValidationResult(true, Array.Empty<string>());
```

**Example Policy**:
```json
{
  "productId": "my-product",
  "bindingMode": "none",
  "cacheTtl": 86400,
  "version": "1.0.0"
}
```

#### Organization

**Behavior**: Validates that the license is bound to the correct organization

**Use Case**:
- B2B licenses sold per company/tenant
- Multi-tenant SaaS applications
- Enterprise deployments

**Required Fields**:
- `org_id` in binding data
- `org_id` in proof public inputs (when proof format is finalized)

**Validation Logic**:

When proof public inputs are **not available** (stub mode):
- Just verifies binding data was collected
- Logs a warning that validation is pending
- Returns valid (allows validation to proceed)

When proof public inputs **are available** (strict mode):
- Requires `org_id` in binding data
- Requires `org_id` in proof public inputs
- Compares `org_id` values (case-sensitive, exact match)
- Returns invalid if values don't match

**Example Policy**:
```json
{
  "productId": "my-product",
  "bindingMode": "organization",
  "cacheTtl": 43200,
  "version": "1.0.0"
}
```

**Example Validation**:
```csharp
// Binding data (collected or provided)
var bindingData = new Dictionary<string, string>
{
    ["org_id"] = "acme-corp"
};

// Proof public inputs (from ZK proof verification)
var publicInputs = new Dictionary<string, string>
{
    ["org_id"] = "acme-corp"
};

// Result: Valid (org_id matches)
```

#### Environment

**Behavior**: Validates that the license is bound to the correct environment

**Use Case**:
- Node-locked licenses
- Container-specific licensing
- Hardware-bound licenses
- Environment-specific deployments (prod vs. staging)

**Required Fields**:
- `environment_id` in binding data
- `environment_id` in proof public inputs (when proof format is finalized)

**Validation Logic**:

When proof public inputs are **not available** (stub mode):
- Just verifies binding data was collected
- Logs a warning that validation is pending
- Returns valid (allows validation to proceed)

When proof public inputs **are available** (strict mode):
- Requires `environment_id` in binding data
- Requires `environment_id` in proof public inputs
- Compares `environment_id` values (case-sensitive, exact match)
- Returns invalid if values don't match

**Example Policy**:
```json
{
  "productId": "my-product",
  "bindingMode": "environment",
  "cacheTtl": 3600,
  "version": "1.0.0"
}
```

**Example Validation**:
```csharp
// Binding data (collected or provided)
var bindingData = new Dictionary<string, string>
{
    ["environment_id"] = "prod-us-east-1"
};

// Proof public inputs (from ZK proof verification)
var publicInputs = new Dictionary<string, string>
{
    ["environment_id"] = "prod-us-east-1"
};

// Result: Valid (environment_id matches)
```

#### Attestation

**Behavior**: Reserved for future attestation-based validation (TPM, SGX, etc.)

**Current Status**: Not yet implemented. Returns valid with a warning.

**Use Case**:
- High-security scenarios
- TPM/SGX attestation
- Hardware-backed license binding

## Architecture

### IBindingComparator Interface

The `IBindingComparator` interface defines the contract for binding validation:

```csharp
public interface IBindingComparator
{
    BindingValidationResult Validate(
        BindingMode bindingMode,
        IReadOnlyDictionary<string, string>? bindingData,
        IReadOnlyDictionary<string, string>? publicInputs);
}

public record BindingValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);
```

### BindingComparator Implementation

The `BindingComparator` class implements the default binding validation logic:

- **Pluggable Design**: Can be replaced with custom implementation via DI
- **Mode-Specific Validation**: Delegates to mode-specific methods
- **Comprehensive Logging**: Logs debug, warning, and error messages
- **Clear Error Messages**: Returns actionable error messages for troubleshooting

### Integration with LicenseValidator

The `LicenseValidator` integrates binding validation into its validation pipeline:

```csharp
// Step 5: Validate binding mode requirements
var bindingValidationResult = _bindingComparator.Validate(
    policy.BindingMode,
    context.BindingData,
    verificationResult.PublicInputs
);

if (!bindingValidationResult.IsValid)
{
    _logger.LogWarning("Binding validation failed for product {ProductId}: {Errors}",
        context.ProductId, string.Join("; ", bindingValidationResult.Errors));
    return new LicenseValidationResult(
        IsValid: false,
        Errors: bindingValidationResult.Errors.ToArray(),
        ValidatedAt: now
    );
}
```

## Transition Plan: Stub to Real Validation

### Current State (Stub Mode)

**Scenario**: ZK proof format not finalized, public inputs not available

**Behavior**:
- Binding data is collected as usual
- Binding validation is performed, but is lenient
- When public inputs are null/empty, validation logs a warning and returns valid
- This allows the system to function while proof format is being finalized

**Rationale**:
- Allows development and testing of binding data collection
- Enables integration testing without real proofs
- Provides clear logging about what validation is pending

### Future State (Strict Mode)

**Scenario**: ZK proof format finalized, public inputs available

**Behavior**:
- Binding data is collected as usual
- Binding validation is performed strictly
- `org_id` or `environment_id` must exist in both binding data and public inputs
- Values must match exactly (case-sensitive)
- Validation fails if requirements are not met

**Migration Path**:
1. **Phase 1 (Current)**: Stub mode - lenient validation with warnings
2. **Phase 2**: Proof format finalized, public inputs populated by `IProofVerifier`
3. **Phase 3**: Strict validation automatically enforced (no code changes needed)

## Error Scenarios

### Organization Mode Errors

#### Missing Binding Data
```
Error: "Organization binding mode requires binding data, but none was provided"
Cause: Binding data is null or empty
Solution: Ensure IBindingDataCollector is registered and working, or provide binding data manually
```

#### Missing org_id in Binding Data
```
Error: "Organization binding mode requires 'org_id' in binding data"
Cause: Binding data doesn't contain 'org_id' field
Solution: Add CHAINBORN_BINDING_ORG_ID environment variable or include org_id in ValidationContext
```

#### Missing org_id in Proof
```
Error: "Proof does not contain required 'org_id' in public inputs"
Cause: Proof public inputs don't contain 'org_id' field
Solution: Ensure the license issuer includes org_id in the proof when generating it
```

#### Mismatched org_id
```
Error: "Organization ID mismatch: binding data has 'acme-corp' but proof has 'different-org'"
Cause: The org_id in binding data doesn't match the org_id in the proof
Solution: Use the correct proof for this organization, or update binding data
```

### Environment Mode Errors

#### Missing Binding Data
```
Error: "Environment binding mode requires binding data, but none was provided"
Cause: Binding data is null or empty
Solution: Ensure IBindingDataCollector is registered and working, or provide binding data manually
```

#### Missing environment_id in Binding Data
```
Error: "Environment binding mode requires 'environment_id' in binding data"
Cause: Binding data doesn't contain 'environment_id' field
Solution: Add CHAINBORN_BINDING_ENVIRONMENT_ID environment variable or include environment_id in ValidationContext
```

#### Missing environment_id in Proof
```
Error: "Proof does not contain required 'environment_id' in public inputs"
Cause: Proof public inputs don't contain 'environment_id' field
Solution: Ensure the license issuer includes environment_id in the proof when generating it
```

#### Mismatched environment_id
```
Error: "Environment ID mismatch: binding data has 'prod-us-east-1' but proof has 'dev-us-west-2'"
Cause: The environment_id in binding data doesn't match the environment_id in the proof
Solution: Use the correct proof for this environment, or deploy to the correct environment
```

## Configuration Examples

### Organization Binding with Custom org_id

**Dockerfile**:
```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY . .

ENV CHAINBORN_BINDING_ORG_ID=acme-corp

ENTRYPOINT ["dotnet", "MyApp.dll"]
```

**Policy**:
```json
{
  "productId": "my-product",
  "bindingMode": "organization",
  "cacheTtl": 43200,
  "version": "1.0.0"
}
```

### Environment Binding with Kubernetes

**Deployment YAML**:
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: my-app
spec:
  template:
    spec:
      containers:
      - name: my-app
        image: my-app:latest
        env:
        - name: CHAINBORN_BINDING_ENVIRONMENT_ID
          value: "prod-us-east-1"
```

**Policy**:
```json
{
  "productId": "my-product",
  "bindingMode": "environment",
  "cacheTtl": 3600,
  "version": "1.0.0"
}
```

### Manual Binding Data

**C# Code**:
```csharp
var bindingData = new Dictionary<string, string>
{
    ["org_id"] = GetOrganizationId(),
    ["environment_id"] = GetEnvironmentId()
};

var context = new ValidationContext(
    productId: "my-product",
    bindingData: bindingData
);

var result = await licenseValidator.ValidateAsync(proof, context);
```

## Testing

### Unit Tests

The `BindingComparatorTests` class provides comprehensive unit test coverage:

```bash
dotnet test --filter "FullyQualifiedName~BindingComparatorTests"
```

**Test Coverage**:
- BindingMode.None validation
- Organization mode with/without public inputs
- Organization mode error scenarios (missing fields, mismatched values)
- Environment mode with/without public inputs
- Environment mode error scenarios (missing fields, mismatched values)
- Attestation mode (stub)

### Integration Tests

The `BindingModeIntegrationTests` class tests end-to-end binding scenarios:

```bash
dotnet test --filter "FullyQualifiedName~BindingModeIntegrationTests"
```

**Test Coverage**:
- Binding data collection with different modes
- Manual vs. automatic binding data
- Environment variable integration
- Kubernetes metadata collection

### Adding Tests for Real Proofs

When real ZK proofs are available, add tests like:

```csharp
[Fact]
public async Task ValidateAsync_OrganizationMode_WithRealProof_ValidatesBinding()
{
    // Arrange
    var bindingData = new Dictionary<string, string> { ["org_id"] = "acme-corp" };
    var proof = GenerateRealProofForOrganization("acme-corp"); // Real proof
    var context = new ValidationContext("my-product", bindingData);
    
    // Act
    var result = await _validator.ValidateAsync(proof, context);
    
    // Assert
    Assert.True(result.IsValid);
}
```

## Extensibility

### Custom Binding Comparator

Implement custom binding logic by creating your own `IBindingComparator`:

```csharp
public class CustomBindingComparator : IBindingComparator
{
    public BindingValidationResult Validate(
        BindingMode bindingMode,
        IReadOnlyDictionary<string, string>? bindingData,
        IReadOnlyDictionary<string, string>? publicInputs)
    {
        // Custom validation logic
        // E.g., fuzzy matching, multiple org IDs, hierarchical validation
        
        var errors = new List<string>();
        
        // Your custom logic here
        
        return new BindingValidationResult(errors.Count == 0, errors);
    }
}
```

**Registration**:
```csharp
services.AddSingleton<IBindingComparator, CustomBindingComparator>();
```

### Wrapping Default Comparator

Extend the default comparator with additional logic:

```csharp
public class ExtendedBindingComparator : IBindingComparator
{
    private readonly BindingComparator _defaultComparator;
    
    public ExtendedBindingComparator(BindingComparator defaultComparator)
    {
        _defaultComparator = defaultComparator;
    }
    
    public BindingValidationResult Validate(
        BindingMode bindingMode,
        IReadOnlyDictionary<string, string>? bindingData,
        IReadOnlyDictionary<string, string>? publicInputs)
    {
        // Run default validation first
        var result = _defaultComparator.Validate(bindingMode, bindingData, publicInputs);
        
        if (!result.IsValid)
        {
            return result;
        }
        
        // Add additional custom validation
        var additionalErrors = new List<string>();
        
        // Your custom logic here
        
        if (additionalErrors.Count > 0)
        {
            return new BindingValidationResult(false, additionalErrors);
        }
        
        return result;
    }
}
```

## Known Limitations

1. **Stub Mode Required**: Until ZK proof format is finalized, validation operates in lenient stub mode
2. **Exact String Matching**: Current implementation uses case-sensitive exact string matching for org_id and environment_id
3. **Single Identifier**: Only supports one org_id or environment_id per proof (no multi-org licenses yet)
4. **No Wildcards**: No support for wildcard or pattern matching in binding validation
5. **Attestation Not Implemented**: Attestation binding mode is not yet implemented

## Troubleshooting

### Validation Passes But Shouldn't

**Symptom**: License validates successfully even though binding data doesn't match

**Cause**: Stub mode is active (public inputs are null/empty)

**Solution**: Wait for ZK proof format to be finalized and public inputs to be populated

### Validation Fails with "requires 'org_id'"

**Symptom**: Error message says org_id is required in binding data

**Cause**: Public inputs are available but binding data doesn't include org_id

**Solution**: 
- Add `CHAINBORN_BINDING_ORG_ID` environment variable
- Or provide org_id in `ValidationContext.BindingData`

### Validation Fails with "Organization ID mismatch"

**Symptom**: Proof has different org_id than binding data

**Cause**: Using a proof generated for a different organization

**Solution**:
- Obtain the correct proof for your organization
- Or update the binding data to match the proof's organization

## Related Documentation

- [Binding Data Collection](../binding-data-collection.md) - How binding data is collected
- [Policy Schema](../policy-schema.md) - Policy configuration details
- [Architecture](../architecture.md) - System architecture overview
- [Validator README](README.md) - Validator usage guide

## Support

For questions or issues:
- Open an issue on [GitHub](https://github.com/Chainborn-X/midnight-licensing/issues)
- Review test examples in `BindingComparatorTests.cs`
- Check [binding-data-collection.md](../binding-data-collection.md) for data collection details
