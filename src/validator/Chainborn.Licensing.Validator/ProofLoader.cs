using System.Text;
using System.Text.Json;
using Chainborn.Licensing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Chainborn.Licensing.Validator;

/// <summary>
/// Loads proof envelopes from environment variables or file system.
/// Supports multiple sources with a priority-based resolution order.
/// </summary>
public class ProofLoader : IProofLoader
{
    private const string EnvVarProof = "LICENSE_PROOF";
    private const string EnvVarProofFile = "LICENSE_PROOF_FILE";
    private const string DefaultProofPath = "/etc/chainborn/proof.json";

    private readonly ILogger<ProofLoader> _logger;
    private readonly Func<string, string?> _getEnvironmentVariable;
    private readonly Func<string, bool> _fileExists;
    private readonly Func<string, CancellationToken, Task<string>> _readFileAsync;

    /// <summary>
    /// Creates a new ProofLoader with default file system and environment variable access.
    /// </summary>
    public ProofLoader(ILogger<ProofLoader> logger)
        : this(
            logger,
            Environment.GetEnvironmentVariable,
            File.Exists,
            (path, ct) => File.ReadAllTextAsync(path, ct))
    {
    }

    /// <summary>
    /// Creates a new ProofLoader with custom environment and file system access (for testing).
    /// </summary>
    /// <param name="logger">Logger instance</param>
    /// <param name="getEnvironmentVariable">Function to retrieve environment variable values</param>
    /// <param name="fileExists">Function to check if a file exists</param>
    /// <param name="readFileAsync">Function to asynchronously read file contents</param>
    public ProofLoader(
        ILogger<ProofLoader> logger,
        Func<string, string?> getEnvironmentVariable,
        Func<string, bool> fileExists,
        Func<string, CancellationToken, Task<string>> readFileAsync)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _getEnvironmentVariable = getEnvironmentVariable ?? throw new ArgumentNullException(nameof(getEnvironmentVariable));
        _fileExists = fileExists ?? throw new ArgumentNullException(nameof(fileExists));
        _readFileAsync = readFileAsync ?? throw new ArgumentNullException(nameof(readFileAsync));
    }

    /// <inheritdoc />
    public async Task<ProofEnvelope?> LoadAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Attempting to load proof envelope");

        // Priority 1: LICENSE_PROOF environment variable (base64-encoded JSON)
        var envProof = _getEnvironmentVariable(EnvVarProof);
        if (!string.IsNullOrWhiteSpace(envProof))
        {
            _logger.LogInformation("Loading proof from {EnvVar} environment variable", EnvVarProof);
            return LoadFromBase64Json(envProof);
        }

        // Priority 2: LICENSE_PROOF_FILE environment variable (file path)
        var envProofFile = _getEnvironmentVariable(EnvVarProofFile);
        if (!string.IsNullOrWhiteSpace(envProofFile))
        {
            _logger.LogInformation("Loading proof from file specified in {EnvVar}: {FilePath}", EnvVarProofFile, envProofFile);
            return await LoadFromFileAsync(envProofFile, cancellationToken);
        }

        // Priority 3: Default fallback path
        if (_fileExists(DefaultProofPath))
        {
            _logger.LogInformation("Loading proof from default path: {DefaultPath}", DefaultProofPath);
            return await LoadFromFileAsync(DefaultProofPath, cancellationToken);
        }

        // No proof found in any location
        var errorMessage = $"No proof envelope found. Checked: " +
                          $"1) {EnvVarProof} environment variable, " +
                          $"2) {EnvVarProofFile} environment variable, " +
                          $"3) {DefaultProofPath}";
        _logger.LogError("{ErrorMessage}", errorMessage);
        throw new LicenseValidationException(errorMessage);
    }

    private ProofEnvelope LoadFromBase64Json(string base64Json)
    {
        try
        {
            var jsonBytes = Convert.FromBase64String(base64Json);
            var json = Encoding.UTF8.GetString(jsonBytes);
            return DeserializeProofEnvelope(json);
        }
        catch (FormatException ex)
        {
            var errorMessage = $"Failed to decode base64 from {EnvVarProof} environment variable";
            _logger.LogError(ex, "{ErrorMessage}", errorMessage);
            throw new LicenseValidationException(errorMessage, ex);
        }
        catch (Exception ex) when (ex is not LicenseValidationException)
        {
            var errorMessage = $"Failed to parse proof envelope from {EnvVarProof} environment variable";
            _logger.LogError(ex, "{ErrorMessage}", errorMessage);
            throw new LicenseValidationException(errorMessage, ex);
        }
    }

    private async Task<ProofEnvelope> LoadFromFileAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            if (!_fileExists(filePath))
            {
                var errorMessage = $"Proof file not found: {filePath}";
                _logger.LogError("{ErrorMessage}", errorMessage);
                throw new LicenseValidationException(errorMessage);
            }

            var json = await _readFileAsync(filePath, cancellationToken);
            _logger.LogDebug("Successfully read proof file: {FilePath}", filePath);
            return DeserializeProofEnvelope(json);
        }
        catch (Exception ex) when (ex is not LicenseValidationException)
        {
            var errorMessage = $"Failed to load proof envelope from file: {filePath}";
            _logger.LogError(ex, "{ErrorMessage}", errorMessage);
            throw new LicenseValidationException(errorMessage, ex);
        }
    }

    private ProofEnvelope DeserializeProofEnvelope(string json)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                AllowTrailingCommas = true
            };

            var envelope = JsonSerializer.Deserialize<ProofEnvelopeDto>(json, options);
            if (envelope == null)
            {
                throw new LicenseValidationException("Deserialized proof envelope is null");
            }

            // Validate required fields
            if (envelope.Proof == null)
            {
                throw new LicenseValidationException("Proof envelope is missing 'Proof' property");
            }

            if (string.IsNullOrWhiteSpace(envelope.Proof.ProofBytes))
            {
                throw new LicenseValidationException("Proof envelope is missing 'ProofBytes' property");
            }

            if (string.IsNullOrWhiteSpace(envelope.Proof.VerificationKeyBytes))
            {
                throw new LicenseValidationException("Proof envelope is missing 'VerificationKeyBytes' property");
            }

            if (string.IsNullOrWhiteSpace(envelope.Proof.ProductId))
            {
                throw new LicenseValidationException("Proof envelope is missing 'ProductId' property");
            }

            if (envelope.Proof.Challenge == null)
            {
                throw new LicenseValidationException("Proof envelope is missing 'Challenge' property");
            }

            if (string.IsNullOrWhiteSpace(envelope.Proof.Challenge.Nonce))
            {
                throw new LicenseValidationException("Proof envelope is missing 'Challenge.Nonce' property");
            }

            // Convert DTO to domain model
            var proof = new LicenseProof(
                ProofBytes: Convert.FromBase64String(envelope.Proof.ProofBytes),
                VerificationKeyBytes: Convert.FromBase64String(envelope.Proof.VerificationKeyBytes),
                ProductId: envelope.Proof.ProductId,
                Challenge: new ProofChallenge(
                    Nonce: envelope.Proof.Challenge.Nonce,
                    IssuedAt: envelope.Proof.Challenge.IssuedAt,
                    ExpiresAt: envelope.Proof.Challenge.ExpiresAt
                ),
                Metadata: envelope.Proof.Metadata
            );

            var proofEnvelope = new ProofEnvelope(
                Proof: proof,
                Version: envelope.Version ?? "1.0",
                Metadata: envelope.Metadata
            );

            _logger.LogInformation("Successfully deserialized proof envelope for product {ProductId}", proof.ProductId);
            return proofEnvelope;
        }
        catch (JsonException ex)
        {
            var errorMessage = "Failed to deserialize proof envelope JSON";
            _logger.LogError(ex, "{ErrorMessage}", errorMessage);
            throw new LicenseValidationException(errorMessage, ex);
        }
        catch (FormatException ex)
        {
            var errorMessage = "Failed to decode base64 proof bytes in envelope";
            _logger.LogError(ex, "{ErrorMessage}", errorMessage);
            throw new LicenseValidationException(errorMessage, ex);
        }
    }

    // DTOs for JSON deserialization
    private class ProofEnvelopeDto
    {
        public ProofDto? Proof { get; set; }
        public string? Version { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    private class ProofDto
    {
        public string? ProofBytes { get; set; }
        public string? VerificationKeyBytes { get; set; }
        public string? ProductId { get; set; }
        public ChallengeDto? Challenge { get; set; }
        public Dictionary<string, string>? Metadata { get; set; }
    }

    private class ChallengeDto
    {
        public string? Nonce { get; set; }
        public DateTimeOffset IssuedAt { get; set; }
        public DateTimeOffset ExpiresAt { get; set; }
    }
}
