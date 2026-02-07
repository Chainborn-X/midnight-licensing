# Chainborn Licensing Platform

Privacy-preserving licensing platform built on Midnight blockchain with offline ZK proof verification in .NET applications distributed as Docker images.

## Overview

This platform enables commercial software vendors to issue and validate licenses using zero-knowledge proofs on the Midnight blockchain. The key benefits:

- **Privacy-Preserving**: License ownership is private. Customers prove they have a valid license without revealing wallet contents.
- **Offline Validation**: Applications validate licenses locally without calling a centralized server at runtime.
- **ZK Proof Based**: Cryptographically secure validation using Midnight's zero-knowledge proof system.
- **Flexible Policies**: Support for tiers, features, revocation, renewal, and binding modes.
- **Docker Native**: Designed for containerized enterprise environments and air-gapped deployments.

## Key Architectural Principle

**The only bridge point between Midnight's ecosystem and .NET is proof verification.**

- **Proof generation** and **smart contracts** use Midnight's native toolchain (Compact/TypeScript)
- The .NET side **only verifies proof output** via the `IProofVerifier` interface
- The bridge interface accepts: proof bytes + verification key → returns: valid/invalid + public inputs
- Everything else (policy evaluation, caching, binding modes, TTL enforcement) is pure .NET business logic with no blockchain dependency

## Repository Structure

```
midnight-licensing/
├── docs/                          # Documentation
│   ├── prs.md                     # Product Requirements Specification
│   └── architecture.md            # Architecture and design decisions
├── src/
│   ├── contracts/                 # Midnight blockchain contracts (separate toolchain)
│   │   ├── license-contract/      # Compact smart contracts
│   │   └── tests/                 # Contract tests
│   ├── sdk/                       # .NET SDK libraries
│   │   ├── Chainborn.Licensing.Abstractions/  # Core interfaces and types
│   │   └── Chainborn.Licensing.Policy/        # Policy providers
│   ├── validator/                 # License validation library
│   │   ├── Chainborn.Licensing.Validator/       # Main validator implementation
│   │   └── Chainborn.Licensing.Validator.Tests/ # Unit tests
│   ├── cli/                       # Command-line tools
│   │   └── Chainborn.Licensing.Cli/           # Proof generator CLI
│   ├── issuer/                    # License issuance service
│   │   └── Chainborn.Licensing.Issuer/        # SaaS license issuer API
│   └── sample-app/                # Reference implementations
│       └── Chainborn.Licensing.SampleApp/     # Sample Docker app with validation
├── policies/                      # Sample license policy configurations
├── Chainborn.Licensing.sln        # .NET solution file
└── Directory.Build.props          # Shared .NET build configuration
```

## Components

### Midnight Contracts Island (`src/contracts/`)
Midnight blockchain smart contracts written in Compact. Uses its own TypeScript/Compact toolchain. Not part of the .NET solution.

### .NET SDK (`src/sdk/`)
- **Abstractions**: Core interfaces (`ILicenseValidator`, `IProofVerifier`, `IPolicyProvider`) and types
- **Policy**: Policy providers and validators for configuration-driven validation

### Validator Library (`src/validator/`)
NuGet package for integrating license validation into .NET applications. Supports caching, TTL, binding modes, and policy-driven validation.

### CLI Tools (`src/cli/`)
Command-line tooling for generating proofs (bridges to Midnight's proof generation APIs).

### Issuer Service (`src/issuer/`)
Optional SaaS API for managing license issuance on the Midnight blockchain.

### Sample Application (`src/sample-app/`)
Reference Docker application demonstrating license validation integration.

## Getting Started

### .NET Development

```bash
# Build all .NET projects
dotnet build

# Run tests
dotnet test

# Run the sample application
cd src/sample-app/Chainborn.Licensing.SampleApp
dotnet run
```

### Contracts Development

```bash
cd src/contracts

# Install dependencies
npm install

# Build contracts (once Compact tooling is integrated)
npm run build

# Run contract tests
npm test
```

### Docker

Build the Docker image from the repository root:

```bash
# Build the sample application Docker image
docker build -f src/sample-app/Chainborn.Licensing.SampleApp/Dockerfile -t chainborn-sample-app .

# Run the containerized application
docker run -p 8080:8080 chainborn-sample-app

# Test the health endpoint
curl http://localhost:8080/health
```

## Documentation

- [Product Requirements Specification](docs/prs.md) - Detailed requirements and success criteria
- [Architecture](docs/architecture.md) - Technical architecture and bridge point design
- [Proof Envelope Format](docs/proof-envelope.md) - Canonical JSON format for license proofs
- [Spike Documentation](docs/spikes/) - Research findings from Milestone 1 spike issues
  - [Spike #1: License Policy Schema](docs/spikes/spike-001-policy-schema.md)
  - [Spike #2: Wallet Interaction Model](docs/spikes/spike-002-wallet-interaction.md)
  - [Spike #3: ZK Proof .NET Verification](docs/spikes/spike-003-zk-proof-dotnet.md)
  - [Spike #4: Compact Contract Capabilities](docs/spikes/spike-004-compact-contracts.md)

## License

See [LICENSE](LICENSE) for details.
