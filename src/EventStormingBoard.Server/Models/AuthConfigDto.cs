namespace EventStormingBoard.Server.Models;

public sealed class AuthConfigDto
{
    public bool Enabled { get; set; }
    public string? ClientId { get; set; }
    public string? TenantId { get; set; }
    public string? Instance { get; set; }
    public string[]? Scopes { get; set; }
}
