using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upgradarr.Domain.Entities;

namespace Upgradarr.Data.EntityConfigurations;

public class QueueRecordEntityConfiguration : IEntityTypeConfiguration<QueueRecord>
{
    public void Configure(EntityTypeBuilder<QueueRecord> builder)
    {
        builder.HasKey(q => q.DownloadId);

        builder.ComplexProperty(q => q.ItemScores, qb => qb.ToJson());
    }
}
