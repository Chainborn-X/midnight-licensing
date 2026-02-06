namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Defines the licensing policy for a product.
/// </summary>
public record LicensePolicy(
    string ProductId,
    string RequiredTier,
    IReadOnlyList<string> RequiredFeatures,
    BindingMode BindingMode,
    TimeSpan CacheTtl,
    RevocationModel RevocationModel,
    string Version);
