using System.Text;
using System.Text.Json;
using Chainborn.Licensing.Abstractions;
using Xunit;

namespace Chainborn.Licensing.Validator.Tests;

/// <summary>
/// Tests for proof envelope JSON format and test fixtures.
/// Validates that the canonical JSON format can be properly loaded and deserialized.
/// </summary>
public class ProofEnvelopeTests
{
    private static readonly string FixturesPath = Path.Combine(
        GetRepositoryRoot(),
        "tests",
        "fixtures"
    );

    [Fact]
    public void ValidProofEnvelope_LoadsSuccessfully()
    {
        // Arrange
        var fixturePath = Path.Combine(FixturesPath, "valid-proof-envelope.json");
        
        // Act
        var envelope = LoadProofEnvelope(fixturePath);
        
        // Assert
        Assert.NotNull(envelope);
        Assert.Equal("chainborn-sample-app", envelope.ProductId);
        Assert.NotEmpty(envelope.ProofBytes);
        Assert.NotEmpty(envelope.VerificationKeyBytes);
        Assert.NotNull(envelope.Challenge);
        Assert.Equal("MTIzNDU2Nzg5MGFiY2RlZg==", envelope.Challenge.Nonce);
        
        // Validate challenge timestamps
        Assert.True(envelope.Challenge.IssuedAt < envelope.Challenge.ExpiresAt);
        
        // Validate metadata is present
        Assert.NotNull(envelope.Metadata);
        Assert.True(envelope.Metadata!.ContainsKey("tier"));
        Assert.Equal("professional", envelope.Metadata["tier"]);
    }

    [Fact]
    public void ExpiredProofEnvelope_LoadsSuccessfully()
    {
        // Arrange
        var fixturePath = Path.Combine(FixturesPath, "expired-proof-envelope.json");
        
        // Act
        var envelope = LoadProofEnvelope(fixturePath);
        
        // Assert
        Assert.NotNull(envelope);
        Assert.Equal("chainborn-sample-app", envelope.ProductId);
        
        // Validate that the challenge is indeed expired
        var now = DateTimeOffset.UtcNow;
        Assert.True(envelope.Challenge.ExpiresAt < now, 
            "The expired proof envelope should have an ExpiresAt timestamp in the past");
    }

    [Fact]
    public void ValidProofEnvelope_HasValidBase64Encoding()
    {
        // Arrange
        var fixturePath = Path.Combine(FixturesPath, "valid-proof-envelope.json");
        var envelope = LoadProofEnvelope(fixturePath);
        
        // Act & Assert - Should not throw
        Assert.True(envelope.ProofBytes.Length > 0);
        Assert.True(envelope.VerificationKeyBytes.Length > 0);
        
        // Validate the nonce is also valid base64 by checking the challenge nonce
        var nonceBytes = Convert.FromBase64String(envelope.Challenge.Nonce);
        Assert.True(nonceBytes.Length > 0);
    }

    [Fact]
    public void ProofEnvelope_RoundTripSerialization()
    {
        // Arrange
        var fixturePath = Path.Combine(FixturesPath, "valid-proof-envelope.json");
        var originalJson = File.ReadAllText(fixturePath);
        
        // Act - Load and re-serialize
        var envelope = LoadProofEnvelope(fixturePath);
        var serialized = SerializeProofEnvelope(envelope);
        var deserialized = DeserializeProofEnvelope(serialized);
        
        // Assert - Should maintain all critical data
        Assert.Equal(envelope.ProductId, deserialized.ProductId);
        Assert.Equal(envelope.ProofBytes, deserialized.ProofBytes);
        Assert.Equal(envelope.VerificationKeyBytes, deserialized.VerificationKeyBytes);
        Assert.Equal(envelope.Challenge.Nonce, deserialized.Challenge.Nonce);
        Assert.Equal(envelope.Challenge.IssuedAt, deserialized.Challenge.IssuedAt);
        Assert.Equal(envelope.Challenge.ExpiresAt, deserialized.Challenge.ExpiresAt);
    }

    private static LicenseProof LoadProofEnvelope(string filePath)
    {
        var json = File.ReadAllText(filePath);
        return DeserializeProofEnvelope(json);
    }

    private static LicenseProof DeserializeProofEnvelope(string json)
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        // Parse required fields
        var proofBytesBase64 = root.GetProperty("proofBytes").GetString()!;
        var verificationKeyBytesBase64 = root.GetProperty("verificationKeyBytes").GetString()!;
        var productId = root.GetProperty("productId").GetString()!;

        // Parse challenge
        var challenge = root.GetProperty("challenge");
        var nonceBase64 = challenge.GetProperty("nonce").GetString()!;
        var issuedAt = challenge.GetProperty("issuedAt").GetDateTimeOffset();
        var expiresAt = challenge.GetProperty("expiresAt").GetDateTimeOffset();

        // Parse optional metadata
        Dictionary<string, string>? metadata = null;
        if (root.TryGetProperty("metadata", out var metadataElement))
        {
            metadata = new Dictionary<string, string>();
            foreach (var property in metadataElement.EnumerateObject())
            {
                // Handle different value types
                var value = property.Value.ValueKind switch
                {
                    JsonValueKind.String => property.Value.GetString()!,
                    JsonValueKind.Number => property.Value.GetRawText(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    JsonValueKind.Array => property.Value.GetRawText(),
                    _ => property.Value.GetRawText()
                };
                metadata[property.Name] = value;
            }
        }

        return new LicenseProof(
            Convert.FromBase64String(proofBytesBase64),
            Convert.FromBase64String(verificationKeyBytesBase64),
            productId,
            new ProofChallenge(nonceBase64, issuedAt, expiresAt),
            metadata
        );
    }

    private static string SerializeProofEnvelope(LicenseProof proof)
    {
        var envelope = new
        {
            proofBytes = Convert.ToBase64String(proof.ProofBytes),
            verificationKeyBytes = Convert.ToBase64String(proof.VerificationKeyBytes),
            productId = proof.ProductId,
            challenge = new
            {
                nonce = proof.Challenge.Nonce,
                issuedAt = proof.Challenge.IssuedAt.ToString("O"),
                expiresAt = proof.Challenge.ExpiresAt.ToString("O")
            },
            metadata = proof.Metadata
        };

        return JsonSerializer.Serialize(envelope, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });
    }

    private static string GetRepositoryRoot()
    {
        var currentDir = Directory.GetCurrentDirectory();
        while (currentDir != null && !File.Exists(Path.Combine(currentDir, "Chainborn.Licensing.sln")))
        {
            currentDir = Directory.GetParent(currentDir)?.FullName;
        }
        return currentDir ?? throw new InvalidOperationException("Could not find repository root");
    }
}
