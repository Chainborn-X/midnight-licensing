using Chainborn.Licensing.Abstractions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace Chainborn.Licensing.Validator.Tests;

/// <summary>
/// Unit tests for BindingComparator.
/// </summary>
public class BindingComparatorTests
{
    private readonly ILogger<BindingComparator> _mockLogger;
    private readonly BindingComparator _comparator;

    public BindingComparatorTests()
    {
        _mockLogger = Substitute.For<ILogger<BindingComparator>>();
        _comparator = new BindingComparator(_mockLogger);
    }

    #region BindingMode.None Tests

    [Fact]
    public void Validate_WithBindingModeNone_ReturnsValid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["org_id"] = "test-org" };
        var publicInputs = new Dictionary<string, string> { ["org_id"] = "test-org" };

        // Act
        var result = _comparator.Validate(BindingMode.None, bindingData, publicInputs);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_WithBindingModeNone_NoBindingData_ReturnsValid()
    {
        // Arrange & Act
        var result = _comparator.Validate(BindingMode.None, null, null);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    #endregion

    #region BindingMode.Organization Tests - No Public Inputs (Stub Mode)

    [Fact]
    public void Validate_OrganizationMode_NoPublicInputs_WithBindingData_ReturnsValid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> 
        { 
            ["hostname"] = "test-host",
            ["container_id"] = "abc123"
        };

        // Act
        var result = _comparator.Validate(BindingMode.Organization, bindingData, null);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_OrganizationMode_NoPublicInputs_NoBindingData_ReturnsValid()
    {
        // Arrange & Act
        var result = _comparator.Validate(BindingMode.Organization, null, null);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    #endregion

    #region BindingMode.Organization Tests - With Public Inputs (Strict Mode)

    [Fact]
    public void Validate_OrganizationMode_WithPublicInputs_MatchingOrgId_ReturnsValid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["org_id"] = "acme-corp" };
        var publicInputs = new Dictionary<string, string> { ["org_id"] = "acme-corp" };

        // Act
        var result = _comparator.Validate(BindingMode.Organization, bindingData, publicInputs);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_OrganizationMode_WithPublicInputs_MismatchedOrgId_ReturnsInvalid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["org_id"] = "acme-corp" };
        var publicInputs = new Dictionary<string, string> { ["org_id"] = "different-org" };

        // Act
        var result = _comparator.Validate(BindingMode.Organization, bindingData, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("Organization ID mismatch", result.Errors[0]);
        Assert.Contains("acme-corp", result.Errors[0]);
        Assert.Contains("different-org", result.Errors[0]);
    }

    [Fact]
    public void Validate_OrganizationMode_WithPublicInputs_NoBindingData_ReturnsInvalid()
    {
        // Arrange
        var publicInputs = new Dictionary<string, string> { ["org_id"] = "acme-corp" };

        // Act
        var result = _comparator.Validate(BindingMode.Organization, null, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("binding data", result.Errors[0].ToLower());
        Assert.Contains("none was provided", result.Errors[0].ToLower());
    }

    [Fact]
    public void Validate_OrganizationMode_WithPublicInputs_EmptyBindingData_ReturnsInvalid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string>();
        var publicInputs = new Dictionary<string, string> { ["org_id"] = "acme-corp" };

        // Act
        var result = _comparator.Validate(BindingMode.Organization, bindingData, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("binding data", result.Errors[0].ToLower());
    }

    [Fact]
    public void Validate_OrganizationMode_WithPublicInputs_MissingOrgIdInBindingData_ReturnsInvalid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["hostname"] = "test-host" };
        var publicInputs = new Dictionary<string, string> { ["org_id"] = "acme-corp" };

        // Act
        var result = _comparator.Validate(BindingMode.Organization, bindingData, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("org_id", result.Errors[0]);
        Assert.Contains("binding data", result.Errors[0]);
    }

    [Fact]
    public void Validate_OrganizationMode_WithPublicInputs_EmptyOrgIdInBindingData_ReturnsInvalid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["org_id"] = "" };
        var publicInputs = new Dictionary<string, string> { ["org_id"] = "acme-corp" };

        // Act
        var result = _comparator.Validate(BindingMode.Organization, bindingData, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("org_id", result.Errors[0]);
    }

    [Fact]
    public void Validate_OrganizationMode_WithPublicInputs_WhitespaceOrgIdInBindingData_ReturnsInvalid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["org_id"] = "   " };
        var publicInputs = new Dictionary<string, string> { ["org_id"] = "acme-corp" };

        // Act
        var result = _comparator.Validate(BindingMode.Organization, bindingData, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("org_id", result.Errors[0]);
    }

    [Fact]
    public void Validate_OrganizationMode_WithPublicInputs_MissingOrgIdInPublicInputs_ReturnsInvalid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["org_id"] = "acme-corp" };
        var publicInputs = new Dictionary<string, string> { ["other_field"] = "value" };

        // Act
        var result = _comparator.Validate(BindingMode.Organization, bindingData, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("Proof does not contain", result.Errors[0]);
        Assert.Contains("org_id", result.Errors[0]);
    }

    [Fact]
    public void Validate_OrganizationMode_WithPublicInputs_EmptyOrgIdInPublicInputs_ReturnsInvalid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["org_id"] = "acme-corp" };
        var publicInputs = new Dictionary<string, string> { ["org_id"] = "" };

        // Act
        var result = _comparator.Validate(BindingMode.Organization, bindingData, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("org_id", result.Errors[0]);
    }

    #endregion

    #region BindingMode.Environment Tests - No Public Inputs (Stub Mode)

    [Fact]
    public void Validate_EnvironmentMode_NoPublicInputs_WithBindingData_ReturnsValid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> 
        { 
            ["hostname"] = "test-host",
            ["container_id"] = "abc123"
        };

        // Act
        var result = _comparator.Validate(BindingMode.Environment, bindingData, null);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_EnvironmentMode_NoPublicInputs_NoBindingData_ReturnsValid()
    {
        // Arrange & Act
        var result = _comparator.Validate(BindingMode.Environment, null, null);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    #endregion

    #region BindingMode.Environment Tests - With Public Inputs (Strict Mode)

    [Fact]
    public void Validate_EnvironmentMode_WithPublicInputs_MatchingEnvironmentId_ReturnsValid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["environment_id"] = "prod-us-east-1" };
        var publicInputs = new Dictionary<string, string> { ["environment_id"] = "prod-us-east-1" };

        // Act
        var result = _comparator.Validate(BindingMode.Environment, bindingData, publicInputs);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Validate_EnvironmentMode_WithPublicInputs_MismatchedEnvironmentId_ReturnsInvalid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["environment_id"] = "prod-us-east-1" };
        var publicInputs = new Dictionary<string, string> { ["environment_id"] = "dev-us-west-2" };

        // Act
        var result = _comparator.Validate(BindingMode.Environment, bindingData, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("Environment ID mismatch", result.Errors[0]);
        Assert.Contains("prod-us-east-1", result.Errors[0]);
        Assert.Contains("dev-us-west-2", result.Errors[0]);
    }

    [Fact]
    public void Validate_EnvironmentMode_WithPublicInputs_NoBindingData_ReturnsInvalid()
    {
        // Arrange
        var publicInputs = new Dictionary<string, string> { ["environment_id"] = "prod-us-east-1" };

        // Act
        var result = _comparator.Validate(BindingMode.Environment, null, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("binding data", result.Errors[0].ToLower());
        Assert.Contains("none was provided", result.Errors[0].ToLower());
    }

    [Fact]
    public void Validate_EnvironmentMode_WithPublicInputs_EmptyBindingData_ReturnsInvalid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string>();
        var publicInputs = new Dictionary<string, string> { ["environment_id"] = "prod-us-east-1" };

        // Act
        var result = _comparator.Validate(BindingMode.Environment, bindingData, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("binding data", result.Errors[0].ToLower());
    }

    [Fact]
    public void Validate_EnvironmentMode_WithPublicInputs_MissingEnvironmentIdInBindingData_ReturnsInvalid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["hostname"] = "test-host" };
        var publicInputs = new Dictionary<string, string> { ["environment_id"] = "prod-us-east-1" };

        // Act
        var result = _comparator.Validate(BindingMode.Environment, bindingData, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("environment_id", result.Errors[0]);
        Assert.Contains("binding data", result.Errors[0]);
    }

    [Fact]
    public void Validate_EnvironmentMode_WithPublicInputs_EmptyEnvironmentIdInBindingData_ReturnsInvalid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["environment_id"] = "" };
        var publicInputs = new Dictionary<string, string> { ["environment_id"] = "prod-us-east-1" };

        // Act
        var result = _comparator.Validate(BindingMode.Environment, bindingData, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("environment_id", result.Errors[0]);
    }

    [Fact]
    public void Validate_EnvironmentMode_WithPublicInputs_MissingEnvironmentIdInPublicInputs_ReturnsInvalid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["environment_id"] = "prod-us-east-1" };
        var publicInputs = new Dictionary<string, string> { ["other_field"] = "value" };

        // Act
        var result = _comparator.Validate(BindingMode.Environment, bindingData, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("Proof does not contain", result.Errors[0]);
        Assert.Contains("environment_id", result.Errors[0]);
    }

    [Fact]
    public void Validate_EnvironmentMode_WithPublicInputs_EmptyEnvironmentIdInPublicInputs_ReturnsInvalid()
    {
        // Arrange
        var bindingData = new Dictionary<string, string> { ["environment_id"] = "prod-us-east-1" };
        var publicInputs = new Dictionary<string, string> { ["environment_id"] = "" };

        // Act
        var result = _comparator.Validate(BindingMode.Environment, bindingData, publicInputs);

        // Assert
        Assert.False(result.IsValid);
        Assert.Single(result.Errors);
        Assert.Contains("environment_id", result.Errors[0]);
    }

    #endregion

    #region BindingMode.Attestation Tests

    [Fact]
    public void Validate_AttestationMode_ReturnsValid()
    {
        // Arrange & Act
        var result = _comparator.Validate(BindingMode.Attestation, null, null);

        // Assert
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    #endregion
}
