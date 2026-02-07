# Test Fixtures

This directory contains test fixtures for the Midnight license proof system.

## Proof Envelope Fixtures

### `valid-proof-envelope.json`

A complete, valid proof envelope that demonstrates the canonical JSON format. This fixture:

- ✅ Passes JSON schema validation
- ✅ Contains a challenge that expires in the future (relative to its creation date)
- ✅ Includes all required fields with proper base64 encoding
- ✅ Demonstrates the use of optional metadata fields

**Use cases:**
- Integration testing of proof loading
- Documentation examples
- Schema validation tests
- Development and debugging

### `expired-proof-envelope.json`

A proof envelope with an expired challenge (`expiresAt` is in the past). This fixture:

- ✅ Passes JSON schema validation (schema doesn't validate timestamps)
- ⚠️ Has an expired challenge (expired on 2026-01-02)
- ✅ Useful for testing expiration logic in the validator

**Use cases:**
- Testing proof expiration validation
- Testing error handling for expired proofs
- Demonstrating challenge TTL behavior

## Validating Fixtures

### Automated Validation

Run the validation script to check all fixtures against the JSON schema:

```bash
# From the repository root
./tests/validate-fixtures.sh
```

### Manual Validation

You can also validate fixtures manually using `ajv-cli`:

```bash
# Install ajv-cli (if not already installed)
npm install -g ajv-cli ajv-formats

# Validate a specific fixture
ajv validate \
  -s policies/schemas/proof-envelope.schema.json \
  -d tests/fixtures/valid-proof-envelope.json \
  --spec=draft7 \
  -c ajv-formats
```

## Creating New Fixtures

When creating new test fixtures:

1. **Follow the schema**: Ensure all required fields are present
2. **Use valid base64**: All `*Bytes` fields must be valid base64-encoded strings
3. **Use ISO 8601 dates**: All timestamp fields must use ISO 8601 format with timezone
4. **Validate before committing**: Run `./tests/validate-fixtures.sh` to ensure schema compliance
5. **Document the purpose**: Add a description to this README

### Example Template

```json
{
  "$schema": "../../../policies/schemas/proof-envelope.schema.json",
  "version": "1.0.0",
  "proofBytes": "<base64-encoded-proof>",
  "verificationKeyBytes": "<base64-encoded-vk>",
  "productId": "your-product-id",
  "challenge": {
    "nonce": "<base64-encoded-nonce>",
    "issuedAt": "2026-02-07T10:00:00.000Z",
    "expiresAt": "2026-02-08T10:00:00.000Z"
  },
  "metadata": {
    "generatedAt": "2026-02-07T10:15:00.000Z",
    "proofServerVersion": "0.16.0"
  }
}
```

## Integration with .NET Tests

These fixtures can be used in .NET integration tests:

```csharp
[Fact]
public async Task LoadProofEnvelope_ValidFixture_ShouldSucceed()
{
    var fixturePath = Path.Combine(
        TestContext.RepositoryRoot,
        "tests/fixtures/valid-proof-envelope.json"
    );
    
    var envelope = await ProofEnvelopeLoader.LoadFromFileAsync(fixturePath);
    
    Assert.NotNull(envelope);
    Assert.Equal("chainborn-sample-app", envelope.ProductId);
}

[Fact]
public async Task ValidateProof_ExpiredChallenge_ShouldFail()
{
    var fixturePath = Path.Combine(
        TestContext.RepositoryRoot,
        "tests/fixtures/expired-proof-envelope.json"
    );
    
    var envelope = await ProofEnvelopeLoader.LoadFromFileAsync(fixturePath);
    var result = await validator.ValidateAsync(envelope);
    
    Assert.False(result.IsValid);
    Assert.Contains("expired", result.Error, StringComparison.OrdinalIgnoreCase);
}
```

## Security Notes

⚠️ **These are test fixtures only!**

- The proof bytes and verification keys in these fixtures are **synthetic examples**
- They do NOT represent real Midnight ZK proofs
- They MUST NOT be used in production environments
- Real proofs must be generated using the Midnight proof server and Chainborn CLI

## Related Documentation

- [Proof Envelope Format Documentation](../docs/proof-envelope.md)
- [Proof Envelope JSON Schema](../policies/schemas/proof-envelope.schema.json)
