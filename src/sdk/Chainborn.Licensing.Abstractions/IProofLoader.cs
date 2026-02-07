namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Interface for loading proof envelopes from various sources.
/// </summary>
public interface IProofLoader
{
    /// <summary>
    /// Loads a proof envelope from the configured source(s).
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The loaded proof envelope, or null if no proof is found</returns>
    /// <exception cref="LicenseValidationException">Thrown when proof loading fails</exception>
    Task<ProofEnvelope?> LoadAsync(CancellationToken cancellationToken = default);
}
