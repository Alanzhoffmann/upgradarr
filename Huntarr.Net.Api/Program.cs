using System.Text.Json.Serialization;
using Huntarr.Net.Api.BackgroundServices;
using Huntarr.Net.Api.Endpoints;
using Microsoft.EntityFrameworkCore;
using Upgradarr.Application.Extensions;
using Upgradarr.Apps.Radarr.Extensions;
using Upgradarr.Apps.Sonarr.Extensions;
using Upgradarr.Data;
using Upgradarr.Domain.Entities;

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

builder.Services.AddRadarr();
builder.Services.AddSonarr();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapCleanupEndpoints();
app.MapUpgradeEndpoints();

if (!Directory.Exists("/config"))
{
    Directory.CreateDirectory("/config");
}

var dbContext = app.Services.GetRequiredService<AppDbContext>();
await dbContext.Database.MigrateAsync();

app.Run();

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, UseStringEnumConverter = true)]
[JsonSerializable(typeof(List<QueueRecord>))]
[JsonSerializable(typeof(UpgradeState))]
[JsonSerializable(typeof(List<UpgradeState>))]
internal partial class AppJsonSerializerContext : JsonSerializerContext { }
