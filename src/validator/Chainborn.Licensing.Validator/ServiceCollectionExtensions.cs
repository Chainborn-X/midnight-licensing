using Chainborn.Licensing.Abstractions;
using Chainborn.Licensing.Policy;
using Microsoft.Extensions.DependencyInjection;

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
        var options = new LicenseValidationOptions();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<IPolicyProvider>(sp =>
            new JsonPolicyProvider(options.PolicyDirectory));
        services.AddSingleton<ILicenseValidator, LicenseValidator>();

        return services;
    }
}
