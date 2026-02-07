namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Controls validation strictness behavior.
/// </summary>
public enum StrictnessMode
{
    /// <summary>All checks must pass. Failures are hard errors.</summary>
    Strict,
    /// <summary>Non-critical failures are logged as warnings.</summary>
    Permissive
}
