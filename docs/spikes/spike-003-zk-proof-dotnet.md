# Spike #3: ZK Proof Format and Offline Verification in .NET

**Status:** Complete  
**Issue:** #3  
**Date:** 2026-02-07

---

## Problem Statement

Investigate the format of Midnight ZK proofs, how they can be verified offline in a .NET environment, and identify technical risks and implementation strategies for native .NET proof verification.

---

## Findings

### Midnight Proof System

- Midnight uses **zk-SNARKs** (Groth16-family) for zero-knowledge proofs
- Proofs are generated via the Midnight proof server (Docker-based)
- Proof system provides succinct verification: proofs are small (typically < 1KB) and fast to verify (milliseconds)

### Proof Artifact Structure

A complete proof artifact consists of three components:

1. **Proof bytes**: The actual zk-SNARK proof data (serialized curve points)
2. **Verification key**: G1 and G2 elliptic curve group elements used to verify the proof
3. **Public inputs/signals**: The public values that the proof attests to (e.g., product_id, tier, challenge nonce)

### Critical Gap: No Native C# zk-SNARK Verifier

**This is the single biggest technical risk for the project.**

- No pure C# library exists for Groth16 zk-SNARK verification
- The .NET ecosystem lacks native implementations of pairing-based cryptography (BLS12-381, BN254 curves)
- All existing zk-SNARK verification libraries are in JavaScript (snarkjs), Rust (arkworks, bellman), or Go (gnark)

### Viable Verification Strategies

We identified three approaches, ranked by preference:

#### 1. WASM-Based Verifier (Recommended)

**Approach:**
- Compile a Rust verifier (arkworks or bellman) to WebAssembly or use snarkjs WASM module
- Load and execute WASM module from .NET using `wasmtime-dotnet` NuGet package
- Verification happens in-process with near-native performance

**Pros:**
- Portable across platforms (Linux, Windows, macOS)
- No external process dependencies
- Good performance (WASM overhead is typically 1.5-3x native)
- Leverages battle-tested Rust/JS verifier implementations
- Single binary deployment (WASM module embedded as resource)

**Cons:**
- Requires WASM runtime (adds ~2-5 MB to deployment)
- Slightly slower than native FFI
- May require custom WASM wrapper if existing modules don't match our interface

**Key library:**
- [wasmtime-dotnet](https://github.com/bytecodealliance/wasmtime-dotnet) - Official .NET bindings for Wasmtime

#### 2. Sidecar Node.js Microservice

**Approach:**
- Run snarkjs in a Node.js container alongside the .NET application
- .NET validator communicates with sidecar via HTTP or Unix socket
- Sidecar is a simple Express/Fastify server with one endpoint: `POST /verify`

**Pros:**
- Uses snarkjs directly (the reference implementation used by Midnight ecosystem)
- No FFI or WASM complexity
- Easy to update verifier independently of .NET code
- Simple to prototype and test

**Cons:**
- **Operational complexity:** Two containers to deploy, monitor, and maintain
- Network latency (mitigated by Unix sockets, but still overhead)
- Defeats "single container" goal for simple deployments
- Requires Node.js runtime in deployment environment

**When to use:**
- If WASM approach proves infeasible
- For initial prototyping/validation
- For deployments where multi-container orchestration is already standard (Kubernetes)

#### 3. Native Rust FFI via P/Invoke

**Approach:**
- Compile arkworks or bellman Rust verifier as a shared library (.so/.dll/.dylib)
- Call from C# via P/Invoke
- Ship native libraries alongside .NET binaries

**Pros:**
- **Best performance:** Native code with no overhead
- Direct control over verification implementation

**Cons:**
- **Least portable:** Must compile and ship separate binaries for Linux (x64/ARM), Windows, macOS
- **Build complexity:** Cross-compilation required for all target platforms
- **Hardest to maintain:** Native dependency chain, ABI compatibility concerns
- **Security concerns:** P/Invoke introduces more attack surface

**When to use:**
- If performance requirements exceed WASM capabilities (unlikely for startup validation)
- For extremely high-throughput scenarios (not applicable to license validation)

### Proof Format and Versioning Risks

- Exact Midnight proof byte format and verification key structure follow snarkjs/arkworks conventions
- **Format is not yet finalized for mainnet** — Midnight is pre-Genesis, still in testnet
- Format may change with protocol upgrades or circuit compiler updates
- **Mitigation strategy:**
  - Lock test fixtures to current testnet proof format
  - Add regression tests that fail loudly if format changes
  - Abstract verification behind `IProofVerifier` interface so backend can be swapped
  - Monitor Midnight release notes and upgrade verification layer proactively

### Performance Expectations

Based on benchmarks from snarkjs and arkworks:

- **Groth16 verification time:** 5-50ms depending on circuit complexity
- **Typical license circuit:** Expected to be on the smaller side (simple attribute checks)
- **Target verification time:** < 20ms
- **Acceptable for startup validation:** Even 100ms would be fine for Docker container startup checks

---

## Recommendations

### 1. Start with Mock Verifier for M2 Integration Work

**Do NOT block on real verification implementation.**

```csharp
public class MockProofVerifier : IProofVerifier
{
    public Task<ProofVerificationResult> VerifyAsync(
        byte[] proof,
        byte[] verificationKey,
        ProofChallenge challenge,
        CancellationToken cancellationToken = default)
    {
        // Always return valid for development
        return Task.FromResult(new ProofVerificationResult
        {
            IsValid = true,
            PublicInputs = ExtractPublicInputs(proof) // Parse from proof metadata
        });
    }
}
```

This allows all policy logic, caching, binding validation, and Docker integration to proceed immediately.

### 2. Build WASM-Based Verifier as Primary Strategy

**Implementation plan:**

1. **Select WASM module:** Use snarkjs compiled to WASM or arkworks-based verifier
2. **Integrate wasmtime-dotnet:** Add NuGet package to validator project
3. **Create WASM wrapper:**
   ```csharp
   public class WasmProofVerifier : IProofVerifier
   {
       private readonly Engine _engine;
       private readonly Module _module;
       
       public WasmProofVerifier(string wasmPath)
       {
           _engine = new Engine();
           _module = Module.FromFile(_engine, wasmPath);
       }
       
       public async Task<ProofVerificationResult> VerifyAsync(...)
       {
           // Call WASM verify function
           // Parse results
       }
   }
   ```
4. **Embed WASM as resource:** Include WASM binary in NuGet package
5. **Benchmark:** Measure verification time with realistic license circuit proofs

### 3. Keep Sidecar Node.js as Documented Fallback

- Document the sidecar architecture in `docs/architecture.md`
- Provide Docker Compose example with sidecar for reference
- Use as contingency if WASM proves infeasible or has unexpected limitations

### 4. Lock Test Fixtures and Add Regression Tests

**Critical for mainnet readiness:**

```csharp
[Fact]
public async Task VerifyProof_WithKnownTestnetProof_ShouldSucceed()
{
    // This test uses a real proof from Midnight testnet
    // If it fails, the proof format has changed and we need to update the verifier
    var proof = LoadFixture("testnet_proof_v0.16.bin");
    var vk = LoadFixture("testnet_vk_v0.16.bin");
    
    var result = await _verifier.VerifyAsync(proof, vk, challenge);
    
    Assert.True(result.IsValid);
    Assert.Equal("expected_product_id", result.PublicInputs.ProductId);
}
```

Run these tests in CI and fail the build if they break — this is an early warning system.

### 5. Abstract All Verification Behind IProofVerifier Interface

**Already designed this way in architecture.md.** Benefits:

- Verification backend can be swapped without touching business logic
- Easy to A/B test different implementations
- Mockable for unit tests
- Allows phased rollout: mock → WASM → native FFI (if needed)

---

## Key Libraries and Resources

### WASM Runtime
- [wasmtime-dotnet](https://github.com/bytecodealliance/wasmtime-dotnet) - Official .NET bindings for Wasmtime

### zk-SNARK Verifiers (for compiling to WASM or using as sidecar)
- [snarkjs](https://github.com/iden3/snarkjs) - JavaScript/WASM zk-SNARK verifier (most widely used)
- [arkworks](https://github.com/arkworks-rs/) - Rust ecosystem for zk-SNARKs (high performance)
- [bellman](https://github.com/zkcrypto/bellman) - Rust zk-SNARK library by Zcash team

### Reference Implementations
- Midnight proof server source (if available) for format details
- snarkjs proof format documentation

---

## Open Questions

1. **Midnight proof format stability:** When will the proof format be locked for mainnet?
   - Need to engage with Midnight team for format versioning roadmap
   - May require adaptive verifier that supports multiple formats

2. **Verification key distribution:** How are verification keys distributed and updated?
   - Are they per-contract or per-deployment?
   - How do we handle VK rotation?

3. **Proof size limits:** What is the maximum proof size we should support?
   - Impacts buffer allocation and potential DoS vectors

4. **Hardware requirements:** What are minimum CPU requirements for acceptable verification performance?
   - Important for customer deployment planning

5. **WASM module trust:** How do we ensure WASM modules are not tampered with?
   - Need code signing or hash verification for embedded WASM

---

## Technical Risks

### High Risk
- **No native .NET verifier exists** → Mitigated by WASM approach, but adds complexity
- **Proof format may change** → Mitigated by regression tests and interface abstraction

### Medium Risk
- **WASM performance** → Expected to be acceptable, but needs validation with real proofs
- **Proof format compatibility** → May need versioning and migration strategy

### Low Risk
- **Integration complexity** → wasmtime-dotnet is mature and well-documented
- **Deployment size** → WASM adds a few MB, acceptable for Docker deployments

---

## Next Steps

1. **M2:** Implement `MockProofVerifier` for immediate integration work
2. **M2:** Research snarkjs WASM compilation or find pre-built WASM verifier module
3. **M2:** Prototype `WasmProofVerifier` with wasmtime-dotnet
4. **M3:** Obtain real Midnight testnet proof fixtures and lock them in tests
5. **M3:** Benchmark WASM verification performance
6. **M3:** Document sidecar architecture as fallback
7. **M4:** Replace mock with WASM verifier for production deployments
8. **Post-M4:** Consider native FFI for extreme performance scenarios (if needed)

---

## References

- [wasmtime-dotnet Documentation](https://github.com/bytecodealliance/wasmtime-dotnet)
- [snarkjs Repository](https://github.com/iden3/snarkjs)
- [arkworks Rust Ecosystem](https://github.com/arkworks-rs/)
- [bellman Rust zk-SNARK Library](https://github.com/zkcrypto/bellman)
- [Spike #2: Wallet Interaction](spike-002-wallet-interaction.md) - Proof generation side
- [Spike #4: Compact Contracts](spike-004-compact-contracts.md) - Contract-side proof generation
