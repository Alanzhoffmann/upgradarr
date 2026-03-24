using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Upgradarr.Apps.Enums;
using Upgradarr.Apps.Interfaces;
using Upgradarr.Apps.Radarr.Options;

namespace Upgradarr.Apps.Radarr.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddRadarr()
        {
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
                        var options = serviceProvider.GetRequiredService<IOptionsSnapshot<RadarrOptions>>().Value;
                        client.BaseAddress = new Uri(options.BaseUrl);
                        if (!string.IsNullOrEmpty(options.ApiKey))
                        {
                            client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
                        }
                    }
                );

            services.AddKeyedScoped<IQueueManager>(RecordSource.Radarr, (sp, _) => sp.GetRequiredService<RadarrClient>());

            return services;
        }
    }
}
