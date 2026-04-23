using System.Text.Json.Serialization;
using Upgradarr.Api.Endpoints;
using Upgradarr.Api.Middleware;
using Upgradarr.Application.BackgroundServices;
using Upgradarr.Application.Extensions;
using Upgradarr.Contracts;

var builder = WebApplication.CreateSlimBuilder(args);

// Configure logging with timestamps
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.IncludeScopes = true;
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss.fff zzz ";
});

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.TypeInfoResolverChain.Insert(0, AppJsonSerializerContext.Default);
});

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddHostedService<UpgradeBackgroundService>();
builder.Services.AddHostedService<CleanupBackgroundService>();

builder.Services.AddApplicationServices();

var app = builder.Build();

app.UseBlazorFrameworkFiles();
app.UseStaticFiles();

app.UseMiddleware<MigrationMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var apiGroup = app.MapGroup("/api");
apiGroup.MapCleanupEndpoints();
apiGroup.MapUpgradeEndpoints();

// Point unmatched requests to the Blazor index
app.MapFallbackToFile("index.html");

app.Run();

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(List<QueueRecordDto>))]
[JsonSerializable(typeof(UpgradeStateDto))]
[JsonSerializable(typeof(List<UpgradeStateDto>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext { }
