namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// The result of a license validation attempt.
/// </summary>
public record LicenseValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    DateTimeOffset ValidatedAt,
    DateTimeOffset? ExpiresAt = null,
    string? CacheKey = null);
