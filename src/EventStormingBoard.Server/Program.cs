using EventStormingBoard.Server.Hubs;
using EventStormingBoard.Server.Repositories;
using EventStormingBoard.Server.Services;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
	.AddJsonOptions(options =>
	{
		options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
	});
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddSingleton<IBoardsRepository, BoardsRepository>();
builder.Services.AddSingleton<IBoardStateService, BoardStateService>();
builder.Services.AddSingleton<IBoardEventLog, BoardEventLog>();
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

app.UseAuthorization();

app.MapControllers();

app.MapHub<BoardsHub>("/hub");

app.MapFallbackToFile("/index.html");

app.Run();
