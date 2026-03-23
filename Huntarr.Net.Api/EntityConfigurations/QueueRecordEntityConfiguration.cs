using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upgradarr.Apps.Models;

namespace Huntarr.Net.Api.EntityConfigurations;

public class QueueRecordEntityConfiguration : IEntityTypeConfiguration<QueueRecord>
{
    public void Configure(EntityTypeBuilder<QueueRecord> builder)
    {
        builder.HasKey(q => q.DownloadId);

        builder.OwnsMany(
            q => q.ItemScores,
            qb =>
            {
                qb.ToJson();
            }
        );
    }
}
