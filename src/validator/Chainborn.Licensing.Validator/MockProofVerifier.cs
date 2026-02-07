using Chainborn.Licensing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Chainborn.Licensing.Validator;

/// <summary>
/// Mock proof verifier for development and testing.
/// WARNING: This is NOT a real implementation and should be replaced with actual Midnight ZK proof verification.
/// </summary>
public class MockProofVerifier : IProofVerifier
{
    private readonly ILogger<MockProofVerifier> _logger;

    public MockProofVerifier(ILogger<MockProofVerifier> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<ProofVerificationResult> VerifyAsync(
        byte[] proof,
        byte[] verificationKey,
        ProofChallenge challenge,
        CancellationToken cancellationToken = default)
    {
        _logger.LogWarning("MockProofVerifier is being used - this is NOT a real proof verification implementation!");
        
        // Mock implementation: accept any non-empty proof
        if (proof == null || proof.Length == 0)
        {
            return Task.FromResult(new ProofVerificationResult(false, "Proof is empty"));
        }

        if (verificationKey == null || verificationKey.Length == 0)
        {
            return Task.FromResult(new ProofVerificationResult(false, "Verification key is empty"));
        }

        // In a real implementation, this would verify the cryptographic proof
        // For now, we accept all proofs to allow the application to run
        return Task.FromResult(new ProofVerificationResult(true));
    }
}
