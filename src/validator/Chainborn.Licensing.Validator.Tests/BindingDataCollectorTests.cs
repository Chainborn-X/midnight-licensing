using Chainborn.Licensing.Validator;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Chainborn.Licensing.Validator.Tests;

public class BindingDataCollectorTests
{
    private readonly ILogger<BindingDataCollector> _mockLogger;
    private readonly BindingDataCollector _collector;

    public BindingDataCollectorTests()
    {
        _mockLogger = Substitute.For<ILogger<BindingDataCollector>>();
        _collector = new BindingDataCollector(_mockLogger);
    }

    [Fact]
    public async Task CollectAsync_CollectsHostname()
    {
        // Act
        var result = await _collector.CollectAsync();

        // Assert
        Assert.NotNull(result);
        Assert.Contains("hostname", result.Keys);
        Assert.False(string.IsNullOrWhiteSpace(result["hostname"]));
    }

    [Fact]
    public async Task CollectAsync_ReturnsReadOnlyDictionary()
    {
        // Act
        var result = await _collector.CollectAsync();

        // Assert
        Assert.NotNull(result);
        Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(result);
    }

    [Fact]
    public async Task CollectAsync_CollectsCustomEnvironmentVariables()
    {
        // Arrange
        var testValue = "test-org-123";
        Environment.SetEnvironmentVariable("CHAINBORN_BINDING_ORG_ID", testValue);

        try
        {
            // Act
            var result = await _collector.CollectAsync();

            // Assert
            Assert.Contains("org_id", result.Keys);
            Assert.Equal(testValue, result["org_id"]);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("CHAINBORN_BINDING_ORG_ID", null);
        }
    }

    [Fact]
    public async Task CollectAsync_IgnoresNonChainbornPrefixedVariables()
    {
        // Arrange
        Environment.SetEnvironmentVariable("SOME_OTHER_VAR", "value");

        try
        {
            // Act
            var result = await _collector.CollectAsync();

            // Assert
            Assert.DoesNotContain("some_other_var", result.Keys);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("SOME_OTHER_VAR", null);
        }
    }

    [Fact]
    public async Task CollectAsync_CollectsMultipleCustomVariables()
    {
        // Arrange
        Environment.SetEnvironmentVariable("CHAINBORN_BINDING_VAR1", "value1");
        Environment.SetEnvironmentVariable("CHAINBORN_BINDING_VAR2", "value2");
        Environment.SetEnvironmentVariable("CHAINBORN_BINDING_VAR3", "value3");

        try
        {
            // Act
            var result = await _collector.CollectAsync();

            // Assert
            Assert.Contains("var1", result.Keys);
            Assert.Contains("var2", result.Keys);
            Assert.Contains("var3", result.Keys);
            Assert.Equal("value1", result["var1"]);
            Assert.Equal("value2", result["var2"]);
            Assert.Equal("value3", result["var3"]);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("CHAINBORN_BINDING_VAR1", null);
            Environment.SetEnvironmentVariable("CHAINBORN_BINDING_VAR2", null);
            Environment.SetEnvironmentVariable("CHAINBORN_BINDING_VAR3", null);
        }
    }

    [Fact]
    public async Task CollectAsync_CollectsK8sNamespace_WhenSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("K8S_NAMESPACE", "production");

        try
        {
            // Act
            var result = await _collector.CollectAsync();

            // Assert
            Assert.Contains("k8s_namespace", result.Keys);
            Assert.Equal("production", result["k8s_namespace"]);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("K8S_NAMESPACE", null);
        }
    }

    [Fact]
    public async Task CollectAsync_CollectsK8sPodName_WhenSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("K8S_POD_NAME", "my-app-pod-123");

        try
        {
            // Act
            var result = await _collector.CollectAsync();

            // Assert
            Assert.Contains("k8s_pod_name", result.Keys);
            Assert.Equal("my-app-pod-123", result["k8s_pod_name"]);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("K8S_POD_NAME", null);
        }
    }

    [Fact]
    public async Task CollectAsync_PrefersK8sPrefix_OverKubernetesPrefix()
    {
        // Arrange
        Environment.SetEnvironmentVariable("K8S_NAMESPACE", "k8s-value");
        Environment.SetEnvironmentVariable("KUBERNETES_NAMESPACE", "kubernetes-value");

        try
        {
            // Act
            var result = await _collector.CollectAsync();

            // Assert
            Assert.Contains("k8s_namespace", result.Keys);
            Assert.Equal("k8s-value", result["k8s_namespace"]);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("K8S_NAMESPACE", null);
            Environment.SetEnvironmentVariable("KUBERNETES_NAMESPACE", null);
        }
    }

    [Fact]
    public async Task CollectAsync_UsesKubernetesPrefix_WhenK8sPrefixNotSet()
    {
        // Arrange
        Environment.SetEnvironmentVariable("KUBERNETES_NAMESPACE", "kubernetes-value");

        try
        {
            // Act
            var result = await _collector.CollectAsync();

            // Assert
            Assert.Contains("k8s_namespace", result.Keys);
            Assert.Equal("kubernetes-value", result["k8s_namespace"]);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("KUBERNETES_NAMESPACE", null);
        }
    }

    [Fact]
    public async Task CollectAsync_CaseInsensitiveCustomPrefix()
    {
        // Arrange
        Environment.SetEnvironmentVariable("chainborn_binding_test", "lower");
        Environment.SetEnvironmentVariable("CHAINBORN_BINDING_TEST2", "upper");

        try
        {
            // Act
            var result = await _collector.CollectAsync();

            // Assert - Both should be collected
            Assert.Contains("test", result.Keys);
            Assert.Contains("test2", result.Keys);
        }
        finally
        {
            // Cleanup
            Environment.SetEnvironmentVariable("chainborn_binding_test", null);
            Environment.SetEnvironmentVariable("CHAINBORN_BINDING_TEST2", null);
        }
    }
}
