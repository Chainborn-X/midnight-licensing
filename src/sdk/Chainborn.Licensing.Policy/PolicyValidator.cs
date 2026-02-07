using Chainborn.Licensing.Abstractions;

namespace Chainborn.Licensing.Policy;

/// <summary>
/// Static validation for license policies.
/// </summary>
public static class PolicyValidator
{
    /// <summary>
    /// Validates a license policy and returns a list of validation errors.
    /// </summary>
    /// <param name="policy">The policy to validate.</param>
    /// <returns>A list of validation error messages. Empty if valid.</returns>
    public static IReadOnlyList<string> Validate(LicensePolicy policy)
    {
        if (policy == null)
        {
            return new[] { "Policy cannot be null." };
        }

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(policy.ProductId))
        {
            errors.Add("ProductId cannot be empty.");
        }

        if (string.IsNullOrWhiteSpace(policy.RequiredTier))
        {
            errors.Add("RequiredTier cannot be empty.");
        }

        if (policy.CacheTtl <= TimeSpan.Zero)
        {
            errors.Add("CacheTtl must be greater than zero.");
        }

        if (string.IsNullOrWhiteSpace(policy.Version))
        {
            errors.Add("Version cannot be empty.");
        }

        return errors;
    }
}
