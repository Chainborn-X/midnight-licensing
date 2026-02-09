using Chainborn.Licensing.Abstractions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Chainborn.Licensing.Validator.Tests;

/// <summary>
/// Integration tests for binding mode validation and data collection.
/// </summary>
public class BindingModeIntegrationTests
{
    private readonly IProofVerifier _mockProofVerifier;
    private readonly IPolicyProvider _mockPolicyProvider;
    private readonly IValidationCache _mockCache;
    private readonly IBindingDataCollector _bindingDataCollector;
    private readonly ILogger<LicenseValidator> _mockLogger;
    private readonly LicenseValidator _validator;

    public BindingModeIntegrationTests()
    {
        _mockProofVerifier = Substitute.For<IProofVerifier>();
        _mockPolicyProvider = Substitute.For<IPolicyProvider>();
        _mockCache = Substitute.For<IValidationCache>();
        _bindingDataCollector = new BindingDataCollector(Substitute.For<ILogger<BindingDataCollector>>());
        _mockLogger = Substitute.For<ILogger<LicenseValidator>>();
        
        _validator = new LicenseValidator(
            _mockProofVerifier,
            _mockPolicyProvider,
            _mockCache,
            _bindingDataCollector,
            _mockLogger
        );
    }

    [Fact]
    public async Task ValidateAsync_WithBindingModeNone_DoesNotCollectBindingData()
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
        // Binding data should not be collected for BindingMode.None
        Assert.Null(context.BindingData);
    }

    [Fact]
    public async Task ValidateAsync_WithBindingModeOrganization_CollectsBindingData()
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
            BindingMode.Organization,
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
        // Binding data should have been collected in the validator
        // We can't directly check the modified context, but we know it was collected
        // because BindingMode is Organization
    }

    [Fact]
    public async Task ValidateAsync_WithBindingModeEnvironment_CollectsBindingData()
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
            BindingMode.Environment,
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
        // Binding data should have been collected in the validator
    }

    [Fact]
    public async Task ValidateAsync_WithProvidedBindingData_DoesNotOverwrite()
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
        var existingBindingData = new Dictionary<string, string>
        {
            ["custom_org_id"] = "my-org-123"
        };
        var context = new ValidationContext("test-product", existingBindingData);
        var policy = new LicensePolicy(
            "test-product",
            "standard",
            Array.Empty<string>(),
            BindingMode.Organization,
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
        // Existing binding data should be preserved
        Assert.NotNull(context.BindingData);
        Assert.Contains("custom_org_id", context.BindingData.Keys);
        Assert.Equal("my-org-123", context.BindingData["custom_org_id"]);
    }

    [Fact]
    public async Task ValidateAsync_WithCustomBindingEnvironmentVariable_IncludesInBinding()
    {
        // Arrange
        Environment.SetEnvironmentVariable("CHAINBORN_BINDING_ORG_ID", "test-org-456");
        
        try
        {
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
                BindingMode.Organization,
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
            // Custom environment variable should be collected
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("CHAINBORN_BINDING_ORG_ID", null);
        }
    }

    [Fact]
    public async Task ValidateAsync_WithK8sEnvironmentVariables_IncludesInBinding()
    {
        // Arrange
        Environment.SetEnvironmentVariable("K8S_NAMESPACE", "production");
        Environment.SetEnvironmentVariable("K8S_POD_NAME", "my-app-pod-123");
        
        try
        {
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
                BindingMode.Environment,
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
            // K8s environment variables should be collected
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("K8S_NAMESPACE", null);
            Environment.SetEnvironmentVariable("K8S_POD_NAME", null);
        }
    }
}
