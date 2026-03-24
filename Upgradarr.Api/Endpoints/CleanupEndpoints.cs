using Microsoft.EntityFrameworkCore;
using Upgradarr.Application.Services;
using Upgradarr.Data;

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

        private static async Task<IResult> GetTrackedDownloads(AppDbContext dbContext) => Results.Ok(await dbContext.TrackedDownloads.ToListAsync());

        private static async Task<IResult> RunCleanup(CleanupService cleanupService, CancellationToken cancellationToken)
        {
            await cleanupService.PerformCleanupAsync(cancellationToken);
            return Results.Ok();
        }
    }
}
