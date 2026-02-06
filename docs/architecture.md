# Architecture

## Key Principle

**Proof generation and smart contracts use Midnight's native toolchain (Compact/TypeScript). The .NET side only verifies proof output.**

The single bridge interface is `IProofVerifier` — it accepts proof bytes and a verification key and returns whether the proof is valid plus any public inputs.

Everything above that layer (policy evaluation, caching, binding checks, TTL enforcement) is pure .NET business logic with no blockchain dependency. This separation ensures:

- The .NET validator can work offline without any Midnight dependencies
- Policy logic is product-configurable without touching blockchain code
- Proof verification implementation can be swapped (mock, native interop, WASM, sidecar)
- Testing is straightforward with mockable interfaces

## Component Diagram

**TODO**: Add component diagram showing:
- Midnight blockchain (smart contracts)
- Customer wallet & proof generator (TypeScript/Compact)
- .NET validator library (offline, policy-driven)
- Docker application (embedding validator)
- Bridge point: `IProofVerifier` interface

## Data Flow

**TODO**: Add detailed data flow diagram

### High-Level Flow

1. **License Issuance** (Midnight blockchain)
   - Vendor issues license to customer wallet via smart contract
   - License stored in private state on Midnight network
   - Customer receives license NFT or private state reference

2. **Proof Generation** (Customer-side, Midnight toolchain)
   - Customer invokes Midnight proof generator (CLI/SDK)
   - Generates ZK proof that they own a valid license
   - Proof bound to challenge nonce (anti-replay)
   - Output: portable proof file (bytes + verification key + metadata)

3. **Proof File Transfer**
   - Customer provides proof file to application
   - Via environment variable, mounted volume, or secret manager
   - Proof is opaque to the application

4. **License Validation** (.NET validator, offline, local)
   - Application loads proof file at startup
   - `ILicenseValidator.ValidateAsync()` called with proof + context
   - Steps:
     - Check cache for recent validation
     - Load product policy (JSON configuration)
     - Call `IProofVerifier.VerifyAsync()` — **THE BRIDGE POINT**
     - Validate nonce freshness (anti-replay)
     - Evaluate policy (tier, features, binding)
     - Cache result with TTL
   - Returns: `LicenseValidationResult` (valid/invalid + errors)

5. **Cached Result**
   - Successful validations cached with TTL
   - Cache survives container restarts (file-based)
   - Reduces proof verification overhead
   - Respects license expiry and policy TTL

## Bridge Point

### `IProofVerifier` Interface

```csharp
public interface IProofVerifier
{
    Task<ProofVerificationResult> VerifyAsync(
        byte[] proof,
        byte[] verificationKey,
        ProofChallenge challenge,
        CancellationToken cancellationToken = default);
}
```

This interface is **intentionally minimal**. It represents the single point where .NET code interacts with Midnight's ZK proof system.

### Implementation Strategies

The initial implementation will be a **mock verifier** for development and testing. The real implementation strategy is **deferred to spike work** and may use:

1. **Native Interop**: P/Invoke to Midnight's native verification library (if available)
2. **WASM**: WebAssembly module embedded in .NET app
3. **Sidecar Process**: Separate process running Midnight verification, called via IPC
4. **Remote Service**: HTTP call to verification service (defeats offline goal, not preferred)

The interface design ensures we can swap implementations without changing any consuming code.

### Why This Separation Matters

- **Testability**: Mock `IProofVerifier` for unit tests, no blockchain required
- **Portability**: Verification strategy is platform-specific, .NET logic is not
- **Security**: Proof verification is isolated, can be sandboxed or attested
- **Maintainability**: Midnight SDK updates don't cascade through entire codebase
- **Performance**: Can optimize/replace verifier without touching business logic

## Technology Stack

### Midnight Blockchain Side
- **Smart Contracts**: Compact language
- **Proof Generation**: TypeScript/Midnight SDK
- **Deployment**: Midnight network

### .NET Side
- **Runtime**: .NET 8.0
- **Validator Library**: Class library (NuGet package)
- **Policy Format**: JSON
- **Caching**: File-based (default), extensible via `IValidationCache`
- **Logging**: Microsoft.Extensions.Logging
- **DI**: Microsoft.Extensions.DependencyInjection

### Docker
- **Base Images**: `mcr.microsoft.com/dotnet/aspnet:8.0`
- **Build**: Multi-stage Dockerfile
- **Configuration**: Environment variables + mounted policy files

## Security Considerations

- **Proof Integrity**: Cryptographic verification ensures proofs cannot be forged
- **Replay Protection**: Nonce binding prevents proof reuse
- **Cache Security**: Cached results respect original proof constraints
- **Binding Modes**: Organization/environment binding prevents license sharing
- **Revocation**: Bounded by TTL, requires proof refresh to detect revocation
- **Privacy**: No wallet information exposed during validation

## Future Extensions

- Attestation-based binding (TPM, SGX)
- Multi-signature revocation
- Hierarchical policies (product families)
- Metrics and telemetry integration
- Cloud secret manager integration (Azure Key Vault, AWS Secrets Manager)
