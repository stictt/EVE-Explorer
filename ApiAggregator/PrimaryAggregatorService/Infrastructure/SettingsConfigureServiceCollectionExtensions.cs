﻿
using PrimaryAggregatorService.Models;

namespace PrimaryAggregatorService.Infrastructure
{
    public static class ConfigureSettingsServiceCollectionExtensions
    {
        public static IServiceCollection AddSettings(
        this IServiceCollection services, ConfigurationManager configuration)
        {
            services.Configure<DefaultRegionSettings>(configuration.GetSection("DefaultRegion"));
            return services;
        }
    }
}
