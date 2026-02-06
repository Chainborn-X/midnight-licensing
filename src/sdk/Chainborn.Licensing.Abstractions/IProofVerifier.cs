namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Verifies a zero-knowledge proof against a verification key.
/// This is the single bridge point between the Midnight ZK ecosystem and .NET.
/// Implementations may use native interop, WASM, or a sidecar process.
/// </summary>
public interface IProofVerifier
{
    /// <summary>
    /// Verifies the cryptographic proof using the provided verification key and challenge.
    /// </summary>
    Task<ProofVerificationResult> VerifyAsync(
        byte[] proof,
        byte[] verificationKey,
        ProofChallenge challenge,
        CancellationToken cancellationToken = default);
}
