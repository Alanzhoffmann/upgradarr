using Microsoft.EntityFrameworkCore;
using Upgradarr.Contracts;
using Upgradarr.Data;
using Upgradarr.Domain.Enums;

namespace Upgradarr.Api.Endpoints;

public static class UpgradeEndpoints
{
    extension(IEndpointRouteBuilder routes)
    {
        public void MapUpgradeEndpoints()
        {
            var upgradeApi = routes.MapGroup("api/upgrade");

            upgradeApi.MapGet("/", GetUpgradeStates).WithName("GetUpgradeStates");

            upgradeApi.MapGet("/pending", GetPendingUpgradeStates).WithName("GetPendingUpgradeStates");
        }

        private static async Task<IResult> GetUpgradeStates(AppDbContext dbContext) =>
            Results.Ok(
                await dbContext
                    .UpgradeStates.OrderBy(u => u.QueuePosition)
                    .Select(u => new UpgradeStateDto
                    {
                        Id = u.Id,
                        Title = u.Title,
                        ItemType = u.ItemType.ToString(),
                        SearchState = u.SearchState.ToString(),
                        QueuePosition = u.QueuePosition,
                    })
                    .ToListAsync()
            );

        private static async Task<IResult> GetPendingUpgradeStates(AppDbContext dbContext) =>
            Results.Ok(
                await dbContext
                    .UpgradeStates.OrderBy(u => u.QueuePosition)
                    .Where(u => u.SearchState == SearchState.Pending)
                    .Select(u => new UpgradeStateDto
                    {
                        Id = u.Id,
                        Title = u.Title,
                        ItemType = u.ItemType.ToString(),
                        SearchState = u.SearchState.ToString(),
                        QueuePosition = u.QueuePosition,
                    })
                    .ToListAsync()
            );
    }
}
