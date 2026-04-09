namespace EventStormingBoard.Server.Tests;

public class EntraIdConfigValidationTests
{
    [Fact]
    public void Validation_AllEmpty_DoesNotThrow()
    {
        // All empty = auth disabled, which is valid
        var ex = Record.Exception(() => ValidateEntraIdConfig(null, null, null));
        Assert.Null(ex);
    }

    [Fact]
    public void Validation_AllPresent_DoesNotThrow()
    {
        var ex = Record.Exception(() => ValidateEntraIdConfig("client", "tenant", new[] { "scope" }));
        Assert.Null(ex);
    }

    [Fact]
    public void Validation_ClientIdOnly_ThrowsWithMissing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ValidateEntraIdConfig("client", null, null));

        Assert.Contains("Missing: TenantId, Scopes", ex.Message);
    }

    [Fact]
    public void Validation_TenantIdOnly_ThrowsWithMissing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ValidateEntraIdConfig(null, "tenant", null));

        Assert.Contains("Missing: ClientId, Scopes", ex.Message);
    }

    [Fact]
    public void Validation_ScopesOnly_ThrowsWithMissing()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ValidateEntraIdConfig(null, null, new[] { "scope" }));

        Assert.Contains("Missing: ClientId, TenantId", ex.Message);
    }

    [Fact]
    public void Validation_MissingScopesOnly_ThrowsWithScopes()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            ValidateEntraIdConfig("client", "tenant", null));

        Assert.Contains("Missing: Scopes", ex.Message);
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
