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

        // Validate that proof product ID matches context product ID
        if (!string.Equals(proof.ProductId, context.ProductId, StringComparison.Ordinal))
        {
            _logger.LogError("Product ID mismatch: proof is for '{ProofProductId}' but validation requested for '{ContextProductId}'",
                proof.ProductId, context.ProductId);
            return new LicenseValidationResult(
                IsValid: false,
                Errors: new[] { $"Product ID mismatch: proof is for '{proof.ProductId}' but validation requested for '{context.ProductId}'" },
                ValidatedAt: now
            );
        }

        var cacheKey = GenerateCacheKey(proof, context);

        // Step 1: Check cache
        var cachedResult = await _validationCache.GetAsync(cacheKey, cancellationToken);
        if (cachedResult != null && cachedResult.ExpiresAt > now)
        {
            // Step 1.1: Verify cache invariant before returning cached result
            // Get policy to validate cache TTL invariant
            var policyForCacheValidation = await _policyProvider.GetPolicyAsync(context.ProductId, cancellationToken);
            if (policyForCacheValidation != null)
            {
                var maxAllowedExpiry = CalculateExpiresAt(proof.Challenge.ExpiresAt, cachedResult.ValidatedAt, policyForCacheValidation.CacheTtl);
                
                if (cachedResult.ExpiresAt > maxAllowedExpiry)
                {
                    _logger.LogError(
                        "Cache invariant violation detected for product {ProductId}: " +
                        "cached ExpiresAt ({CachedExpiresAt}) exceeds maximum allowed ({MaxAllowedExpiry}). " +
                        "Challenge expires at {ChallengeExpiresAt}, validated at {ValidatedAt}, cache TTL is {CacheTtl}",
                        context.ProductId,
                        cachedResult.ExpiresAt,
                        maxAllowedExpiry,
                        proof.Challenge.ExpiresAt,
                        cachedResult.ValidatedAt,
                        policyForCacheValidation.CacheTtl);
                    
                    return new LicenseValidationResult(
                        IsValid: false,
                        Errors: new[] { 
                            $"Cache invariant violation: cached result expires at {cachedResult.ExpiresAt:O} " +
                            $"but maximum allowed expiry is {maxAllowedExpiry:O} " +
                            $"(min of challenge expiry {proof.Challenge.ExpiresAt:O} and cache TTL bound {cachedResult.ValidatedAt + policyForCacheValidation.CacheTtl:O})"
                        },
                        ValidatedAt: now
                    );
                }
            }

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

        // Step 3: Validate nonce (challenge) before expensive proof verification
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

        // Step 4: Verify proof cryptographically
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
        // Include product ID, nonce, binding data, and strictness mode in cache key
        var keyParts = new List<string>
        {
            context.ProductId,
            proof.Challenge.Nonce,
            context.StrictnessMode.ToString()
        };

        // Include binding data if present, using secure serialization
        if (context.BindingData != null && context.BindingData.Count > 0)
        {
            // Use Base64 encoding to avoid injection attacks from special characters in binding data
            var bindingDataString = string.Join("|", context.BindingData
                .OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{kvp.Key}={kvp.Value}"));
            var bindingDataBytes = System.Text.Encoding.UTF8.GetBytes(bindingDataString);
            var bindingDataHash = Convert.ToBase64String(bindingDataBytes);
            keyParts.Add(bindingDataHash);
        }

        return string.Join(":", keyParts);
    }
}
