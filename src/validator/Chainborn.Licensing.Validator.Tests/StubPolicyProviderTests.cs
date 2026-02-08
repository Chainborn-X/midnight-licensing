using Chainborn.Licensing.Validator.Mocks;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Chainborn.Licensing.Validator.Tests;

public class StubPolicyProviderTests
{
    private readonly ILogger<StubPolicyProvider> _mockLogger;
    private readonly StubPolicyProvider _provider;

    public StubPolicyProviderTests()
    {
        _mockLogger = Substitute.For<ILogger<StubPolicyProvider>>();
        _provider = new StubPolicyProvider(_mockLogger);
    }

    [Fact]
    public async Task GetPolicyAsync_WithBasicProduct_ReturnsValidPolicy()
    {
        // Act
        var policy = await _provider.GetPolicyAsync("example-product-basic");

        // Assert
        Assert.NotNull(policy);
        Assert.Equal("example-product-basic", policy.ProductId);
        Assert.Equal("basic", policy.RequiredTier);
        Assert.Empty(policy.RequiredFeatures);
    }

    [Fact]
    public async Task GetPolicyAsync_WithProProduct_ReturnsValidPolicy()
    {
        // Act
        var policy = await _provider.GetPolicyAsync("example-product-pro");

        // Assert
        Assert.NotNull(policy);
        Assert.Equal("example-product-pro", policy.ProductId);
        Assert.Equal("pro", policy.RequiredTier);
        Assert.Contains("api-access", policy.RequiredFeatures);
        Assert.Contains("advanced-analytics", policy.RequiredFeatures);
    }

    [Fact]
    public async Task GetPolicyAsync_WithEnterpriseProduct_ReturnsValidPolicy()
    {
        // Act
        var policy = await _provider.GetPolicyAsync("example-product-enterprise");

        // Assert
        Assert.NotNull(policy);
        Assert.Equal("example-product-enterprise", policy.ProductId);
        Assert.Equal("enterprise", policy.RequiredTier);
        Assert.Contains("api-access", policy.RequiredFeatures);
        Assert.Contains("advanced-analytics", policy.RequiredFeatures);
        Assert.Contains("white-label", policy.RequiredFeatures);
        Assert.Contains("priority-support", policy.RequiredFeatures);
    }

    [Fact]
    public async Task GetPolicyAsync_WithUnknownProduct_ReturnsNull()
    {
        // Act
        var policy = await _provider.GetPolicyAsync("unknown-product");

        // Assert
        Assert.Null(policy);
    }

    [Fact]
    public async Task GetPolicyAsync_WithBasicProduct_HasCorrectCacheTtl()
    {
        // Act
        var policy = await _provider.GetPolicyAsync("example-product-basic");

        // Assert
        Assert.NotNull(policy);
        Assert.Equal(TimeSpan.FromMinutes(15), policy.CacheTtl);
    }

    [Fact]
    public async Task GetPolicyAsync_WithProProduct_HasCorrectCacheTtl()
    {
        // Act
        var policy = await _provider.GetPolicyAsync("example-product-pro");

        // Assert
        Assert.NotNull(policy);
        Assert.Equal(TimeSpan.FromMinutes(30), policy.CacheTtl);
    }

    [Fact]
    public async Task GetPolicyAsync_WithEnterpriseProduct_HasCorrectCacheTtl()
    {
        // Act
        var policy = await _provider.GetPolicyAsync("example-product-enterprise");

        // Assert
        Assert.NotNull(policy);
        Assert.Equal(TimeSpan.FromHours(1), policy.CacheTtl);
    }

    [Fact]
    public async Task GetPolicyAsync_WithNullProductId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetPolicyAsync(null!));
    }

    [Fact]
    public async Task GetPolicyAsync_WithEmptyProductId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetPolicyAsync(string.Empty));
    }

    [Fact]
    public async Task GetPolicyAsync_WithWhitespaceProductId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetPolicyAsync("   "));
    }

    [Fact]
    public async Task GetPolicyAsync_WithPathTraversalAttempt_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetPolicyAsync("../secret"));
    }

    [Fact]
    public async Task GetPolicyAsync_WithSlashInProductId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetPolicyAsync("product/malicious"));
    }

    [Fact]
    public async Task GetPolicyAsync_WithBackslashInProductId_ThrowsArgumentException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => _provider.GetPolicyAsync("product\\malicious"));
    }
}
