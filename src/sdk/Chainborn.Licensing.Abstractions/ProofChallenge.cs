namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// A challenge used to bind a proof to a specific validation request (anti-replay).
/// </summary>
public record ProofChallenge(
    string Nonce,
    DateTimeOffset IssuedAt,
    DateTimeOffset ExpiresAt);
