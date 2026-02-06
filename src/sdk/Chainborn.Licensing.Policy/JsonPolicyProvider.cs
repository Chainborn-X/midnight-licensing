using System.Collections.Concurrent;
using System.Text.Json;
using Chainborn.Licensing.Abstractions;

namespace Chainborn.Licensing.Policy;

/// <summary>
/// Implements IPolicyProvider by loading policy JSON files from a configured directory.
/// </summary>
public class JsonPolicyProvider : IPolicyProvider
{
    private readonly string _policyDirectory;
    private readonly ConcurrentDictionary<string, LicensePolicy?> _cache = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public JsonPolicyProvider(string policyDirectory)
    {
        _policyDirectory = policyDirectory ?? throw new ArgumentNullException(nameof(policyDirectory));
    }

    public async Task<LicensePolicy?> GetPolicyAsync(string productId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(productId))
        {
            throw new ArgumentException("Product ID cannot be null or whitespace.", nameof(productId));
        }

        // Check cache first
        if (_cache.TryGetValue(productId, out var cachedPolicy))
        {
            return cachedPolicy;
        }

        // Load from file
        var policyFilePath = Path.Combine(_policyDirectory, $"{productId}.json");
        
        if (!File.Exists(policyFilePath))
        {
            _cache[productId] = null;
            return null;
        }

        var jsonContent = await File.ReadAllTextAsync(policyFilePath, cancellationToken);
        var policyDto = JsonSerializer.Deserialize<PolicyDto>(jsonContent, _jsonOptions);

        if (policyDto == null)
        {
            _cache[productId] = null;
            return null;
        }

        var policy = new LicensePolicy(
            policyDto.ProductId,
            policyDto.RequiredTier,
            (IReadOnlyList<string>)(policyDto.RequiredFeatures ?? new List<string>()),
            policyDto.BindingMode,
            TimeSpan.FromSeconds(policyDto.CacheTtlSeconds),
            policyDto.RevocationModel,
            policyDto.Version
        );

        _cache[productId] = policy;
        return policy;
    }

    private class PolicyDto
    {
        public string ProductId { get; set; } = string.Empty;
        public string RequiredTier { get; set; } = string.Empty;
        public List<string>? RequiredFeatures { get; set; }
        public BindingMode BindingMode { get; set; }
        public int CacheTtlSeconds { get; set; }
        public RevocationModel RevocationModel { get; set; }
        public string Version { get; set; } = string.Empty;
    }
}
