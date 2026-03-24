using System.Text.Json.Serialization;
using Huntarr.Net.Api.BackgroundServices;
using Microsoft.EntityFrameworkCore;
using Upgradarr.Application.Extensions;
using Upgradarr.Application.Services;
using Upgradarr.Apps.Radarr.Extensions;
using Upgradarr.Apps.Sonarr.Extensions;
using Upgradarr.Data;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Enums;

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

var cleanupApi = app.MapGroup("/cleanup");
cleanupApi
    .MapGet(
        "/run",
        async (CleanupService cleanupService, CancellationToken cancellationToken) =>
        {
            await cleanupService.PerformCleanupAsync(cancellationToken);
            return Results.Ok();
        }
    )
    .WithName("RunCleanup");

cleanupApi.MapGet("/", async (AppDbContext dbContext) => Results.Ok(await dbContext.TrackedDownloads.ToListAsync())).WithName("GetTrackedDownloads");

var upgradeApi = app.MapGroup("/upgrade");
upgradeApi
    .MapGet("/", async (AppDbContext dbContext) => Results.Ok(await dbContext.UpgradeStates.OrderBy(u => u.QueuePosition).ToListAsync()))
    .WithName("GetUpgradeStates");

upgradeApi
    .MapGet(
        "/pending",
        async (AppDbContext dbContext) =>
            Results.Ok(await dbContext.UpgradeStates.OrderBy(u => u.QueuePosition).Where(u => u.SearchState == SearchState.Pending).ToListAsync())
    )
    .WithName("GetPendingUpgradeStates");

upgradeApi
    .MapPost(
        "/reset",
        async (UpgradeService upgradeService, CancellationToken cancellationToken) =>
        {
            await upgradeService.InitializeUpgradeStatesAsync(cancellationToken);
            return Results.Ok("Upgrade states reinitialized");
        }
    )
    .WithName("ResetUpgradeStates");

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
