namespace EventStormingBoard.Server.Tests;

public class EntraIdConfigValidationTests
{
    [Fact]
    public void GivenEmptyConfig_WhenValidatingEntraIdConfig_ThenDoesNotThrow()
    {
        // Arrange
        Action act = () => ValidateEntraIdConfig(null, null, null);

        // Act

        // Assert
        // All empty = auth disabled, which is valid
        act.Should().NotThrow();
    }

    [Fact]
    public void GivenCompleteConfig_WhenValidatingEntraIdConfig_ThenDoesNotThrow()
    {
        // Arrange
        Action act = () => ValidateEntraIdConfig("client", "tenant", new[] { "scope" });

        // Act

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void GivenOnlyClientId_WhenValidatingEntraIdConfig_ThenThrowsWithMissingTenantIdAndScopes()
    {
        // Arrange
        Action act = () => ValidateEntraIdConfig("client", null, null);

        // Act
        var ex = act.Should().Throw<InvalidOperationException>().Which;

        // Assert
        ex.Message.Should().Contain("Missing: TenantId, Scopes");
    }

    [Fact]
    public void GivenOnlyTenantId_WhenValidatingEntraIdConfig_ThenThrowsWithMissingClientIdAndScopes()
    {
        // Arrange
        Action act = () => ValidateEntraIdConfig(null, "tenant", null);

        // Act
        var ex = act.Should().Throw<InvalidOperationException>().Which;

        // Assert
        ex.Message.Should().Contain("Missing: ClientId, Scopes");
    }

    [Fact]
    public void GivenOnlyScopes_WhenValidatingEntraIdConfig_ThenThrowsWithMissingClientIdAndTenantId()
    {
        // Arrange
        Action act = () => ValidateEntraIdConfig(null, null, new[] { "scope" });

        // Act
        var ex = act.Should().Throw<InvalidOperationException>().Which;

        // Assert
        ex.Message.Should().Contain("Missing: ClientId, TenantId");
    }

    [Fact]
    public void GivenMissingScopes_WhenValidatingEntraIdConfig_ThenThrowsWithMissingScopes()
    {
        // Arrange
        Action act = () => ValidateEntraIdConfig("client", "tenant", null);

        // Act
        var ex = act.Should().Throw<InvalidOperationException>().Which;

        // Assert
        ex.Message.Should().Contain("Missing: Scopes");
    }

    /// <summary>
    /// Mirrors the validation logic in Program.cs — kept in sync manually.
    /// </summary>
    private static void ValidateEntraIdConfig(string? clientId, string? tenantId, string[]? scopes)
    {
        var hasClient = !string.IsNullOrEmpty(clientId);
        var hasTenant = !string.IsNullOrEmpty(tenantId);
        var hasScopes = scopes is { Length: > 0 };
        var entraIdEnabled = hasClient && hasTenant && hasScopes;

        if ((hasClient || hasTenant || hasScopes) && !entraIdEnabled)
        {
            var missing = new List<string>();
            if (!hasClient) missing.Add("ClientId");
            if (!hasTenant) missing.Add("TenantId");
            if (!hasScopes) missing.Add("Scopes");
            throw new InvalidOperationException(
                $"EntraId configuration is incomplete. Missing: {string.Join(", ", missing)}. " +
                "Either provide all of ClientId, TenantId, and Scopes, or remove the EntraId section entirely to disable authentication.");
        }
    }
}
