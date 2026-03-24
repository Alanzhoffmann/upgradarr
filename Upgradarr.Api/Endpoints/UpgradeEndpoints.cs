using Microsoft.EntityFrameworkCore;
using Upgradarr.Data;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;

namespace Upgradarr.Api.Endpoints;

public static class UpgradeEndpoints
{
    extension(IEndpointRouteBuilder routes)
    {
        public void MapUpgradeEndpoints()
        {
            var upgradeApi = routes.MapGroup("/upgrade");

            upgradeApi.MapGet("/", GetUpgradeStates).WithName("GetUpgradeStates");

            upgradeApi.MapGet("/pending", GetPendingUpgradeStates).WithName("GetPendingUpgradeStates");
        }

        private static async Task<IResult> GetUpgradeStates(AppDbContext dbContext) =>
            Results.Ok(await dbContext.UpgradeStates.OrderBy(u => u.QueuePosition).ToListAsync());

        private static async Task<IResult> GetPendingUpgradeStates(AppDbContext dbContext) =>
            Results.Ok(await dbContext.UpgradeStates.OrderBy(u => u.QueuePosition).Where(u => u.SearchState == SearchState.Pending).ToListAsync());
    }
}
