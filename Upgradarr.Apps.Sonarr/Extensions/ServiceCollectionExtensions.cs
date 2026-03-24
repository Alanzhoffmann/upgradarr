using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Upgradarr.Apps.Sonarr.Options;
using Upgradarr.Domain.Enums;
using Upgradarr.Domain.Interfaces;

namespace Upgradarr.Apps.Sonarr.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSonarr()
        {
            services
                .AddOptions<SonarrOptions>()
                .Configure(
                    (SonarrOptions opt, IServiceProvider sp) =>
                    {
                        sp.GetRequiredService<IConfiguration>().GetSection(SonarrOptions.SectionName).Bind(opt);
                    }
                );

            services
                .AddHttpClient<SonarrClient>()
                .ConfigureHttpClient(
                    (serviceProvider, client) =>
                    {
                        var options = serviceProvider.GetRequiredService<IOptionsSnapshot<SonarrOptions>>().Value;
                        client.BaseAddress = new Uri(options.BaseUrl);
                        if (!string.IsNullOrEmpty(options.ApiKey))
                        {
                            client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
                        }
                    }
                );

            services.AddKeyedScoped<IQueueManager>(RecordSource.Sonarr, (sp, _) => sp.GetRequiredService<SonarrClient>());
            services.AddTransient(sp => sp.GetRequiredKeyedService<IQueueManager>(RecordSource.Sonarr));

            return services;
        }
    }
}
