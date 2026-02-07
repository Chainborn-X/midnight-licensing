# Midnight License Contracts

This directory contains the Midnight blockchain smart contracts for the licensing platform. These contracts are written in Compact (Midnight's smart contract language) and use the Midnight TypeScript SDK.

## Separate Toolchain

**Important**: This directory uses its own toolchain and is **NOT part of the .NET solution**. It has separate build and test commands.

### Build

```bash
npm install
npm run build
```

### Test

```bash
npm test
```

## What Goes Here

### License Contract (`license-contract/`)

The core smart contract that:
- Issues licenses to customer wallets
- Stores license attributes in private state (product_id, tier, features, validity, etc.)
- Supports license transfer (if configured)
- Handles revocation (explicit or via expiry)

### Proof Circuits

Zero-knowledge proof circuits that enable customers to prove:
- They own a valid license
- The license satisfies a specific policy
- Without revealing wallet contents or other licenses

### Contract Tests (`tests/`)

Test suite for contract functionality:
- License issuance flows
- Proof generation and verification
- Revocation scenarios
- Transfer mechanics

## Integration with .NET

The .NET validator does **not** call these contracts directly. The integration flow is:

1. **Issuance**: Vendor calls contract to issue license → license stored on Midnight
2. **Proof Generation**: Customer uses Midnight tooling (TypeScript) to generate proof
3. **Proof File**: Customer provides proof file to .NET application
4. **Validation**: .NET validator verifies proof cryptographically (offline)

See `docs/architecture.md` for the complete bridge point design.

## Development Status

**Status**: Pending Midnight SDK integration

The Compact contract implementation and proof circuit design are pending spike work to evaluate Midnight's current APIs and primitives.
