using Chainborn.Licensing.Abstractions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Chainborn.Licensing.Validator.Tests;

public class LicenseValidatorTests
{
    private readonly IProofVerifier _mockProofVerifier;
    private readonly IPolicyProvider _mockPolicyProvider;
    private readonly IValidationCache _mockCache;
    private readonly ILogger<LicenseValidator> _mockLogger;
    private readonly LicenseValidator _validator;

    public LicenseValidatorTests()
    {
        _mockProofVerifier = Substitute.For<IProofVerifier>();
        _mockPolicyProvider = Substitute.For<IPolicyProvider>();
        _mockCache = Substitute.For<IValidationCache>();
        _mockLogger = Substitute.For<ILogger<LicenseValidator>>();
        
        _validator = new LicenseValidator(
            _mockProofVerifier,
            _mockPolicyProvider,
            _mockCache,
            _mockLogger
        );
    }

    [Fact]
    public async Task ValidateAsync_WithValidProof_ReturnsValid()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var challenge = new ProofChallenge("test-nonce", now.AddMinutes(-5), now.AddHours(1));
        var proof = new LicenseProof(
            new byte[] { 1, 2, 3 },
            new byte[] { 4, 5, 6 },
            "test-product",
            challenge
        );
        var context = new ValidationContext("test-product");
        var policy = new LicensePolicy(
            "test-product",
            "standard",
            Array.Empty<string>(),
            BindingMode.None,
            TimeSpan.FromMinutes(30),
            RevocationModel.ValidityByRenewal,
            "1.0.0"
        );

        _mockCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<LicenseValidationResult?>(null));
        _mockPolicyProvider.GetPolicyAsync("test-product", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<LicensePolicy?>(policy));
        _mockProofVerifier.VerifyAsync(
                Arg.Any<byte[]>(),
                Arg.Any<byte[]>(),
                Arg.Any<ProofChallenge>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProofVerificationResult(true)));

        // Act
        var result = await _validator.ValidateAsync(proof, context);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_WithExpiredNonce_ReturnsInvalid()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var challenge = new ProofChallenge("test-nonce", now.AddHours(-2), now.AddMinutes(-5)); // Expired
        var proof = new LicenseProof(
            new byte[] { 1, 2, 3 },
            new byte[] { 4, 5, 6 },
            "test-product",
            challenge
        );
        var context = new ValidationContext("test-product");
        var policy = new LicensePolicy(
            "test-product",
            "standard",
            Array.Empty<string>(),
            BindingMode.None,
            TimeSpan.FromMinutes(30),
            RevocationModel.ValidityByRenewal,
            "1.0.0"
        );

        _mockCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<LicenseValidationResult?>(null));
        _mockPolicyProvider.GetPolicyAsync("test-product", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<LicensePolicy?>(policy));
        _mockProofVerifier.VerifyAsync(
                Arg.Any<byte[]>(),
                Arg.Any<byte[]>(),
                Arg.Any<ProofChallenge>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProofVerificationResult(true)));

        // Act
        var result = await _validator.ValidateAsync(proof, context);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("expired"));
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidProof_ReturnsInvalid()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var challenge = new ProofChallenge("test-nonce", now.AddMinutes(-5), now.AddHours(1));
        var proof = new LicenseProof(
            new byte[] { 1, 2, 3 },
            new byte[] { 4, 5, 6 },
            "test-product",
            challenge
        );
        var context = new ValidationContext("test-product");
        var policy = new LicensePolicy(
            "test-product",
            "standard",
            Array.Empty<string>(),
            BindingMode.None,
            TimeSpan.FromMinutes(30),
            RevocationModel.ValidityByRenewal,
            "1.0.0"
        );

        _mockCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<LicenseValidationResult?>(null));
        _mockPolicyProvider.GetPolicyAsync("test-product", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<LicensePolicy?>(policy));
        _mockProofVerifier.VerifyAsync(
                Arg.Any<byte[]>(),
                Arg.Any<byte[]>(),
                Arg.Any<ProofChallenge>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new ProofVerificationResult(false, "Invalid proof signature")));

        // Act
        var result = await _validator.ValidateAsync(proof, context);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Invalid proof signature", result.Errors);
    }

    [Fact]
    public async Task ValidateAsync_WithMissingPolicy_ReturnsInvalid()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var challenge = new ProofChallenge("test-nonce", now.AddMinutes(-5), now.AddHours(1));
        var proof = new LicenseProof(
            new byte[] { 1, 2, 3 },
            new byte[] { 4, 5, 6 },
            "test-product",
            challenge
        );
        var context = new ValidationContext("test-product");

        _mockCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<LicenseValidationResult?>(null));
        _mockPolicyProvider.GetPolicyAsync("test-product", Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<LicensePolicy?>(null)); // No policy found

        // Act
        var result = await _validator.ValidateAsync(proof, context);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Policy not found"));
    }

    [Fact]
    public async Task ValidateAsync_WithCachedResult_ReturnsCachedResult()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var challenge = new ProofChallenge("test-nonce", now.AddMinutes(-5), now.AddHours(1));
        var proof = new LicenseProof(
            new byte[] { 1, 2, 3 },
            new byte[] { 4, 5, 6 },
            "test-product",
            challenge
        );
        var context = new ValidationContext("test-product");
        var cachedResult = new LicenseValidationResult(
            true,
            Array.Empty<string>(),
            now.AddMinutes(-10),
            now.AddMinutes(20),
            "cache-key"
        );

        _mockCache.GetAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<LicenseValidationResult?>(cachedResult));

        // Act
        var result = await _validator.ValidateAsync(proof, context);

        // Assert
        Assert.True(result.IsValid);
        Assert.Same(cachedResult, result);
        
        // Verify proof verifier was never called
        await _mockProofVerifier.DidNotReceive().VerifyAsync(
            Arg.Any<byte[]>(),
            Arg.Any<byte[]>(),
            Arg.Any<ProofChallenge>(),
            Arg.Any<CancellationToken>());
    }
}
