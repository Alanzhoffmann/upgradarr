using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Integrations.Radarr.Options;

namespace Upgradarr.Integrations.Radarr.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRadarr()
        {
            services.AddHybridCache();

            services
                .AddOptions<RadarrOptions>()
                .Configure(
                    (RadarrOptions opt, IServiceProvider sp) =>
                    {
                        sp.GetRequiredService<IConfiguration>().GetSection(RadarrOptions.SectionName).Bind(opt);
                    }
                );

            services
                .AddHttpClient<RadarrClient>()
                .ConfigureHttpClient(
                    (serviceProvider, client) =>
                    {
                        var options = serviceProvider.GetRequiredService<IOptionsMonitor<RadarrOptions>>().CurrentValue;
                        client.BaseAddress = new Uri(options.BaseUrl);
                        if (!string.IsNullOrEmpty(options.ApiKey))
                        {
                            client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
                        }
                    }
                );

            services.AddKeyedTransient<IQueueManager>(RecordSource.Radarr, (sp, _) => sp.GetRequiredService<RadarrClient>());
            services.AddTransient(sp => sp.GetRequiredKeyedService<IQueueManager>(RecordSource.Radarr));

            services.AddKeyedTransient<IUpgradeManager>(RecordSource.Radarr, (sp, _) => sp.GetRequiredService<RadarrClient>());
            services.AddTransient(sp => sp.GetRequiredKeyedService<IUpgradeManager>(RecordSource.Radarr));

            return services;
        }
    }
}
