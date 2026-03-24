using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Upgradarr.Data.Interceptors;

namespace Upgradarr.Data.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddUpgradarrData()
        {
            services.AddSingleton<DeleteQueueItemInterceptor>();
            services.AddDbContext<AppDbContext>(
                (serviceProvider, options) =>
                {
                    options.UseSqlite("Data Source=/config/app.db;Cache=Shared");
                    options.AddInterceptors(serviceProvider.GetRequiredService<DeleteQueueItemInterceptor>());
                }
            );
            return services;
        }
    }
}
