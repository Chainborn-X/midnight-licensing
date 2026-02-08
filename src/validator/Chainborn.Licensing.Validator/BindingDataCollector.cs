using Chainborn.Licensing.Abstractions;
using Microsoft.Extensions.Logging;

namespace Chainborn.Licensing.Validator;

/// <summary>
/// Collects environment identity data for license binding validation.
/// </summary>
public class BindingDataCollector : IBindingDataCollector
{
    private readonly ILogger<BindingDataCollector> _logger;

    public BindingDataCollector(ILogger<BindingDataCollector> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Collects binding data from the current runtime environment.
    /// </summary>
    public Task<IReadOnlyDictionary<string, string>> CollectAsync(CancellationToken cancellationToken = default)
    {
        var bindingData = new Dictionary<string, string>();

        // Collect hostname
        try
        {
            var hostname = Environment.MachineName;
            if (!string.IsNullOrWhiteSpace(hostname))
            {
                bindingData["hostname"] = hostname;
                _logger.LogDebug("Collected hostname: {Hostname}", hostname);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect hostname");
        }

        // Collect container ID
        try
        {
            var containerId = GetContainerId();
            if (!string.IsNullOrWhiteSpace(containerId))
            {
                bindingData["container_id"] = containerId;
                _logger.LogDebug("Collected container ID: {ContainerId}", containerId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect container ID");
        }

        // Collect Kubernetes namespace and pod name
        try
        {
            var k8sNamespace = Environment.GetEnvironmentVariable("K8S_NAMESPACE") 
                ?? Environment.GetEnvironmentVariable("KUBERNETES_NAMESPACE");
            if (!string.IsNullOrWhiteSpace(k8sNamespace))
            {
                bindingData["k8s_namespace"] = k8sNamespace;
                _logger.LogDebug("Collected K8s namespace: {Namespace}", k8sNamespace);
            }

            var k8sPodName = Environment.GetEnvironmentVariable("K8S_POD_NAME") 
                ?? Environment.GetEnvironmentVariable("KUBERNETES_POD_NAME");
            if (!string.IsNullOrWhiteSpace(k8sPodName))
            {
                bindingData["k8s_pod_name"] = k8sPodName;
                _logger.LogDebug("Collected K8s pod name: {PodName}", k8sPodName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect Kubernetes metadata");
        }

        // Collect custom environment variables prefixed with CHAINBORN_BINDING_
        try
        {
            var envVars = Environment.GetEnvironmentVariables();
            foreach (var key in envVars.Keys)
            {
                if (key == null) continue;
                
                var keyStr = key.ToString();
                if (!string.IsNullOrWhiteSpace(keyStr) && keyStr.StartsWith("CHAINBORN_BINDING_", StringComparison.OrdinalIgnoreCase))
                {
                    var value = envVars[key]?.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        // Remove the prefix and convert to lowercase for consistent key naming
                        var bindingKey = keyStr.Substring("CHAINBORN_BINDING_".Length).ToLowerInvariant();
                        bindingData[bindingKey] = value;
                        _logger.LogDebug("Collected custom binding data: {Key} = {Value}", bindingKey, value);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to collect custom binding environment variables");
        }

        return Task.FromResult<IReadOnlyDictionary<string, string>>(bindingData);
    }

    /// <summary>
    /// Attempts to extract container ID from cgroup or HOSTNAME environment variable.
    /// </summary>
    private string? GetContainerId()
    {
        // Try HOSTNAME environment variable first (common in Docker containers)
        var hostname = Environment.GetEnvironmentVariable("HOSTNAME");
        if (!string.IsNullOrWhiteSpace(hostname) && IsLikelyContainerId(hostname))
        {
            return hostname;
        }

        // Try reading from /proc/self/cgroup (Linux containers)
        var cgroupPath = "/proc/self/cgroup";
        if (File.Exists(cgroupPath))
        {
            try
            {
                var lines = File.ReadAllLines(cgroupPath);
                foreach (var line in lines)
                {
                    // Look for container ID in cgroup paths
                    // Example: 12:pids:/docker/1234567890abcdef
                    // Example: 0::/system.slice/docker-1234567890abcdef.scope
                    var parts = line.Split(':');
                    if (parts.Length >= 3)
                    {
                        var path = parts[2];
                        
                        // Docker pattern
                        var dockerMatch = System.Text.RegularExpressions.Regex.Match(
                            path, @"/docker/([0-9a-f]{12,64})");
                        if (dockerMatch.Success)
                        {
                            return dockerMatch.Groups[1].Value;
                        }

                        // Docker scope pattern
                        var dockerScopeMatch = System.Text.RegularExpressions.Regex.Match(
                            path, @"/docker-([0-9a-f]{12,64})\.scope");
                        if (dockerScopeMatch.Success)
                        {
                            return dockerScopeMatch.Groups[1].Value;
                        }

                        // Kubernetes/containerd pattern
                        var k8sMatch = System.Text.RegularExpressions.Regex.Match(
                            path, @"/kubepods/[^/]+/pod[0-9a-f-]+/([0-9a-f]{12,64})");
                        if (k8sMatch.Success)
                        {
                            return k8sMatch.Groups[1].Value;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to read cgroup file");
            }
        }

        return null;
    }

    /// <summary>
    /// Checks if a string looks like a container ID (12-64 character hex string).
    /// </summary>
    private static bool IsLikelyContainerId(string value)
    {
        if (value.Length < 12 || value.Length > 64)
        {
            return false;
        }

        return System.Text.RegularExpressions.Regex.IsMatch(value, @"^[0-9a-f]{12,64}$", 
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    }
}
