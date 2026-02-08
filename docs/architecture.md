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
   - **Proof Loading** (see [Proof Loader](#proof-loader) section below):
     - `IProofLoader.LoadAsync()` checks multiple sources in priority order
     - 1) `LICENSE_PROOF` environment variable (base64 JSON)
     - 2) `LICENSE_PROOF_FILE` environment variable (file path)
     - 3) `/etc/chainborn/proof.json` (default fallback)
     - Deserializes JSON into `ProofEnvelope` structure

4. **License Validation** (.NET validator, offline, local)
   - Application loads proof file at startup via `IProofLoader`
   - Extracts `LicenseProof` from `ProofEnvelope`
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
   - Cache respects critical TTL invariant: `ExpiresAt ≤ min(challenge.ExpiresAt, validatedAt + policy.CacheTtl)`
   - Reduces proof verification overhead
   - See [Runtime Cache Architecture](runtime-cache.md) for detailed cache behavior and invariant enforcement

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

## Proof Loader

### Overview

The **`IProofLoader`** interface provides a flexible mechanism for loading proof envelopes from various sources. This abstraction allows applications to obtain proofs from environment variables, files, Kubernetes secrets, or future custom sources without changing application code.

### Interface

```csharp
public interface IProofLoader
{
    Task<ProofEnvelope?> LoadAsync(CancellationToken cancellationToken = default);
}
```

### Resolution Priority

The default `ProofLoader` implementation checks sources in this order:

1. **`LICENSE_PROOF` environment variable** (base64-encoded JSON)
   - Highest priority
   - Useful for CI/CD, simple deployments, and testing
   - Entire proof envelope is base64-encoded and passed as a single variable

2. **`LICENSE_PROOF_FILE` environment variable** (file path)
   - Points to a file containing proof envelope JSON
   - Supports absolute and relative paths
   - Useful for Docker volumes and custom mount points

3. **`/etc/chainborn/proof.json`** (default fallback path)
   - Standard location by convention
   - Used when no environment variables are set
   - Supports Docker volumes and Kubernetes ConfigMaps/Secrets

### Proof Envelope Format

The proof envelope is a JSON structure that wraps a `LicenseProof`:

```json
{
  "proof": {
    "proofBytes": "<base64>",
    "verificationKeyBytes": "<base64>",
    "productId": "product-id",
    "challenge": {
      "nonce": "random-nonce",
      "issuedAt": "2026-02-07T10:00:00Z",
      "expiresAt": "2026-02-07T11:00:00Z"
    },
    "metadata": {}
  },
  "version": "1.0",
  "metadata": {}
}
```

See [Proof Envelope](proof-envelope.md) for detailed format specification and examples.

### Error Handling

The proof loader **fails fast** with clear error messages:

- **No source found**: Lists all checked locations
- **Invalid format**: Specifies which parsing/decoding step failed
- **File not found**: Indicates the missing file path
- **Missing required fields**: Points to the invalid envelope structure

This design ensures operators can quickly diagnose proof loading issues during deployment.

### Docker & Kubernetes Integration

#### Docker Compose
```yaml
services:
  app:
    environment:
      - LICENSE_PROOF_FILE=/etc/chainborn/proof.json
    volumes:
      - ./proof.json:/etc/chainborn/proof.json:ro
```

#### Kubernetes Secret
```yaml
env:
- name: LICENSE_PROOF_FILE
  value: /mnt/secrets/proof.json
volumeMounts:
- name: proof-secret
  mountPath: /mnt/secrets
  readOnly: true
volumes:
- name: proof-secret
  secret:
    secretName: license-proof
```

### Extensibility

The proof loader is designed for future extension:

#### Custom Proof Sources
Future versions may support plugin-based proof sources:
- Cloud secret managers (AWS Secrets Manager, Azure Key Vault, GCP Secret Manager)
- HTTP endpoints for centralized proof distribution
- Database backends (PostgreSQL, Redis)
- Custom enterprise integrations

#### Planned Plugin Architecture
```csharp
public interface IProofSource
{
    Task<ProofEnvelope?> TryLoadAsync(CancellationToken cancellationToken);
    int Priority { get; }
}

services.AddLicenseValidation(options =>
{
    options.ProofSources.Add<AzureKeyVaultProofSource>();
    options.ProofSources.Add<HttpProofSource>();
});
```

#### Schema Validation
Future versions may include JSON Schema validation to ensure proof envelopes conform to the canonical format before deserialization, providing additional safety against malformed inputs.

### Testing

The `ProofLoader` constructor accepts injectable dependencies for environment access and file I/O, enabling comprehensive unit testing without file system or environment dependencies:

```csharp
public ProofLoader(
    ILogger<ProofLoader> logger,
    Func<string, string?> getEnvironmentVariable,
    Func<string, bool> fileExists,
    Func<string, CancellationToken, Task<string>> readFileAsync)
```

This design allows tests to mock all external dependencies and verify behavior in isolation.


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

---

## Known Unknowns

- **Compact proof output format stability:** Midnight is currently in the Kūkolu phase with mainnet genesis projected within ~90 days (as of Jan 2026). The Compact language and proof output format may change before or after mainnet. Keep test fixtures locked to specific testnet versions and regression test regularly when upgrading SDK versions.
- **.NET ZK proof verification availability:** There is currently no official .NET-native ZK proof verifier for Midnight. The ecosystem is TypeScript/JavaScript-first. Spike #3 is investigating feasibility — this is the single biggest technical risk to the project. If native verification proves infeasible, fallback strategies exist (see below).
- **Wallet and proof generation tooling maturity:** Midnight wallet tooling (CLI, browser extension, desktop) is still evolving. The proof generation UX for customers may change significantly as the ecosystem matures. Spike #2 is tracking this.
- **Contract upgradability:** It is not yet confirmed whether Compact contracts support schema migration or upgradability patterns. If license struct changes are needed post-deployment, this could require redeployment and migration tooling. Spike #4 is investigating.
- **Legal and compliance considerations:** ZK-based licensing in regulated geographies may have implications that need legal review before the first enterprise pilot.

## Platform Agnostic by Design

- The validator architecture is intentionally not married to Midnight. The `IProofVerifier` interface is the single, deliberately thin bridge point between any ZK proof system and the .NET validation pipeline.
- Everything above `IProofVerifier` — policy evaluation, caching, binding checks, TTL enforcement, nonce verification — is pure .NET business logic with zero blockchain dependency.
- To support a different ZK proof system or blockchain, you only need to implement `IProofVerifier` with the new backend. No other code changes are required unless public input semantics change.
- To add a new policy field or constraint, extend the `LicensePolicy` record and update only the policy evaluation logic in `LicenseValidator`.
- This design makes the platform suitable for future multi-chain or multi-proof-system scenarios.

## Fallback Verification Strategies

- If .NET-native proof verification is not feasible (the most likely risk), three fallback approaches are supported without breaking the validator pipeline:
  1. **Node.js sidecar process:** Run Midnight's official TypeScript verification logic as a lightweight HTTP microservice or stdin/stdout subprocess. The `IProofVerifier` implementation calls it via HTTP or process invocation. Adds a runtime dependency but uses battle-tested verification code.
  2. **WASM module:** If Midnight's verifier is available as WASM (or can be compiled from Rust to WASM), load it in-process via a .NET WASM runtime (e.g., Wasmtime for .NET). Best performance of the fallback options, no external process needed.
  3. **Native interop (P/Invoke):** If a Rust or C verification library exists, wrap it via P/Invoke or NativeAOT bindings. Highest performance, but requires platform-specific native binaries in the NuGet package.
- All three approaches are transparent to the rest of the system — `LicenseValidator`, policy evaluation, caching, and the sample app don't change regardless of which `IProofVerifier` implementation is used.
- The decision on which approach to use will be driven by the findings of Spike #3.

## External References

- <a href="https://docs.midnight.network/">Midnight Official Documentation</a> — Full API reference, developer guides, troubleshooting
- <a href="https://midnight.network/developer-hub">Midnight Developer Hub</a> — SDK downloads, tooling, community resources
- <a href="https://docs.midnight.network/develop/reference/compact/">Compact Language Reference</a> — Language spec, standard library, circuit syntax
- <a href="https://academy.midnight.network">Midnight Academy 2.0</a> — Free developer education curriculum
- <a href="https://midnight.network/">Nightpaper</a> — Technical whitepaper covering proof architecture and design decisions
- <a href="https://github.com/joacolinares/kyc-midnight">KYC Reference dApp (GitHub)</a> — Example privacy-preserving dApp using Compact and ZK proofs
- <a href="https://github.com/luislucena16/midnight-quick-starter">Midnight Quick Starter Template</a> — Monorepo template with contracts, backend, frontend
- <a href="https://awesomemidnight.com/tooling">Awesome Midnight — Tooling &amp; Libraries</a> — Community-curated SDK and tool index
- <a href="https://discord.gg/midnight">Midnight Discord</a> — Community support and developer discussions
- <a href="https://midnight.network/blog/state-of-the-network-january-2026">State of the Network — January 2026</a> — Latest network status and roadmap update
