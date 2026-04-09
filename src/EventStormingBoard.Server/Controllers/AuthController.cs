using EventStormingBoard.Server.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EventStormingBoard.Server.Controllers;

[ApiController]
[Route("api/auth")]
[AllowAnonymous]
public sealed class AuthController : ControllerBase
{
    private readonly IConfiguration _configuration;

    public AuthController(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    [HttpGet("config")]
    public ActionResult<AuthConfigDto> GetConfig()
    {
        var section = _configuration.GetSection("EntraId");
        var clientId = section["ClientId"];
        var tenantId = section["TenantId"];
        var scopes = section.GetSection("Scopes").Get<string[]>();
        var enabled = !string.IsNullOrEmpty(clientId)
                      && !string.IsNullOrEmpty(tenantId)
                      && scopes is { Length: > 0 };

        if (!enabled)
        {
            return Ok(new AuthConfigDto { Enabled = false });
        }

        return Ok(new AuthConfigDto
        {
            Enabled = true,
            ClientId = clientId,
            TenantId = tenantId,
            Instance = section["Instance"] ?? "https://login.microsoftonline.com",
            Scopes = scopes
        });
    }
}
