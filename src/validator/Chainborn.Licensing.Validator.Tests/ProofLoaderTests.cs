using System.Text;
using System.Text.Json;
using Chainborn.Licensing.Abstractions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Chainborn.Licensing.Validator.Tests;

public class ProofLoaderTests
{
    private readonly ILogger<ProofLoader> _mockLogger;

    public ProofLoaderTests()
    {
        _mockLogger = Substitute.For<ILogger<ProofLoader>>();
    }

    [Fact]
    public async Task LoadAsync_FromEnvironmentVariable_ReturnsProofEnvelope()
    {
        // Arrange
        var proofEnvelope = CreateTestProofEnvelope();
        var json = SerializeProofEnvelope(proofEnvelope);
        var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns(base64Json);
        getEnv("LICENSE_PROOF_FILE").Returns((string?)null);

        var fileExists = Substitute.For<Func<string, bool>>();
        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act
        var result = await loader.LoadAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-product", result.Proof.ProductId);
        Assert.Equal("test-nonce", result.Proof.Challenge.Nonce);
        Assert.Equal("1.0", result.Version);
    }

    [Fact]
    public async Task LoadAsync_FromEnvironmentVariableFile_ReturnsProofEnvelope()
    {
        // Arrange
        var proofEnvelope = CreateTestProofEnvelope();
        var json = SerializeProofEnvelope(proofEnvelope);
        var filePath = "/custom/path/proof.json";

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns((string?)null);
        getEnv("LICENSE_PROOF_FILE").Returns(filePath);

        var fileExists = Substitute.For<Func<string, bool>>();
        fileExists(filePath).Returns(true);

        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();
        readFile(filePath, Arg.Any<CancellationToken>()).Returns(Task.FromResult(json));

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act
        var result = await loader.LoadAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-product", result.Proof.ProductId);
        await readFile.Received(1).Invoke(filePath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_FromDefaultPath_ReturnsProofEnvelope()
    {
        // Arrange
        var proofEnvelope = CreateTestProofEnvelope();
        var json = SerializeProofEnvelope(proofEnvelope);
        var defaultPath = "/etc/chainborn/proof.json";

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns((string?)null);
        getEnv("LICENSE_PROOF_FILE").Returns((string?)null);

        var fileExists = Substitute.For<Func<string, bool>>();
        fileExists(defaultPath).Returns(true);

        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();
        readFile(defaultPath, Arg.Any<CancellationToken>()).Returns(Task.FromResult(json));

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act
        var result = await loader.LoadAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-product", result.Proof.ProductId);
        await readFile.Received(1).Invoke(defaultPath, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_NoSourceAvailable_ThrowsException()
    {
        // Arrange
        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns((string?)null);
        getEnv("LICENSE_PROOF_FILE").Returns((string?)null);

        var fileExists = Substitute.For<Func<string, bool>>();
        fileExists(Arg.Any<string>()).Returns(false);

        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseValidationException>(
            async () => await loader.LoadAsync());

        Assert.Contains("No proof envelope found", exception.Message);
        Assert.Contains("LICENSE_PROOF", exception.Message);
        Assert.Contains("LICENSE_PROOF_FILE", exception.Message);
        Assert.Contains("/etc/chainborn/proof.json", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_InvalidBase64_ThrowsException()
    {
        // Arrange
        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns("not-valid-base64!");
        getEnv("LICENSE_PROOF_FILE").Returns((string?)null);

        var fileExists = Substitute.For<Func<string, bool>>();
        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseValidationException>(
            async () => await loader.LoadAsync());

        Assert.Contains("Failed to decode base64", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_InvalidJson_ThrowsException()
    {
        // Arrange
        var invalidJson = "{invalid json}";
        var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(invalidJson));

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns(base64Json);
        getEnv("LICENSE_PROOF_FILE").Returns((string?)null);

        var fileExists = Substitute.For<Func<string, bool>>();
        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseValidationException>(
            async () => await loader.LoadAsync());

        Assert.Contains("Failed to", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_FileNotFound_ThrowsException()
    {
        // Arrange
        var filePath = "/missing/proof.json";

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns((string?)null);
        getEnv("LICENSE_PROOF_FILE").Returns(filePath);

        var fileExists = Substitute.For<Func<string, bool>>();
        fileExists(filePath).Returns(false);

        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseValidationException>(
            async () => await loader.LoadAsync());

        Assert.Contains("Proof file not found", exception.Message);
        Assert.Contains(filePath, exception.Message);
    }

    [Fact]
    public async Task LoadAsync_MissingProofProperty_ThrowsException()
    {
        // Arrange
        var invalidEnvelope = new { Version = "1.0", Metadata = new Dictionary<string, string>() };
        var json = JsonSerializer.Serialize(invalidEnvelope);
        var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns(base64Json);
        getEnv("LICENSE_PROOF_FILE").Returns((string?)null);

        var fileExists = Substitute.For<Func<string, bool>>();
        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseValidationException>(
            async () => await loader.LoadAsync());

        Assert.Contains("missing 'Proof' property", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_WithMetadata_PreservesMetadata()
    {
        // Arrange
        var metadata = new Dictionary<string, string>
        {
            { "key1", "value1" },
            { "key2", "value2" }
        };
        var proofEnvelope = CreateTestProofEnvelope("test-product", metadata);
        var json = SerializeProofEnvelope(proofEnvelope);
        var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns(base64Json);
        getEnv("LICENSE_PROOF_FILE").Returns((string?)null);

        var fileExists = Substitute.For<Func<string, bool>>();
        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act
        var result = await loader.LoadAsync();

        // Assert
        Assert.NotNull(result);
        Assert.NotNull(result.Metadata);
        Assert.Equal(2, result.Metadata.Count);
        Assert.Equal("value1", result.Metadata["key1"]);
        Assert.Equal("value2", result.Metadata["key2"]);
    }

    [Fact]
    public async Task LoadAsync_PrioritizesEnvironmentVariableOverFile()
    {
        // Arrange
        var envProofEnvelope = CreateTestProofEnvelope();
        var envJson = SerializeProofEnvelope(envProofEnvelope);
        var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(envJson));

        var fileProofEnvelope = CreateTestProofEnvelope("file-product");
        var fileJson = SerializeProofEnvelope(fileProofEnvelope);

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns(base64Json);
        getEnv("LICENSE_PROOF_FILE").Returns("/some/file.json");

        var fileExists = Substitute.For<Func<string, bool>>();
        fileExists("/some/file.json").Returns(true);

        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();
        readFile("/some/file.json", Arg.Any<CancellationToken>()).Returns(Task.FromResult(fileJson));

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act
        var result = await loader.LoadAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("test-product", result.Proof.ProductId); // From env var, not file
        await readFile.DidNotReceive().Invoke(Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoadAsync_PrioritizesProofFileOverDefaultPath()
    {
        // Arrange
        var fileProofEnvelope = CreateTestProofEnvelope("custom-product");
        var fileJson = SerializeProofEnvelope(fileProofEnvelope);

        var defaultProofEnvelope = CreateTestProofEnvelope("default-product");
        var defaultJson = SerializeProofEnvelope(defaultProofEnvelope);

        var customPath = "/custom/proof.json";
        var defaultPath = "/etc/chainborn/proof.json";

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns((string?)null);
        getEnv("LICENSE_PROOF_FILE").Returns(customPath);

        var fileExists = Substitute.For<Func<string, bool>>();
        fileExists(customPath).Returns(true);
        fileExists(defaultPath).Returns(true);

        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();
        readFile(customPath, Arg.Any<CancellationToken>()).Returns(Task.FromResult(fileJson));
        readFile(defaultPath, Arg.Any<CancellationToken>()).Returns(Task.FromResult(defaultJson));

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act
        var result = await loader.LoadAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Equal("custom-product", result.Proof.ProductId); // From custom path
        await readFile.Received(1).Invoke(customPath, Arg.Any<CancellationToken>());
        await readFile.DidNotReceive().Invoke(defaultPath, Arg.Any<CancellationToken>());
    }

    private ProofEnvelope CreateTestProofEnvelope(string productId = "test-product", Dictionary<string, string>? metadata = null)
    {
        var now = DateTimeOffset.UtcNow;
        var challenge = new ProofChallenge("test-nonce", now.AddMinutes(-5), now.AddHours(1));
        var proof = new LicenseProof(
            new byte[] { 1, 2, 3, 4, 5 },
            new byte[] { 6, 7, 8, 9, 10 },
            productId,
            challenge,
            null
        );
        return new ProofEnvelope(proof, "1.0", metadata);
    }

    private string SerializeProofEnvelope(ProofEnvelope envelope)
    {
        var dto = new
        {
            Proof = new
            {
                ProofBytes = Convert.ToBase64String(envelope.Proof.ProofBytes),
                VerificationKeyBytes = Convert.ToBase64String(envelope.Proof.VerificationKeyBytes),
                envelope.Proof.ProductId,
                Challenge = new
                {
                    envelope.Proof.Challenge.Nonce,
                    envelope.Proof.Challenge.IssuedAt,
                    envelope.Proof.Challenge.ExpiresAt
                },
                envelope.Proof.Metadata
            },
            envelope.Version,
            envelope.Metadata
        };
        return JsonSerializer.Serialize(dto);
    }

    [Fact]
    public async Task LoadAsync_MissingProofBytes_ThrowsException()
    {
        // Arrange
        var invalidEnvelope = new 
        { 
            Proof = new 
            {
                ProofBytes = (string?)null,
                VerificationKeyBytes = "dGVzdA==",
                ProductId = "test-product",
                Challenge = new
                {
                    Nonce = "nonce",
                    IssuedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
                }
            }
        };
        var json = JsonSerializer.Serialize(invalidEnvelope);
        var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns(base64Json);

        var fileExists = Substitute.For<Func<string, bool>>();
        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseValidationException>(
            async () => await loader.LoadAsync());

        Assert.Contains("ProofBytes", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_MissingProductId_ThrowsException()
    {
        // Arrange
        var invalidEnvelope = new 
        { 
            Proof = new 
            {
                ProofBytes = "dGVzdA==",
                VerificationKeyBytes = "dGVzdA==",
                ProductId = (string?)null,
                Challenge = new
                {
                    Nonce = "nonce",
                    IssuedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
                }
            }
        };
        var json = JsonSerializer.Serialize(invalidEnvelope);
        var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns(base64Json);

        var fileExists = Substitute.For<Func<string, bool>>();
        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseValidationException>(
            async () => await loader.LoadAsync());

        Assert.Contains("ProductId", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_MissingVerificationKeyBytes_ThrowsException()
    {
        // Arrange
        var invalidEnvelope = new 
        { 
            Proof = new 
            {
                ProofBytes = "dGVzdA==",
                VerificationKeyBytes = (string?)null,
                ProductId = "test-product",
                Challenge = new
                {
                    Nonce = "nonce",
                    IssuedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
                }
            }
        };
        var json = JsonSerializer.Serialize(invalidEnvelope);
        var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns(base64Json);

        var fileExists = Substitute.For<Func<string, bool>>();
        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseValidationException>(
            async () => await loader.LoadAsync());

        Assert.Contains("VerificationKeyBytes", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_MissingChallenge_ThrowsException()
    {
        // Arrange
        var invalidEnvelope = new 
        { 
            Proof = new 
            {
                ProofBytes = "dGVzdA==",
                VerificationKeyBytes = "dGVzdA==",
                ProductId = "test-product",
                Challenge = (object?)null
            }
        };
        var json = JsonSerializer.Serialize(invalidEnvelope);
        var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns(base64Json);

        var fileExists = Substitute.For<Func<string, bool>>();
        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseValidationException>(
            async () => await loader.LoadAsync());

        Assert.Contains("Challenge", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_MissingNonce_ThrowsException()
    {
        // Arrange
        var invalidEnvelope = new 
        { 
            Proof = new 
            {
                ProofBytes = "dGVzdA==",
                VerificationKeyBytes = "dGVzdA==",
                ProductId = "test-product",
                Challenge = new
                {
                    Nonce = (string?)null,
                    IssuedAt = DateTimeOffset.UtcNow,
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
                }
            }
        };
        var json = JsonSerializer.Serialize(invalidEnvelope);
        var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns(base64Json);

        var fileExists = Substitute.For<Func<string, bool>>();
        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseValidationException>(
            async () => await loader.LoadAsync());

        Assert.Contains("Nonce", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_MissingIssuedAt_ThrowsException()
    {
        // Arrange
        var invalidEnvelope = new 
        { 
            Proof = new 
            {
                ProofBytes = "dGVzdA==",
                VerificationKeyBytes = "dGVzdA==",
                ProductId = "test-product",
                Challenge = new
                {
                    Nonce = "nonce",
                    // IssuedAt omitted - will deserialize to MinValue
                    ExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
                }
            }
        };
        var json = JsonSerializer.Serialize(invalidEnvelope);
        var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns(base64Json);

        var fileExists = Substitute.For<Func<string, bool>>();
        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseValidationException>(
            async () => await loader.LoadAsync());

        Assert.Contains("IssuedAt", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_MissingExpiresAt_ThrowsException()
    {
        // Arrange
        var invalidEnvelope = new 
        { 
            Proof = new 
            {
                ProofBytes = "dGVzdA==",
                VerificationKeyBytes = "dGVzdA==",
                ProductId = "test-product",
                Challenge = new
                {
                    Nonce = "nonce",
                    IssuedAt = DateTimeOffset.UtcNow,
                    // ExpiresAt omitted - will deserialize to MinValue
                }
            }
        };
        var json = JsonSerializer.Serialize(invalidEnvelope);
        var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns(base64Json);

        var fileExists = Substitute.For<Func<string, bool>>();
        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseValidationException>(
            async () => await loader.LoadAsync());

        Assert.Contains("ExpiresAt", exception.Message);
    }

    [Fact]
    public async Task LoadAsync_ExpiresAtBeforeIssuedAt_ThrowsException()
    {
        // Arrange
        var now = DateTimeOffset.UtcNow;
        var invalidEnvelope = new 
        { 
            Proof = new 
            {
                ProofBytes = "dGVzdA==",
                VerificationKeyBytes = "dGVzdA==",
                ProductId = "test-product",
                Challenge = new
                {
                    Nonce = "nonce",
                    IssuedAt = now,
                    ExpiresAt = now.AddHours(-1) // Before IssuedAt
                }
            }
        };
        var json = JsonSerializer.Serialize(invalidEnvelope);
        var base64Json = Convert.ToBase64String(Encoding.UTF8.GetBytes(json));

        var getEnv = Substitute.For<Func<string, string?>>();
        getEnv("LICENSE_PROOF").Returns(base64Json);

        var fileExists = Substitute.For<Func<string, bool>>();
        var readFile = Substitute.For<Func<string, CancellationToken, Task<string>>>();

        var loader = new ProofLoader(_mockLogger, getEnv, fileExists, readFile);

        // Act & Assert
        var exception = await Assert.ThrowsAsync<LicenseValidationException>(
            async () => await loader.LoadAsync());

        Assert.Contains("ExpiresAt", exception.Message);
        Assert.Contains("IssuedAt", exception.Message);
    }
}
