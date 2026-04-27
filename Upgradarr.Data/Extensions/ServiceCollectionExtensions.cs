using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Upgradarr.Data.BackgroundServices;
using Upgradarr.Data.Interfaces;
using Upgradarr.Data.Internal;
using Upgradarr.Data.Options;

namespace Upgradarr.Data.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddData()
        {
            services.AddHostedService<MigrationBackgroundService>();
            services.AddSingleton<IMigrationState, MigrationState>();

            services.AddOptions<DataOptions>().BindConfiguration(DataOptions.SectionName);

            services.AddDbContext<AppDbContext>(
                (serviceProvider, options) =>
                {
                    var dataOptions = serviceProvider.GetRequiredService<IOptionsSnapshot<DataOptions>>().Value;
                    options.UseSqlite(dataOptions.ConnectionString);
                    options.AddInterceptors(serviceProvider.GetServices<IInterceptor>());
                }
            );

            return services;
        }
    }
}
