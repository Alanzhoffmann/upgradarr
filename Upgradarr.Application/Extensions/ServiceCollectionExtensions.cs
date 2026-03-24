using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Upgradarr.Application.Options;
using Upgradarr.Application.Services;
using Upgradarr.Data.Extensions;

namespace Upgradarr.Application.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddApplicationServices()
        {
            services.TryAddSingleton(TimeProvider.System);

            services.AddScoped<CleanupService>();
            services.AddScoped<UpgradeService>();

            services
                .AddOptions<CleanupOptions>()
                .Configure(
                    (CleanupOptions opt, IServiceProvider sp) =>
                    {
                        sp.GetRequiredService<IConfiguration>().GetSection(CleanupOptions.SectionName).Bind(opt);
                    }
                );

            services.AddUpgradarrData();

            return services;
        }
    }
}
