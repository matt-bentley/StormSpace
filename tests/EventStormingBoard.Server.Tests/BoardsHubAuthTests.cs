using EventStormingBoard.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EventStormingBoard.Server.Tests;

public class BoardsHubAuthTests
{
    [Fact]
    public void GetAuthenticatedUserName_NameClaim_ReturnsName()
    {
        var claims = new[] { new Claim("name", "Alice Smith") };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var result = BoardsHub.GetAuthenticatedUserName(principal);

        Assert.Equal("Alice Smith", result);
    }

    [Fact]
    public void GetAuthenticatedUserName_PreferredUsername_FallsBack()
    {
        var claims = new[] { new Claim("preferred_username", "alice@contoso.com") };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var result = BoardsHub.GetAuthenticatedUserName(principal);

        Assert.Equal("alice@contoso.com", result);
    }

    [Fact]
    public void GetAuthenticatedUserName_NameAndPreferredUsername_PrefersName()
    {
        var claims = new[]
        {
            new Claim("name", "Alice Smith"),
            new Claim("preferred_username", "alice@contoso.com")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var result = BoardsHub.GetAuthenticatedUserName(principal);

        Assert.Equal("Alice Smith", result);
    }

    [Fact]
    public void GetAuthenticatedUserName_NoHumanReadableClaims_ThrowsHubException()
    {
        var claims = new[] { new Claim("oid", Guid.NewGuid().ToString()) };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        var ex = Assert.Throws<HubException>(() => BoardsHub.GetAuthenticatedUserName(principal));

        Assert.Contains("name", ex.Message);
        Assert.Contains("preferred_username", ex.Message);
    }

    [Fact]
    public void GetAuthenticatedUserName_Unauthenticated_ReturnsNull()
    {
        var identity = new ClaimsIdentity(); // no auth type = not authenticated
        var principal = new ClaimsPrincipal(identity);

        var result = BoardsHub.GetAuthenticatedUserName(principal);

        Assert.Null(result);
    }

    [Fact]
    public void GetAuthenticatedUserName_NullPrincipal_ReturnsNull()
    {
        var result = BoardsHub.GetAuthenticatedUserName(null);

        Assert.Null(result);
    }
}
