namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Collects environment identity data for license binding validation.
/// </summary>
public interface IBindingDataCollector
{
    /// <summary>
    /// Collects binding data from the current runtime environment.
    /// </summary>
    /// <returns>A dictionary of binding data key-value pairs.</returns>
    Task<IReadOnlyDictionary<string, string>> CollectAsync(CancellationToken cancellationToken = default);
}
