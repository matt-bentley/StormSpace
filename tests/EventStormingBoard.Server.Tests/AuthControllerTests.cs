using EventStormingBoard.Server.Controllers;
using EventStormingBoard.Server.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace EventStormingBoard.Server.Tests;

public class AuthControllerTests
{
    private static AuthConfigDto GetDto(ActionResult<AuthConfigDto> result)
    {
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        return Assert.IsType<AuthConfigDto>(okResult.Value);
    }

    [Fact]
    public void GetConfig_NoEntraIdSection_ReturnsDisabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();
        var controller = new AuthController(config);

        var result = controller.GetConfig();
        var dto = GetDto(result);

        Assert.False(dto.Enabled);
    }

    [Fact]
    public void GetConfig_AllValuesPresent_ReturnsEnabled()
    {
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

        var result = controller.GetConfig();
        var dto = GetDto(result);

        Assert.True(dto.Enabled);
        Assert.Equal("test-client-id", dto.ClientId);
        Assert.Equal("test-tenant-id", dto.TenantId);
        Assert.Equal("https://login.microsoftonline.com", dto.Instance);
        Assert.Single(dto.Scopes!);
        Assert.Equal("api://test/access_as_user", dto.Scopes![0]);
    }

    [Fact]
    public void GetConfig_EmptyValues_ReturnsDisabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EntraId:ClientId"] = "",
                ["EntraId:TenantId"] = "",
            })
            .Build();
        var controller = new AuthController(config);

        var result = controller.GetConfig();
        var dto = GetDto(result);

        Assert.False(dto.Enabled);
    }

    [Fact]
    public void GetConfig_MissingScopes_ReturnsDisabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EntraId:ClientId"] = "test-client-id",
                ["EntraId:TenantId"] = "test-tenant-id",
            })
            .Build();
        var controller = new AuthController(config);

        var result = controller.GetConfig();
        var dto = GetDto(result);

        Assert.False(dto.Enabled);
    }

    [Fact]
    public void GetConfig_NoInstance_DefaultsToPublicCloud()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["EntraId:ClientId"] = "test-client-id",
                ["EntraId:TenantId"] = "test-tenant-id",
                ["EntraId:Scopes:0"] = "api://test/access_as_user",
            })
            .Build();
        var controller = new AuthController(config);

        var result = controller.GetConfig();
        var dto = GetDto(result);

        Assert.True(dto.Enabled);
        Assert.Equal("https://login.microsoftonline.com", dto.Instance);
    }

    [Fact]
    public void GetConfig_ScopesSerializeAsArray()
    {
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

        var result = controller.GetConfig();
        var dto = GetDto(result);

        Assert.True(dto.Enabled);
        Assert.Equal(2, dto.Scopes!.Length);
        Assert.Equal("api://test/scope1", dto.Scopes[0]);
        Assert.Equal("api://test/scope2", dto.Scopes[1]);
    }
}
