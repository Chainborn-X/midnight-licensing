using Chainborn.Licensing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Chainborn.Licensing.Validator;

/// <summary>
/// Default implementation of IBindingComparator that validates binding data against proof public inputs.
/// </summary>
public class BindingComparator : IBindingComparator
{
    private readonly ILogger<BindingComparator> _logger;

    public BindingComparator(ILogger<BindingComparator> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public BindingValidationResult Validate(
        BindingMode bindingMode,
        IReadOnlyDictionary<string, string>? bindingData,
        IReadOnlyDictionary<string, string>? publicInputs)
    {
        var errors = new List<string>();

        switch (bindingMode)
        {
            case BindingMode.None:
                // No binding validation required
                _logger.LogDebug("Binding mode is None, skipping binding validation");
                return new BindingValidationResult(true, Array.Empty<string>());

            case BindingMode.Organization:
                ValidateOrganizationBinding(bindingData, publicInputs, errors);
                break;

            case BindingMode.Environment:
                ValidateEnvironmentBinding(bindingData, publicInputs, errors);
                break;

            case BindingMode.Attestation:
                // TODO: Implement attestation binding validation when attestation support is added
                _logger.LogWarning("Attestation binding mode is not yet implemented, treating as valid");
                return new BindingValidationResult(true, Array.Empty<string>());

            default:
                errors.Add($"Unknown binding mode: {bindingMode}");
                _logger.LogError("Unknown binding mode: {BindingMode}", bindingMode);
                break;
        }

        bool isValid = errors.Count == 0;
        if (!isValid)
        {
            _logger.LogWarning("Binding validation failed for mode {BindingMode}: {Errors}",
                bindingMode, string.Join("; ", errors));
        }

        return new BindingValidationResult(isValid, errors);
    }

    /// <summary>
    /// Handles stub mode validation when public inputs are not available yet.
    /// Returns true if stub mode applies (validation should pass), false if strict validation should proceed.
    /// </summary>
    private bool HandleStubModeValidation(
        string bindingModeName,
        IReadOnlyDictionary<string, string>? bindingData,
        IReadOnlyDictionary<string, string>? publicInputs)
    {
        if (publicInputs == null || publicInputs.Count == 0)
        {
            // Public inputs not available yet - proof format not finalized
            if (bindingData == null || bindingData.Count == 0)
            {
                _logger.LogWarning(
                    "{BindingMode} binding mode: binding data not collected. " +
                    "This will be enforced once ZK proof format is finalized.", bindingModeName);
            }
            else
            {
                _logger.LogWarning(
                    "{BindingMode} binding validation: proof public inputs not available yet. " +
                    "Validation will be enforced once ZK proof format is finalized. " +
                    "Collected {Count} binding data fields.", bindingModeName, bindingData.Count);
            }
            return true; // Stub mode - allow validation to pass
        }

        return false; // Strict mode - proceed with validation
    }

    private void ValidateOrganizationBinding(
        IReadOnlyDictionary<string, string>? bindingData,
        IReadOnlyDictionary<string, string>? publicInputs,
        List<string> errors)
    {
        _logger.LogDebug("Validating organization binding");

        // TODO: Once we have real ZK proofs with public inputs, we need to validate the org_id
        // against the proof's public inputs. For now, we log that this validation is pending.
        // When the proof format is finalized, we should:
        // 1. Check if bindingData contains 'org_id'
        // 2. Check if publicInputs contains 'org_id'
        // 3. Compare bindingData["org_id"] with publicInputs["org_id"]
        // 4. Fail validation if they don't match

        if (HandleStubModeValidation("Organization", bindingData, publicInputs))
        {
            return; // Stub mode - allow validation to pass
        }

        // Public inputs are available - enforce strict validation
        
        // Check if binding data is present
        if (bindingData == null || bindingData.Count == 0)
        {
            errors.Add("Organization binding mode requires binding data, but none was provided");
            _logger.LogError("Organization binding validation failed: no binding data provided");
            return;
        }

        // Check if org_id is present in binding data
        if (!bindingData.TryGetValue("org_id", out var orgId) || string.IsNullOrWhiteSpace(orgId))
        {
            errors.Add("Organization binding mode requires 'org_id' in binding data");
            _logger.LogError("Organization binding validation failed: 'org_id' not found in binding data");
            return;
        }

        // Check if org_id is present in public inputs
        if (!publicInputs.TryGetValue("org_id", out var proofOrgId) || string.IsNullOrWhiteSpace(proofOrgId))
        {
            errors.Add("Proof does not contain required 'org_id' in public inputs");
            _logger.LogError("Organization binding validation failed: 'org_id' not found in proof public inputs");
            return;
        }

        // Compare org_id from binding data with org_id from proof
        if (!string.Equals(orgId, proofOrgId, StringComparison.Ordinal))
        {
            errors.Add($"Organization ID mismatch: binding data has '{orgId}' but proof has '{proofOrgId}'");
            _logger.LogError(
                "Organization binding validation failed: org_id mismatch. " +
                "Binding data org_id: {BindingOrgId}, Proof org_id: {ProofOrgId}",
                orgId, proofOrgId);
            return;
        }

        _logger.LogInformation("Organization binding validated successfully for org_id: {OrgId}", orgId);
    }

    private void ValidateEnvironmentBinding(
        IReadOnlyDictionary<string, string>? bindingData,
        IReadOnlyDictionary<string, string>? publicInputs,
        List<string> errors)
    {
        _logger.LogDebug("Validating environment binding");

        // TODO: Once we have real ZK proofs with public inputs, we need to validate the environment_id
        // against the proof's public inputs. For now, we log that this validation is pending.
        // When the proof format is finalized, we should:
        // 1. Check if bindingData contains 'environment_id'
        // 2. Check if publicInputs contains 'environment_id'
        // 3. Compare bindingData["environment_id"] with publicInputs["environment_id"]
        // 4. Fail validation if they don't match

        if (HandleStubModeValidation("Environment", bindingData, publicInputs))
        {
            return; // Stub mode - allow validation to pass
        }

        // Public inputs are available - enforce strict validation
        
        // Check if binding data is present
        if (bindingData == null || bindingData.Count == 0)
        {
            errors.Add("Environment binding mode requires binding data, but none was provided");
            _logger.LogError("Environment binding validation failed: no binding data provided");
            return;
        }

        // Check if environment_id is present in binding data
        if (!bindingData.TryGetValue("environment_id", out var environmentId) || string.IsNullOrWhiteSpace(environmentId))
        {
            errors.Add("Environment binding mode requires 'environment_id' in binding data");
            _logger.LogError("Environment binding validation failed: 'environment_id' not found in binding data");
            return;
        }

        // Check if environment_id is present in public inputs
        if (!publicInputs.TryGetValue("environment_id", out var proofEnvironmentId) || string.IsNullOrWhiteSpace(proofEnvironmentId))
        {
            errors.Add("Proof does not contain required 'environment_id' in public inputs");
            _logger.LogError("Environment binding validation failed: 'environment_id' not found in proof public inputs");
            return;
        }

        // Compare environment_id from binding data with environment_id from proof
        if (!string.Equals(environmentId, proofEnvironmentId, StringComparison.Ordinal))
        {
            errors.Add($"Environment ID mismatch: binding data has '{environmentId}' but proof has '{proofEnvironmentId}'");
            _logger.LogError(
                "Environment binding validation failed: environment_id mismatch. " +
                "Binding data environment_id: {BindingEnvironmentId}, Proof environment_id: {ProofEnvironmentId}",
                environmentId, proofEnvironmentId);
            return;
        }

        _logger.LogInformation("Environment binding validated successfully for environment_id: {EnvironmentId}", environmentId);
    }
}
