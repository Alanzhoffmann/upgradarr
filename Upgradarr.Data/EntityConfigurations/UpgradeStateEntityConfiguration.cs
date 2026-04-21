using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Upgradarr.Domain.Entities;
using Upgradarr.Domain.Entities.Radarr;
using Upgradarr.Domain.Entities.Sonarr;
using Upgradarr.Domain.Enums;

namespace Upgradarr.Data.EntityConfigurations;

public class UpgradeStateEntityConfiguration
    : IEntityTypeConfiguration<SonarrUpgradeState>,
        IEntityTypeConfiguration<RadarrUpgradeState>,
        IEntityTypeConfiguration<UpgradeState>
{
    public void Configure(EntityTypeBuilder<UpgradeState> builder)
    {
        builder.HasDiscriminator<RecordSource>("Source").HasValue<SonarrUpgradeState>(RecordSource.Sonarr).HasValue<RadarrUpgradeState>(RecordSource.Radarr);
    }

    public void Configure(EntityTypeBuilder<SonarrUpgradeState> builder)
    {
        builder.ComplexProperty(s => s.Metadata).ToJson(nameof(SonarrUpgradeState.Metadata));
    }

    public void Configure(EntityTypeBuilder<RadarrUpgradeState> builder)
    {
        builder.ComplexProperty(r => r.Metadata).ToJson(nameof(RadarrUpgradeState.Metadata));
    }
}
