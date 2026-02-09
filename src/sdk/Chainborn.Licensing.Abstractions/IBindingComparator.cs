namespace Chainborn.Licensing.Abstractions;

/// <summary>
/// Compares binding data against proof public inputs to validate license binding.
/// </summary>
public interface IBindingComparator
{
    /// <summary>
    /// Validates that binding data matches the proof's public inputs according to the binding mode.
    /// </summary>
    /// <param name="bindingMode">The binding mode to enforce</param>
    /// <param name="bindingData">Binding data collected from the runtime environment</param>
    /// <param name="publicInputs">Public inputs from the ZK proof verification</param>
    /// <returns>A result indicating whether the binding is valid, with error messages if not</returns>
    BindingValidationResult Validate(
        BindingMode bindingMode,
        IReadOnlyDictionary<string, string>? bindingData,
        IReadOnlyDictionary<string, string>? publicInputs);
}

/// <summary>
/// Result of binding validation.
/// </summary>
public record BindingValidationResult(
    bool IsValid,
    IReadOnlyList<string> Errors);
