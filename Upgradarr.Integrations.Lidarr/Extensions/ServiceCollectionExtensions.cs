using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;
using Upgradarr.Integrations.Extensions;
using Upgradarr.Integrations.Interfaces;
using Upgradarr.Integrations.Lidarr.Options;

namespace Upgradarr.Integrations.Lidarr.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddLidarr()
        {
            services.AddIntegrationBase();
            services.AddHybridCache();

            services
                .AddOptions<LidarrOptions>()
                .Configure(
                    (LidarrOptions opt, IServiceProvider sp) =>
                    {
                        sp.GetRequiredService<IConfiguration>().GetSection(LidarrOptions.SectionName).Bind(opt);
                    }
                );

            services
                .AddHttpClient<LidarrClient>()
                .ConfigureHttpClient(
                    (serviceProvider, client) =>
                    {
                        var options = serviceProvider.GetRequiredService<IOptionsMonitor<LidarrOptions>>().CurrentValue;
                        client.BaseAddress = new Uri(options.BaseUrl);
                        if (!string.IsNullOrEmpty(options.ApiKey))
                        {
                            client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
                        }
                    }
                );

            services.AddKeyedTransient<IQueueManager>(RecordSource.Lidarr, (sp, _) => sp.GetRequiredService<LidarrClient>());
            services.AddTransient(sp => sp.GetRequiredKeyedService<IQueueManager>(RecordSource.Lidarr));

            services.AddKeyedTransient<IUpgradeManager>(RecordSource.Lidarr, (sp, _) => sp.GetRequiredService<LidarrClient>());
            services.AddTransient(sp => sp.GetRequiredKeyedService<IUpgradeManager>(RecordSource.Lidarr));

            return services;
        }
    }
}
