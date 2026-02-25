using Huntarr.Net.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Huntarr.Net.Api;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<QueueRecord> TrackedDownloads => Set<QueueRecord>();
    public DbSet<UpgradeState> UpgradeStates => Set<UpgradeState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
