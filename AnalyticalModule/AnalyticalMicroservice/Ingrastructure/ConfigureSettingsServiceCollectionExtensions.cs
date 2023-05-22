using AnalyticalMicroservice.Models;

namespace AnalyticalMicroservice.Infrastructure
{
    public static class ConfigureSettingsServiceCollectionExtensions
    {
        public static IServiceCollection AddSettings(
        this IServiceCollection services, ConfigurationManager configuration)
        {
            
            services.Configure<ConnercionGRPC>(configuration.GetSection("gRPC"));
            return services;
        }
    }
}
