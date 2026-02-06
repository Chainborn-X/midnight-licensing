namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Validates a license proof against a product policy.
/// </summary>
public interface ILicenseValidator
{
    /// <summary>
    /// Validates the provided license proof within the given context.
    /// </summary>
    Task<LicenseValidationResult> ValidateAsync(
        LicenseProof proof,
        ValidationContext context,
        CancellationToken cancellationToken = default);
}
