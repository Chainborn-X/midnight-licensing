using Chainborn.Licensing.Abstractions;
using Chainborn.Licensing.Policy;
using Chainborn.Licensing.Validator.Mocks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

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
        
        // Use FileValidationCache if cache directory is specified, otherwise use InMemoryValidationCache
        services.TryAddSingleton<IValidationCache>(sp =>
        {
            if (!string.IsNullOrWhiteSpace(options.CacheDirectory))
            {
                try
                {
                    var logger = sp.GetService<ILogger<FileValidationCache>>();
                    return new FileValidationCache(options.CacheDirectory, options.MaxCacheEntries, logger);
                }
                catch (Exception ex)
                {
                    // If FileValidationCache fails to initialize, fall back to InMemoryValidationCache
                    var loggerFactory = sp.GetService<ILoggerFactory>();
                    var logger = loggerFactory?.CreateLogger(typeof(ServiceCollectionExtensions).FullName ?? "Chainborn.Licensing.Validator");
                    logger?.LogWarning(ex, "Failed to initialize FileValidationCache, falling back to InMemoryValidationCache");
                    return new InMemoryValidationCache();
                }
            }
            else
            {
                return new InMemoryValidationCache();
            }
        });
        
        services.TryAddSingleton<IProofVerifier, MockProofVerifier>();
        services.TryAddSingleton<IProofLoader, ProofLoader>();
        services.TryAddSingleton<IBindingDataCollector, BindingDataCollector>();
        
        services.AddSingleton<ILicenseValidator, LicenseValidator>();

        return services;
    }
}
