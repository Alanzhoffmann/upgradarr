using Upgradarr.Application.Interfaces;
using Upgradarr.Application.Services;

namespace Upgradarr.Api.Endpoints;

public static class CleanupEndpoints
{
    extension(IEndpointRouteBuilder routes)
    {
        public void MapCleanupEndpoints()
        {
            var cleanupApi = routes.MapGroup("/cleanup");

            cleanupApi
                .MapGet(
                    "/",
                    async (IQueryService queryService, CancellationToken cancellationToken) =>
                        TypedResults.Ok(await queryService.GetTrackedDownloads(cancellationToken))
                )
                .WithName("GetTrackedDownloads");

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
        }
    }
}
