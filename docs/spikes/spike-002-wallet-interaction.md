# Spike #2: Midnight Wallet Interaction Model for Proof Generation

**Status:** Complete  
**Issue:** #2  
**Date:** 2026-02-07

---

## Problem Statement

Investigate the current state of Midnight wallet tooling, proof generation workflows, and customer-facing integration patterns to understand how customers will generate license proofs in production.

---

## Findings

### Lace Wallet Integration

- **Lace Wallet** is the official browser extension with integrated Midnight support
- Currently requires **Chrome browser**
- Provides seamless integration between traditional Cardano and Midnight networks
- Reference: [Lace Wallet Documentation](https://docs.midnight.network/develop/how-to/lace-wallet)

### Mesh SDK for Contract Interaction

- **Mesh SDK** (`@meshsdk/midnight-setup`) provides TypeScript APIs for:
  - Smart contract interaction
  - Proof generation
  - Wallet connectivity
- Primary developer interface for building Midnight applications
- Reference: [Mesh SDK Documentation](https://meshjs.dev/midnight/midnight-setup/getting-started)

### Midnight DevKit CLI

- **Midnight DevKit CLI** (`@midnight-devkit/cli`) offers comprehensive proof-server management
- Commands available:
  - `midnight proof-server start` - Launch proof server
  - `midnight proof-server stop` - Stop proof server
  - `midnight proof-server status` - Check server status
  - `midnight proof-server logs` - View server logs
- Reference: [Midnight DevKit Repository](https://github.com/depapp/midnight-devkit)

### Proof Generation Architecture

- Proof generation **requires a local proof server** running as a Docker container
- The proof server bundles pre-computed **ZK parameters** (circuit-specific trusted setup)
- **Critical design principle:** Proofs are generated **client-side only** — never server-side
- Ensures customer privacy: vendor never sees customer wallet contents or private state

### Limitations and Gaps

1. **No air-gapped/fully-offline proof generation documented yet**
   - The proof server must be running locally
   - Cannot generate proofs on a completely offline machine without prior setup
   - Air-gapped environments require pre-deploying the proof server Docker image

2. **No standalone CLI for portable proof generation**
   - **Gap identified:** No tool exists for "generate a license proof and export it as a portable file"
   - Current tooling assumes browser-based workflows or direct SDK integration
   - **Chainborn must build this tool** to support Docker/enterprise use cases

3. **Mobile SDK Under Development**
   - iOS and Android SDKs are planned for release in 2026
   - Will enable proof generation on mobile devices
   - Timeline uncertain — not suitable for MVP planning

### Additional Tooling Resources

- [Awesome Midnight Tooling Directory](https://awesomemidnight.com/tooling) - Community-curated list of Midnight development tools and resources

---

## Recommendations

### 1. Build a Custom CLI Tool for License Proof Generation

**Rationale:** Customers need a simple, scriptable way to generate license proofs without writing code or running a browser extension.

**Approach:**
- Wrap the Midnight TypeScript SDK (Mesh or DevKit)
- Create a Node.js-based CLI tool with commands like:
  ```bash
  chainborn-license generate-proof \
    --wallet <wallet-path> \
    --product <product-id> \
    --challenge <nonce> \
    --output proof.json
  ```
- Package as both npm package and standalone binary (using pkg or similar)

### 2. Define Portable Proof JSON Envelope Format

**Rationale:** Proofs must be transferred from customer's proof generation environment to their runtime Docker containers.

**Recommended structure:**
```json
{
  "version": "1.0",
  "proof": "<base64-encoded-proof-bytes>",
  "verificationKey": "<base64-encoded-vk>",
  "publicInputs": {
    "productId": "...",
    "tier": "...",
    "challenge": "..."
  },
  "metadata": {
    "generatedAt": "2026-02-07T10:00:00Z",
    "proofServerVersion": "0.16.0"
  }
}
```

See [Spike #3](spike-003-zk-proof-dotnet.md) for .NET deserialization requirements.

### 3. Document Customer-Facing Proof Generation Flow

**Create a step-by-step guide:**

1. Install proof server Docker image: `docker pull midnight/proof-server:latest`
2. Start proof server: `midnight proof-server start`
3. Install Chainborn CLI: `npm install -g @chainborn/license-cli`
4. Connect wallet (Lace or hardware wallet)
5. Generate proof: `chainborn-license generate-proof --wallet ./wallet.json --product myapp --output license-proof.json`
6. Provide proof to application via environment variable or mounted volume:
   ```bash
   docker run -e LICENSE_PROOF="$(cat license-proof.json)" myapp:latest
   ```

### 4. Plan for Proof Server Docker Image Distribution

**For enterprise/air-gapped customers:**

- Host Midnight proof server Docker image in a public registry or provide download instructions
- Document how to pull and cache the image in enterprise container registries
- Ensure image size is reasonable for customer bandwidth constraints
- Provide checksum/signature verification for security

**For development:**

- Integrate proof server startup into development scripts
- Add `docker-compose.yml` examples for local development environments

---

## Open Questions

1. **Proof server performance:** How long does proof generation take for typical license circuits?
   - Need benchmarks for different circuit complexities
   - May impact customer UX if proofs take more than a few seconds

2. **Proof server resource requirements:** What are the CPU/memory/disk requirements?
   - Critical for enterprise deployment planning
   - May impact Docker image sizing

3. **Hardware wallet support:** Can proofs be generated using Ledger/Trezor devices?
   - Important for high-security enterprise deployments
   - May be a premium feature

4. **Proof refresh frequency:** How often must customers regenerate proofs?
   - Related to TTL and revocation strategy
   - Impacts operational burden on customers

5. **Multi-wallet scenarios:** Can one proof cover licenses from multiple wallets?
   - Useful for organizations with multiple license pools
   - May require aggregated proofs or multiple proof validation

---

## References

- [Mesh SDK: Getting Started](https://meshjs.dev/midnight/midnight-setup/getting-started)
- [Midnight DevKit CLI](https://github.com/depapp/midnight-devkit)
- [Lace Wallet Documentation](https://docs.midnight.network/develop/how-to/lace-wallet)
- [Awesome Midnight Tooling](https://awesomemidnight.com/tooling)
- [Spike #3: ZK Proof .NET Verification](spike-003-zk-proof-dotnet.md)
- [Spike #4: Compact Contracts](spike-004-compact-contracts.md)

---

## Next Steps

1. **M2:** Implement Chainborn License CLI wrapper around Midnight SDK
2. **M2:** Define and document proof JSON envelope schema
3. **M2:** Write customer setup guide with proof generation instructions
4. **M3:** Create Docker Compose examples for proof server + application
5. **M4:** Investigate hardware wallet integration for enterprise security
