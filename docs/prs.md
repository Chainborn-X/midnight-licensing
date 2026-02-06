# Product Requirements Specification (PRS)
## Midnight-Based Private Licensing Platform for Docker-Distributed Software

**Version:** 1.0  
**Owner:** Product / Platform  
**Audience:** Engineering agents, technical leads, AI task decomposition agents  
**Purpose:** This document defines *what* must be built, not *how*. It is intended to be handed to an agent that will decompose the work into epics, GitHub issues, and implementation tasks.

---

## 1. Problem Statement

Commercial software distributed as Docker images typically relies on centralized license servers, static license keys, or machine-bound activation. These approaches introduce operational fragility, privacy concerns, poor offline support, and weak cryptographic guarantees.

We want a **general-purpose licensing platform** that:
- Stores licenses privately on a public blockchain (Midnight)
- Allows customers to prove license ownership **without revealing wallet contents**
- Enables **local, offline license validation** inside Docker containers
- Supports **revocation, renewal, tiers, and compliance constraints**
- Is configurable per product and suitable for future SaaS commercialization

---

## 2. Goals and Non-Goals

### 2.1 Goals
- Enable privacy-preserving license issuance and validation using Midnight + ZK proofs
- Eliminate the need for a centralized license server at runtime
- Provide a reusable validation library (NuGet) for .NET applications
- Support multiple products with different licensing policies
- Support subscription-based licenses with revocation
- Be suitable for enterprise and air-gapped environments

### 2.2 Non-Goals
- Preventing all forms of piracy (runtime patching is out of scope)
- Building a full billing system (Stripe/Paddle integration is optional)
- Providing a UI wallet or full Midnight SDK
- Locking the design to Elsa Workflows specifically

---

## 3. Target Users

### Primary
- Software vendors distributing commercial Docker images
- Enterprise customers running software in Kubernetes, CI, or air-gapped environments

### Secondary
- Platform operators managing issuance and compliance
- Future SaaS customers using the licensing platform for their own products

---

## 4. High-Level System Overview

### Components
1. **Midnight License Smart Contract**
2. **License Issuer Service (optional SaaS)**
3. **Customer Proof Generator (CLI or tooling)**
4. **Runtime License Validation Library (NuGet)**
5. **Dockerized Applications using the validator**

### Key Principle
> License validation must be possible **without any network call** to the issuer at runtime.

---

## 5. Functional Requirements

### FR-1: License Issuance
- The system must support issuing licenses to customer wallets on Midnight.
- Licenses must be stored privately (non-public state).
- License issuance must support configurable attributes:
  - product_id
  - tier
  - features
  - seat count (optional)
  - validity window (from/to)
  - transferability flag
  - binding mode
- Issuance must be automatable (API-driven).

### FR-2: Privacy-Preserving Ownership
- It must not be possible for third parties to discover:
  - which wallet owns which license
  - how many licenses a wallet owns
- License ownership must be provable via ZK proofs.

### FR-3: Proof Generation
- Customers must be able to generate a cryptographic proof that:
  - they control a wallet holding a valid license
  - the license satisfies a product-specific policy
  - the proof is bound to a runtime challenge (nonce)
- Proof generation must not require revealing wallet contents.
- Proofs must be portable (file/string-based).

### FR-4: Runtime Validation (Offline)
- Applications running in Docker must be able to:
  - validate license proofs locally
  - without calling a centralized license server
- Validation must include:
  - cryptographic proof verification
  - nonce verification (anti-replay)
  - policy evaluation
  - optional environment binding checks

### FR-5: Caching and TTL
- The validator must support caching successful validations.
- Cache TTL must be configurable per product/policy.
- Cached validation must never extend license validity beyond proven constraints.
- Cache must survive container restarts (file-based default).

### FR-6: Revocation
- The platform must support revocation of licenses.
- Revocation effectiveness is bounded by proof refresh / TTL.
- Two revocation models must be supported:
  1. Explicit on-chain revocation
  2. Validity-by-renewal (subscription expiry)
- Product policy must define which model applies.

### FR-7: Transferability
- Transferability must be configurable per product:
  - not transferable
  - owner-initiated transfer
  - issuer-only transfer
- Validation logic must respect transfer rules implicitly via proof validity.

### FR-8: Binding Modes
The platform must support multiple binding modes:
- none
- organization-level binding
- environment binding
- attestation-based binding (future)

Binding mode must be:
- configurable per product
- enforced at proof validation time

### FR-9: Policy-Driven Design
- Each product must define a **License Policy** that specifies:
  - required tier
  - required features
  - binding mode
  - cache TTL
  - revocation model
- Validation must be driven entirely by policy configuration.

### FR-10: Reusable Validation Library
- License validation must be implemented as a standalone NuGet package.
- The package must:
  - expose a clean, stable API
  - be usable outside Docker
  - not embed product-specific logic
- The package must be hosted in a separate GitHub repository.

---

## 6. Non-Functional Requirements

### NFR-1: Security
- Proofs must be tamper-resistant.
- Replay attacks must be mitigated via nonce binding.
- All cryptographic verification must be deterministic and local.

### NFR-2: Privacy
- Validation must not leak wallet identity or balances.
- Logs must not include sensitive proof material by default.

### NFR-3: Reliability
- Validation must work without network access.
- Failure modes must be explicit and debuggable.

### NFR-4: Extensibility
- New products and policies must not require code changes in the validator.
- New binding modes and revocation models must be addable.

### NFR-5: Developer Experience
- Clear configuration via ENV / config files
- Actionable error messages
- Well-documented public APIs

---

## 7. Configuration Interfaces

### Runtime Configuration
- Proof input (ENV / file / secret)
- Policy selection
- Cache directory
- Validation strictness mode

### Policy Definition
- JSON or equivalent structured format
- Signed or embedded
- Versioned

---

## 8. Expected Deliverables

### Code Repositories
1. **License Contract Repository**
2. **Runtime Validator (NuGet) Repository**
3. **Reference Docker Image / Sample App**
4. **Proof Generator Tooling (CLI)**

### Documentation
- Product policy schema
- Customer setup guide
- Enterprise operational guide
- Security & threat model

---

## 9. Milestones (High-Level)

### M1: Foundation
- Policy schema
- Validator API skeleton
- Basic proof verification (happy path)

### M2: Runtime Integration
- Docker integration
- ENV-based proof input
- Cache + TTL

### M3: Revocation & Renewal
- On-chain revocation support
- Validity-by-renewal flow
- Proof refresh UX

### M4: Platform Readiness
- Multi-product support
- Binding modes
- Enterprise docs

---

## 10. Open Questions (Explicitly Allowed)
- Exact Midnight ZK APIs and primitives
- Wallet UX for proof generation
- Attestation provider integrations
- Long-term SaaS packaging

---

## 11. Success Criteria

This product is successful if:
- A Docker container can validate a license offline
- License ownership is private and provable
- Revocation works within defined TTL bounds
- New products can onboard without code changes
- Enterprises accept the model for compliance-sensitive use cases

---
