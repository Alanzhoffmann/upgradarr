using Huntarr.Net.Data.EntityConfigurations;
using Huntarr.Net.Data.Models;
using Microsoft.EntityFrameworkCore;

namespace Huntarr.Net.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options)
        : base(options) { }

    public DbSet<QueueRecord> TrackedDownloads => Set<QueueRecord>();
    public DbSet<UpgradeState> UpgradeStates => Set<UpgradeState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new QueueRecordEntityConfiguration());
    }
}
