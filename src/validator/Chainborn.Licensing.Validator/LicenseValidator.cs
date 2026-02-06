using Chainborn.Licensing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Chainborn.Licensing.Validator;

/// <summary>
/// Core license validator implementation.
/// </summary>
public class LicenseValidator : ILicenseValidator
{
    private readonly IProofVerifier _proofVerifier;
    private readonly IPolicyProvider _policyProvider;
    private readonly IValidationCache _validationCache;
    private readonly ILogger<LicenseValidator> _logger;

    public LicenseValidator(
        IProofVerifier proofVerifier,
        IPolicyProvider policyProvider,
        IValidationCache validationCache,
        ILogger<LicenseValidator> logger)
    {
        _proofVerifier = proofVerifier ?? throw new ArgumentNullException(nameof(proofVerifier));
        _policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        _validationCache = validationCache ?? throw new ArgumentNullException(nameof(validationCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LicenseValidationResult> ValidateAsync(
        LicenseProof proof,
        ValidationContext context,
        CancellationToken cancellationToken = default)
    {
        if (proof == null) throw new ArgumentNullException(nameof(proof));
        if (context == null) throw new ArgumentNullException(nameof(context));

        var now = DateTimeOffset.UtcNow;
        var cacheKey = GenerateCacheKey(proof, context);

        // Step 1: Check cache
        var cachedResult = await _validationCache.GetAsync(cacheKey, cancellationToken);
        if (cachedResult != null && cachedResult.ExpiresAt > now)
        {
            _logger.LogInformation("Returning cached validation result for product {ProductId}", context.ProductId);
            return cachedResult;
        }

        // Step 2: Get policy
        var policy = await _policyProvider.GetPolicyAsync(context.ProductId, cancellationToken);
        if (policy == null)
        {
            _logger.LogError("Policy not found for product {ProductId}", context.ProductId);
            return new LicenseValidationResult(
                IsValid: false,
                Errors: new[] { $"Policy not found for product '{context.ProductId}'" },
                ValidatedAt: now
            );
        }

        // Step 3: Verify proof cryptographically
        var verificationResult = await _proofVerifier.VerifyAsync(
            proof.ProofBytes,
            proof.VerificationKeyBytes,
            proof.Challenge,
            cancellationToken
        );

        if (!verificationResult.IsValid)
        {
            _logger.LogWarning("Proof verification failed for product {ProductId}: {Error}", 
                context.ProductId, verificationResult.Error);
            return new LicenseValidationResult(
                IsValid: false,
                Errors: new[] { verificationResult.Error ?? "Proof verification failed" },
                ValidatedAt: now
            );
        }

        // Step 4: Validate nonce (challenge)
        var nonceErrors = ValidateNonce(proof.Challenge, now);
        if (nonceErrors.Count > 0)
        {
            _logger.LogWarning("Nonce validation failed for product {ProductId}", context.ProductId);
            return new LicenseValidationResult(
                IsValid: false,
                Errors: nonceErrors,
                ValidatedAt: now
            );
        }

        // Step 5: Validate policy requirements (tier, features)
        // TODO: This requires knowing the public input format from Midnight proofs
        // For now, we log that this validation is pending
        _logger.LogInformation("Policy validation for tier and features is pending Midnight proof format definition");

        // Step 6: Build successful result
        var expiresAt = CalculateExpiresAt(proof.Challenge.ExpiresAt, now, policy.CacheTtl);
        var result = new LicenseValidationResult(
            IsValid: true,
            Errors: Array.Empty<string>(),
            ValidatedAt: now,
            ExpiresAt: expiresAt,
            CacheKey: cacheKey
        );

        // Step 7: Cache the result
        await _validationCache.SetAsync(cacheKey, result, policy.CacheTtl, cancellationToken);
        
        _logger.LogInformation("License validation successful for product {ProductId}, expires at {ExpiresAt}", 
            context.ProductId, expiresAt);

        return result;
    }

    private static List<string> ValidateNonce(ProofChallenge challenge, DateTimeOffset now)
    {
        var errors = new List<string>();

        if (challenge.ExpiresAt <= now)
        {
            errors.Add($"Challenge has expired (expired at {challenge.ExpiresAt:O})");
        }

        if (challenge.IssuedAt > now)
        {
            errors.Add($"Challenge issued in the future (issued at {challenge.IssuedAt:O})");
        }

        return errors;
    }

    private static DateTimeOffset CalculateExpiresAt(
        DateTimeOffset challengeExpiry,
        DateTimeOffset now,
        TimeSpan cacheTtl)
    {
        // Take the minimum of challenge expiry and cache TTL
        var cacheBound = now + cacheTtl;
        return challengeExpiry < cacheBound ? challengeExpiry : cacheBound;
    }

    private static string GenerateCacheKey(LicenseProof proof, ValidationContext context)
    {
        // Simple cache key based on product and proof nonce
        return $"{context.ProductId}:{proof.Challenge.Nonce}";
    }
}
