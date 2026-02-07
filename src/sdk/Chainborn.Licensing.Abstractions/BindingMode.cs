namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Defines how a license is bound to an environment or organization.
/// </summary>
public enum BindingMode
{
    None,
    Organization,
    Environment,
    Attestation
}
