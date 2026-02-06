namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Provides license policies for products.
/// </summary>
public interface IPolicyProvider
{
    /// <summary>
    /// Retrieves the license policy for the specified product.
    /// </summary>
    Task<LicensePolicy?> GetPolicyAsync(string productId, CancellationToken cancellationToken = default);
}
