using Huntarr.Net.Data.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Huntarr.Net.Data.EntityConfigurations;

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
