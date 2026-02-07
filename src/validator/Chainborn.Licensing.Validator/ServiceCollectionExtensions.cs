using Chainborn.Licensing.Abstractions;
using Chainborn.Licensing.Policy;
using Chainborn.Licensing.Validator.Mocks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Chainborn.Licensing.Validator;

/// <summary>
/// Extension methods for configuring license validation in the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds license validation services to the service collection.
    /// </summary>
    public static IServiceCollection AddLicenseValidation(
        this IServiceCollection services,
        Action<LicenseValidationOptions> configure)
    {
        if (configure == null)
        {
            throw new ArgumentNullException(nameof(configure));
        }

        var options = new LicenseValidationOptions();
        configure(options);

        services.AddSingleton(options);
        
        // Register default implementations if not already registered
        // These can be overridden by calling code before calling AddLicenseValidation
        services.TryAddSingleton<IPolicyProvider>(sp =>
            new JsonPolicyProvider(options.PolicyDirectory));
        services.TryAddSingleton<IValidationCache, InMemoryValidationCache>();
        services.TryAddSingleton<IProofVerifier, MockProofVerifier>();
        
        services.AddSingleton<ILicenseValidator, LicenseValidator>();

        return services;
    }
}
