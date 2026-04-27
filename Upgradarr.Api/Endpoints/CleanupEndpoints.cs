using Microsoft.EntityFrameworkCore;
using Upgradarr.Contracts;
using Upgradarr.Data;
using Upgradarr.Domain.Interfaces;

namespace Upgradarr.Api.Endpoints;

public static class CleanupEndpoints
{
    extension(IEndpointRouteBuilder routes)
    {
        public void MapCleanupEndpoints()
        {
            var cleanupApi = routes.MapGroup("/cleanup");

            cleanupApi.MapGet("/", GetTrackedDownloads).WithName("GetTrackedDownloads");

            cleanupApi.MapGet("/run", RunCleanup).WithName("RunCleanup");
        }

        private static async Task<IResult> GetTrackedDownloads(AppDbContext dbContext) =>
            Results.Ok(
                await dbContext
                    .TrackedDownloads.Select(q => new QueueRecordDto
                    {
                        DownloadId = q.DownloadId,
                        Title = q.Title,
                        Source = q.Source.ToString(),
                        Added = q.Added,
                        RemoveAt = q.RemoveAt,
                    })
                    .ToListAsync()
            );

        private static async Task<IResult> RunCleanup(ICleanupService cleanupService, CancellationToken cancellationToken)
        {
            await cleanupService.PerformCleanupAsync(cancellationToken);
            return Results.Ok();
        }
    }
}
