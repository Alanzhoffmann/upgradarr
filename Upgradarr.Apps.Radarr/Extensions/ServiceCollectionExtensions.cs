using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Upgradarr.Apps.Radarr.Options;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;

namespace Upgradarr.Apps.Radarr.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRadarr()
        {
            services.AddHybridCache();

            services.AddOptions<RadarrOptions>().BindConfiguration(RadarrOptions.SectionName);

            services
                .AddHttpClient<RadarrClient>()
                .ConfigureHttpClient(
                    (serviceProvider, client) =>
                    {
                        var options = serviceProvider.GetRequiredService<IOptionsSnapshot<RadarrOptions>>().Value;
                        client.BaseAddress = new Uri(options.BaseUrl);
                        if (!string.IsNullOrEmpty(options.ApiKey))
                        {
                            client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
                        }
                    }
                );

            services.AddKeyedScoped<IQueueManager>(RecordSource.Radarr, (sp, _) => sp.GetRequiredService<RadarrClient>());
            services.AddScoped(sp => sp.GetRequiredKeyedService<IQueueManager>(RecordSource.Radarr));

            services.AddKeyedScoped<IUpgradeManager>(RecordSource.Radarr, (sp, _) => sp.GetRequiredService<RadarrClient>());
            services.AddScoped(sp => sp.GetRequiredKeyedService<IUpgradeManager>(RecordSource.Radarr));

            return services;
        }
    }
}
