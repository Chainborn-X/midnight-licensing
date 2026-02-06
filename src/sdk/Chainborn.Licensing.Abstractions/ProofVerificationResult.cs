namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// The result of cryptographic proof verification.
/// </summary>
public record ProofVerificationResult(
    bool IsValid,
    string? Error = null,
    IReadOnlyDictionary<string, string>? PublicInputs = null);
