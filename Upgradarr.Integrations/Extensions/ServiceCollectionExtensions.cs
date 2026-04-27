using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Upgradarr.Integrations.Interceptors;

namespace Upgradarr.Integrations.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddIntegrationBase()
        {
            services.TryAddSingleton<IInterceptor, DeleteQueueItemInterceptor>();
            return services;
        }
    }
}
