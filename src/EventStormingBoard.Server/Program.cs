using EventStormingBoard.Server.Filters;
using EventStormingBoard.Server.Hubs;
using EventStormingBoard.Server.Models;
using EventStormingBoard.Server.Repositories;
using EventStormingBoard.Server.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Identity.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Validate and configure Entra ID authentication (optional — enabled when config is present)
var entraIdSection = builder.Configuration.GetSection("EntraId");
var entraClientId = entraIdSection["ClientId"];
var entraTenantId = entraIdSection["TenantId"];
var entraScopes = entraIdSection.GetSection("Scopes").Get<string[]>();

var hasClient = !string.IsNullOrEmpty(entraClientId);
var hasTenant = !string.IsNullOrEmpty(entraTenantId);
var hasScopes = entraScopes is { Length: > 0 };
var entraIdEnabled = hasClient && hasTenant && hasScopes;

// Fail fast if partially configured
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

if (entraIdEnabled)
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddMicrosoftIdentityWebApi(options =>
        {
            builder.Configuration.Bind("EntraId", options);
            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    // SignalR sends tokens via query string during WebSocket negotiation
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hub"))
                    {
                        context.Token = accessToken;
                    }
                    return Task.CompletedTask;
                }
            };
        }, options =>
        {
            builder.Configuration.Bind("EntraId", options);
        });

    builder.Services.AddAuthorizationBuilder()
        .SetFallbackPolicy(
            new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
}
else
{
    // Register authentication with no schemes so UseAuthentication() is a safe no-op
    builder.Services.AddAuthentication();
    builder.Services.AddAuthorization();
}

// Add services to the container.

builder.Services.AddControllers()
	.AddJsonOptions(options =>
	{
		options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
	});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddOptions<AzureOpenAIOptions>()
	.Bind(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName))
	.ValidateDataAnnotations()
	.Validate(options => Uri.TryCreate(options.Endpoint, UriKind.Absolute, out _), "Azure OpenAI endpoint must be an absolute URI.")
	.ValidateOnStart();
builder.Services.AddSingleton<IBoardsRepository, BoardsRepository>();
builder.Services.AddSingleton<IBoardStateService, BoardStateService>();
builder.Services.AddSingleton<IBoardEventLog, BoardEventLog>();
builder.Services.AddSingleton<IBoardEventPipeline, BoardEventPipeline>();
builder.Services.AddSingleton<IBoardPresenceService, BoardPresenceService>();
builder.Services.AddSingleton<IAutonomousFacilitatorCoordinator, AutonomousFacilitatorCoordinator>();
builder.Services.AddSingleton<IAgentService, AgentService>();
builder.Services.AddSingleton<AgentToolCallFilter>();
builder.Services.AddHostedService<AutonomousFacilitatorWorker>();
builder.Services.AddMemoryCache();

builder.Services.AddSignalR()
	.AddJsonProtocol(options =>
	{
		options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
	});

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

if (app.Environment.IsDevelopment())
{
	app.UseSwagger();
	app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.MapHub<BoardsHub>("/hub");

app.MapFallbackToFile("/index.html").AllowAnonymous();

app.Run();
