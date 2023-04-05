
namespace PrimaryAggregatorService.Infrastructure
{
    public static class SettingsConfigureServiceCollectionExtensions
    {
        public static IServiceCollection AddSettings(
        this IServiceCollection services, ConfigurationManager configuration)
        {
            services.Configure<Settings>(configuration.GetSection("Settings"));
            return services;
        }
    }
}
