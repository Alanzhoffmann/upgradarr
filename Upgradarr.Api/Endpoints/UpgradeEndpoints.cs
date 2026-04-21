using Upgradarr.Application.Interfaces;

namespace Upgradarr.Api.Endpoints;

public static class UpgradeEndpoints
{
    extension(IEndpointRouteBuilder routes)
    {
        public void MapUpgradeEndpoints()
        {
            var upgradeApi = routes.MapGroup("/upgrade");

            upgradeApi
                .MapGet(
                    "/",
                    async (IQueryService queryService, CancellationToken cancellationToken) =>
                        TypedResults.Ok(await queryService.GetUpgradeStates(cancellationToken: cancellationToken))
                )
                .WithName("GetUpgradeStates");

            upgradeApi
                .MapGet(
                    "/pending",
                    async (IQueryService queryService, CancellationToken cancellationToken) =>
                        TypedResults.Ok(await queryService.GetUpgradeStates(pendingOnly: true, cancellationToken: cancellationToken))
                )
                .WithName("GetPendingUpgradeStates");
        }
    }
}
