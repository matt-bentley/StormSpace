using EventStormingBoard.Server.Hubs;
using Microsoft.AspNetCore.SignalR;
using System.Security.Claims;

namespace EventStormingBoard.Server.Tests;

public class BoardsHubAuthTests
{
    [Fact]
    public void GivenNameClaim_WhenGettingAuthenticatedUserName_ThenReturnsName()
    {
        // Arrange
        var claims = new[] { new Claim("name", "Alice Smith") };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = BoardsHub.GetAuthenticatedUserName(principal);

        // Assert
        result.Should().Be("Alice Smith");
    }

    [Fact]
    public void GivenPreferredUsernameClaim_WhenGettingAuthenticatedUserName_ThenFallsBackToPreferredUsername()
    {
        // Arrange
        var claims = new[] { new Claim("preferred_username", "alice@contoso.com") };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = BoardsHub.GetAuthenticatedUserName(principal);

        // Assert
        result.Should().Be("alice@contoso.com");
    }

    [Fact]
    public void GivenNameAndPreferredUsernameClaims_WhenGettingAuthenticatedUserName_ThenPrefersName()
    {
        // Arrange
        var claims = new[]
        {
            new Claim("name", "Alice Smith"),
            new Claim("preferred_username", "alice@contoso.com")
        };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = BoardsHub.GetAuthenticatedUserName(principal);

        // Assert
        result.Should().Be("Alice Smith");
    }

    [Fact]
    public void GivenNoHumanReadableClaims_WhenGettingAuthenticatedUserName_ThenThrowsHubException()
    {
        // Arrange
        var claims = new[] { new Claim("oid", Guid.NewGuid().ToString()) };
        var identity = new ClaimsIdentity(claims, "Bearer");
        var principal = new ClaimsPrincipal(identity);
        Action act = () => BoardsHub.GetAuthenticatedUserName(principal);

        // Act
        var ex = act.Should().Throw<HubException>().Which;

        // Assert
        ex.Message.Should().Contain("name").And.Contain("preferred_username");
    }

    [Fact]
    public void GivenUnauthenticatedPrincipal_WhenGettingAuthenticatedUserName_ThenReturnsNull()
    {
        // Arrange
        var identity = new ClaimsIdentity(); // no auth type = not authenticated
        var principal = new ClaimsPrincipal(identity);

        // Act
        var result = BoardsHub.GetAuthenticatedUserName(principal);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void GivenNullPrincipal_WhenGettingAuthenticatedUserName_ThenReturnsNull()
    {
        // Arrange

        // Act
        var result = BoardsHub.GetAuthenticatedUserName(null);

        // Assert
        result.Should().BeNull();
    }
}
