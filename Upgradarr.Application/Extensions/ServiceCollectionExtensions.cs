using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Upgradarr.Application.Options;
using Upgradarr.Application.Services;
using Upgradarr.Data.Extensions;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Integrations.Lidarr.Extensions;
using Upgradarr.Integrations.Radarr.Extensions;
using Upgradarr.Integrations.Sonarr.Extensions;

namespace Upgradarr.Application.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplicationServices()
        {
            services.TryAddSingleton(TimeProvider.System);

            services.AddScoped<CleanupService>();
            services.AddScoped<IUpgradeService, UpgradeService>();

            services
                .AddOptions<CleanupOptions>()
                .Configure(
                    (CleanupOptions opt, IServiceProvider sp) =>
                    {
                        sp.GetRequiredService<IConfiguration>().GetSection(CleanupOptions.SectionName).Bind(opt);
                    }
                );

            services.AddData();

            services.AddRadarr();
            services.AddSonarr();
            services.AddLidarr();

            return services;
        }
    }
}
