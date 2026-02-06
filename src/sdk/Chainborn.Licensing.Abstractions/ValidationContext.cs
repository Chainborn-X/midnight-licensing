namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Context for a license validation request.
/// </summary>
public record ValidationContext(
    string ProductId,
    IReadOnlyDictionary<string, string>? BindingData = null,
    StrictnessMode StrictnessMode = StrictnessMode.Strict);
