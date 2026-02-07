using Chainborn.Licensing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Chainborn.Licensing.Validator.Mocks;

/// <summary>
/// Stub implementation of IPolicyProvider that returns hardcoded example policies.
/// TODO: Replace with real policy provider that loads from persistent storage.
/// </summary>
public class StubPolicyProvider : IPolicyProvider
{
    private readonly ILogger<StubPolicyProvider> _logger;
    private readonly Dictionary<string, LicensePolicy> _policies;

    public StubPolicyProvider(ILogger<StubPolicyProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _policies = InitializePolicies();
    }

    public Task<LicensePolicy?> GetPolicyAsync(string productId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("StubPolicyProvider: Getting policy for product {ProductId}", productId);
        
        if (_policies.TryGetValue(productId, out var policy))
        {
            return Task.FromResult<LicensePolicy?>(policy);
        }

        _logger.LogWarning("Policy not found for product {ProductId}", productId);
        return Task.FromResult<LicensePolicy?>(null);
    }

    private static Dictionary<string, LicensePolicy> InitializePolicies()
    {
        return new Dictionary<string, LicensePolicy>
        {
            ["example-product-basic"] = new LicensePolicy(
                ProductId: "example-product-basic",
                RequiredTier: "basic",
                RequiredFeatures: Array.Empty<string>(),
                BindingMode: BindingMode.None,
                CacheTtl: TimeSpan.FromMinutes(15),
                RevocationModel: RevocationModel.ValidityByRenewal,
                Version: "1.0.0"
            ),
            ["example-product-pro"] = new LicensePolicy(
                ProductId: "example-product-pro",
                RequiredTier: "pro",
                RequiredFeatures: new[] { "api-access", "advanced-analytics" },
                BindingMode: BindingMode.Organization,
                CacheTtl: TimeSpan.FromMinutes(30),
                RevocationModel: RevocationModel.ValidityByRenewal,
                Version: "1.0.0"
            ),
            ["example-product-enterprise"] = new LicensePolicy(
                ProductId: "example-product-enterprise",
                RequiredTier: "enterprise",
                RequiredFeatures: new[] { "api-access", "advanced-analytics", "white-label", "priority-support" },
                BindingMode: BindingMode.Environment,
                CacheTtl: TimeSpan.FromHours(1),
                RevocationModel: RevocationModel.ValidityByRenewal,
                Version: "1.0.0"
            )
        };
    }
}
