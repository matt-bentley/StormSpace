using EventStormingBoard.Server.Controllers;
using EventStormingBoard.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace EventStormingBoard.Server.Tests;

public class AuthControllerTests
{
    private static AuthConfigDto GetDto(ActionResult<AuthConfigDto> result)
    {
        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        return okResult.Value.Should().BeOfType<AuthConfigDto>().Subject;
    }

    [Fact]
    public void GivenNoEntraIdSection_WhenGettingConfig_ThenAuthIsDisabled()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var controller = new AuthController(config);

        // Act
        var result = controller.GetConfig();
        var dto = GetDto(result);

        // Assert
        dto.Enabled.Should().BeFalse();
    }

    [Fact]
    public void GivenAllRequiredValues_WhenGettingConfig_ThenAuthIsEnabled()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EntraId:ClientId"] = "test-client-id",
                ["EntraId:TenantId"] = "test-tenant-id",
                ["EntraId:Scopes:0"] = "api://test/access_as_user",
                ["EntraId:Instance"] = "https://login.microsoftonline.com"
            })
            .Build();
        var controller = new AuthController(config);

        // Act
        var result = controller.GetConfig();
        var dto = GetDto(result);

        // Assert
        dto.Enabled.Should().BeTrue();
        dto.ClientId.Should().Be("test-client-id");
        dto.TenantId.Should().Be("test-tenant-id");
        dto.Instance.Should().Be("https://login.microsoftonline.com");
        dto.Scopes.Should().ContainSingle().Which.Should().Be("api://test/access_as_user");
    }

    [Fact]
    public void GivenEmptyEntraIdValues_WhenGettingConfig_ThenAuthIsDisabled()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EntraId:ClientId"] = "",
                ["EntraId:TenantId"] = "",
            })
            .Build();
        var controller = new AuthController(config);

        // Act
        var result = controller.GetConfig();
        var dto = GetDto(result);

        // Assert
        dto.Enabled.Should().BeFalse();
    }

    [Fact]
    public void GivenMissingScopes_WhenGettingConfig_ThenAuthIsDisabled()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EntraId:ClientId"] = "test-client-id",
                ["EntraId:TenantId"] = "test-tenant-id",
            })
            .Build();
        var controller = new AuthController(config);

        // Act
        var result = controller.GetConfig();
        var dto = GetDto(result);

        // Assert
        dto.Enabled.Should().BeFalse();
    }

    [Fact]
    public void GivenNoInstance_WhenGettingConfig_ThenDefaultsToPublicCloud()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EntraId:ClientId"] = "test-client-id",
                ["EntraId:TenantId"] = "test-tenant-id",
                ["EntraId:Scopes:0"] = "api://test/access_as_user",
            })
            .Build();
        var controller = new AuthController(config);

        // Act
        var result = controller.GetConfig();
        var dto = GetDto(result);

        // Assert
        dto.Enabled.Should().BeTrue();
        dto.Instance.Should().Be("https://login.microsoftonline.com");
    }

    [Fact]
    public void GivenMultipleScopes_WhenGettingConfig_ThenScopesAreSerializedAsArray()
    {
        // Arrange
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EntraId:ClientId"] = "test-client-id",
                ["EntraId:TenantId"] = "test-tenant-id",
                ["EntraId:Scopes:0"] = "api://test/scope1",
                ["EntraId:Scopes:1"] = "api://test/scope2",
            })
            .Build();
        var controller = new AuthController(config);

        // Act
        var result = controller.GetConfig();
        var dto = GetDto(result);

        // Assert
        dto.Enabled.Should().BeTrue();
        dto.Scopes.Should().Equal("api://test/scope1", "api://test/scope2");
    }
}
