# Spike #4: Midnight Compact Contract Capabilities for Private State Licensing

**Status:** Complete  
**Issue:** #4  
**Date:** 2026-02-07

---

## Problem Statement

Investigate Midnight's Compact smart contract language to understand how to implement private (shielded) state for license management, selective disclosure of license attributes, and contract upgradeability patterns.

---

## Findings

### What is Compact?

- **Compact** is a TypeScript-based domain-specific language (DSL) for writing Midnight smart contracts
- Designed specifically for privacy-preserving applications
- Compiles to both:
  - **Executable bytecode** for on-chain execution
  - **ZK circuit definitions** for proof generation
- Single source of truth: contract logic and proving logic stay in sync

### Private (Shielded) State Support

Compact supports **private state** via `ledger` declarations:

```typescript
export ledger product_id: Bytes<32>;
export ledger tier: Uint<8>;
export ledger valid_until: Uint<64>;
export ledger features: Set<Uint<16>>;
```

**Key characteristics:**
- Fields declared as `ledger` are stored in **shielded state**
- Values are **never revealed on-chain** — only proofs about these values are public
- Only the wallet holding the private state can read or prove properties about it
- Enables zero-knowledge license validation: prove "I have a valid license for product X" without revealing the license itself

### Custom License Attributes

**All license attributes can be stored as private state:**

- `product_id` - Which product the license is for
- `tier` - License tier (e.g., starter, professional, enterprise)
- `validity_window` - Timestamps defining when the license is valid
- `features` - Set of enabled feature flags
- `seat_count` - Number of seats (for concurrent user licensing)
- `transferability` - Whether the license can be transferred
- `binding_mode` - Machine binding constraints

**Implementation approach:**

```typescript
export type License = {
  product_id: Bytes<32>,
  tier: Uint<8>,
  valid_from: Uint<64>,
  valid_until: Uint<64>,
  features: Set<Uint<16>>,
  seats: Uint<32>,
  is_transferable: Boolean,
  binding_data: Bytes<64>
};

export ledger license_data: License;
```

### Selective Disclosure via ZK Proofs

Compact's `circuit` functions enable selective disclosure:

```typescript
export circuit validate_license(
  product_id_claim: Bytes<32>,
  tier_claim: Uint<8>,
  challenge: Bytes<32>
): Witnesses<{
  product_matches: Boolean,
  tier_sufficient: Boolean,
  is_valid_time: Boolean
}> {
  // Private state access
  const license = ledger.license_data;
  
  // Generate proof that:
  // 1. License product matches the claim
  const product_matches = license.product_id === product_id_claim;
  
  // 2. License tier meets or exceeds claimed tier
  const tier_sufficient = license.tier >= tier_claim;
  
  // 3. Current time is within validity window
  const current_time = context.block_time;
  const is_valid_time = 
    current_time >= license.valid_from && 
    current_time <= license.valid_until;
  
  // Return witnesses (these become public outputs of the proof)
  return witnesses({
    product_matches,
    tier_sufficient,
    is_valid_time
  });
}
```

**Privacy properties:**
- Wallet address is **never revealed**
- Full license details are **never revealed**
- Other private fields (features, seat count) are **not disclosed** unless explicitly included in the circuit
- Only the **boolean results** (product_matches, tier_sufficient, is_valid_time) are public
- The ZK proof cryptographically ensures these booleans were computed correctly

### Contract Structure

Typical Compact license contract structure:

```typescript
// Language version pragma (expect breaking changes before mainnet)
@language_version 0.16;

// Private state
export ledger product_id: Bytes<32>;
export ledger tier: Uint<8>;
export ledger valid_until: Uint<64>;
export ledger features: Set<Uint<16>>;

// Constructor (called when license is issued)
export function issue_license(
  _product_id: Bytes<32>,
  _tier: Uint<8>,
  _valid_until: Uint<64>,
  _features: Set<Uint<16>>
): Void {
  ledger.product_id = _product_id;
  ledger.tier = _tier;
  ledger.valid_until = _valid_until;
  ledger.features = _features;
}

// Circuit function (generates ZK proofs)
export circuit validate_license(
  product_claim: Bytes<32>,
  challenge: Bytes<32>
): Witnesses<{ is_valid: Boolean }> {
  const license_matches = ledger.product_id === product_claim;
  const not_expired = context.block_time <= ledger.valid_until;
  
  return witnesses({
    is_valid: license_matches && not_expired
  });
}

// Additional circuits for different validation modes
export circuit validate_with_tier(
  product_claim: Bytes<32>,
  tier_claim: Uint<8>,
  challenge: Bytes<32>
): Witnesses<{ is_valid: Boolean, tier_ok: Boolean }> {
  // ... implementation
}

export circuit check_feature_enabled(
  feature_id: Uint<16>,
  challenge: Bytes<32>
): Witnesses<{ has_feature: Boolean }> {
  return witnesses({
    has_feature: ledger.features.contains(feature_id)
  });
}
```

### Language Maturity and Versioning

**Current state:**
- Language version pragma: `language_version 0.16` (as of Feb 2026)
- **Expect breaking changes before Genesis/mainnet**
- Compact is actively evolving — syntax and semantics may change
- Midnight is still in testnet phase

**Implications:**
- Contract code written today may need updates before mainnet
- Test regularly against latest Compact compiler
- Pin to specific Compact version in CI/CD
- Monitor Midnight release notes for breaking changes

### Contract Upgradeability

**Investigation findings:**

1. **No built-in upgrade patterns documented yet**
   - Unclear if Compact contracts support proxy patterns or state migration
   - No official guidance on schema evolution

2. **Potential approaches:**
   - **Redeployment:** Deploy new contract version, migrate licenses via on-chain transactions
   - **Proxy pattern:** If Midnight supports delegatecall or similar (needs confirmation)
   - **State migration tooling:** Build custom migration scripts

3. **Risk mitigation:**
   - Design license struct to be forward-compatible (reserve unused fields)
   - Keep contract logic simple and stable
   - Abstract contract address from .NET validator (configurable per product)

**Open question:** This is a known risk. Spike findings suggest we should:
- Engage with Midnight team on upgradeability roadmap
- Design for "deploy once" contracts initially
- Plan for migration tooling if contracts need updates

---

## Recommendations

### 1. Design License Contract with Selective Disclosure in Mind

**Principle:** Only expose what's necessary for each validation scenario.

**Recommended circuits:**
- `validate_basic` - Proves license exists and is valid (product + expiry only)
- `validate_with_tier` - Proves license tier meets requirements
- `validate_feature` - Proves specific feature is enabled
- `validate_seats` - Proves seat count is sufficient

Each circuit outputs only the minimum required public data.

### 2. Use Forward-Compatible License Schema

```typescript
export type License = {
  // Core fields (v1)
  product_id: Bytes<32>,
  tier: Uint<8>,
  valid_from: Uint<64>,
  valid_until: Uint<64>,
  
  // Extended fields (v1)
  features: Set<Uint<16>>,
  seats: Uint<32>,
  
  // Reserved for future use
  reserved1: Uint<64>,
  reserved2: Bytes<32>,
  
  // Metadata
  version: Uint<8>,  // Schema version
};
```

This provides upgrade path if contract needs to support new attributes.

### 3. Abstract Contract Addresses in .NET Validator

**Don't hardcode contract addresses in the validator library.**

```csharp
public class ProductPolicy
{
    public string ProductId { get; set; }
    public string ContractAddress { get; set; }  // Configurable per product
    public string VerificationKeyPath { get; set; }
    // ... other policy fields
}
```

Benefits:
- Supports multiple contract versions simultaneously
- Easy to migrate products to new contract versions
- Testing with mock contracts is simpler

### 4. Pin Compact Version in CI/CD

```json
// package.json
{
  "dependencies": {
    "@midnight-devkit/compact-compiler": "0.16.0"  // Pin exact version
  }
}
```

Prevents unexpected breakage from Compact compiler updates.

### 5. Build Test Suite Against Testnet Contracts

- Deploy license contract to Midnight testnet
- Generate real proofs from testnet
- Use these fixtures in .NET validator tests
- Regression test if Compact upgrades break contract behavior

---

## Example License Contract (Simplified)

```typescript
@language_version 0.16;

import { Bytes, Uint, Boolean, Set, Void } from '@midnight/compact-runtime';

// License data structure
export type LicenseData = {
  product_id: Bytes<32>,
  tier: Uint<8>,
  valid_from: Uint<64>,
  valid_until: Uint<64>,
  features: Set<Uint<16>>,
  revoked: Boolean
};

// Private ledger state
export ledger license: LicenseData;

// Issue a new license (called by issuer)
export function issue(
  product_id: Bytes<32>,
  tier: Uint<8>,
  valid_from: Uint<64>,
  valid_until: Uint<64>,
  features: Set<Uint<16>>
): Void {
  ledger.license = {
    product_id,
    tier,
    valid_from,
    valid_until,
    features,
    revoked: false
  };
}

// Revoke a license (called by issuer)
export function revoke(): Void {
  ledger.license.revoked = true;
}

// Validation circuit: prove license is valid
export circuit validate(
  product_claim: Bytes<32>,
  tier_claim: Uint<8>,
  challenge: Bytes<32>
): Witnesses<{
  is_valid: Boolean,
  product_id: Bytes<32>,  // Public output
  challenge: Bytes<32>     // Anti-replay
}> {
  const lic = ledger.license;
  
  // Check all validity conditions
  const product_matches = lic.product_id === product_claim;
  const tier_sufficient = lic.tier >= tier_claim;
  const not_revoked = !lic.revoked;
  
  const current_time = context.block_time;
  const time_valid = 
    current_time >= lic.valid_from && 
    current_time <= lic.valid_until;
  
  const is_valid = 
    product_matches && 
    tier_sufficient && 
    not_revoked && 
    time_valid;
  
  return witnesses({
    is_valid,
    product_id: product_claim,  // Echo back for binding
    challenge                    // Prevent replay attacks
  });
}

// Feature-specific validation
export circuit has_feature(
  feature_id: Uint<16>,
  challenge: Bytes<32>
): Witnesses<{
  enabled: Boolean,
  feature_id: Uint<16>,
  challenge: Bytes<32>
}> {
  const enabled = ledger.license.features.contains(feature_id);
  
  return witnesses({
    enabled,
    feature_id,
    challenge
  });
}
```

---

## Open Questions

1. **Contract upgrade patterns:** What is the official Midnight approach to contract upgrades?
   - Need documentation from Midnight team
   - May require custom migration tooling

2. **Gas costs:** What are the costs for issuing, revoking, and querying licenses?
   - Important for SaaS pricing model
   - May influence batch issuance strategies

3. **Multi-license wallets:** Can one wallet hold multiple licenses efficiently?
   - Relevant for customers with multiple products
   - May need indexed data structures

4. **State size limits:** Are there limits on ledger state size or Set cardinality?
   - Impacts maximum features per license
   - May require pagination or chunking

5. **Proof circuit complexity limits:** Are there constraints on circuit size/depth?
   - May limit how many validation conditions we can include in one proof

6. **Language stability timeline:** When will Compact reach 1.0 stability?
   - Impacts development timeline risk

---

## Technical Risks

### High Risk
- **Language breaking changes before mainnet** → Mitigated by pinning versions and regression tests
- **No documented upgrade path** → Mitigated by forward-compatible schema and abstracting contract addresses

### Medium Risk
- **Circuit complexity limits** → Need to validate with realistic license validation circuits
- **Proof generation performance** → May impact customer UX if circuits are too complex

### Low Risk
- **Basic functionality** → Core ledger and circuit features are stable enough for prototyping

---

## Next Steps

1. **M2:** Design initial license contract with basic validation circuit
2. **M2:** Set up Compact development environment (compiler, local testnet)
3. **M2:** Deploy test contract to Midnight testnet
4. **M2:** Generate test proofs and validate format matches expectations from Spike #3
5. **M3:** Implement revocation logic in contract
6. **M3:** Add multi-tier and feature validation circuits
7. **M4:** Test contract upgradeability options (if available)
8. **M4:** Document contract deployment and migration procedures

---

## References

- Midnight Compact Language Documentation (when available)
- Midnight Testnet Documentation
- [Spike #2: Wallet Interaction](spike-002-wallet-interaction.md) - Proof generation tooling
- [Spike #3: ZK Proof .NET Verification](spike-003-zk-proof-dotnet.md) - Proof format and verification
- Compact compiler repository (if public)
- Midnight developer Discord/forum (for upgrade patterns and best practices)
