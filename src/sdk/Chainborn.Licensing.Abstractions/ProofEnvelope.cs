namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Represents a portable proof envelope containing a license proof and associated metadata.
/// This is the serializable format for storing and transferring proofs.
/// </summary>
public record ProofEnvelope(
    LicenseProof Proof,
    string Version = "1.0",
    IReadOnlyDictionary<string, string>? Metadata = null);
