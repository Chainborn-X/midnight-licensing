namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Defines how license revocation is handled.
/// </summary>
public enum RevocationModel
{
    ExplicitOnChain,
    ValidityByRenewal
}
