using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using PrimaryAggregatorService.Infrastructure;
using PrimaryAggregatorService.Infrastructure.Api;
using PrimaryAggregatorService.Models.Api;
using PrimaryAggregatorService.Models.DataBases;
using PrimaryAggregatorService.Services;
using Serilog;
using System.Configuration;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.Configuration.AddJsonFile(Paths.ApiSettingsPath);

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSettings(builder.Configuration);

        var logger = new LoggerConfiguration()
                    .MinimumLevel.Debug()
                    .WriteTo.Console()
                    .CreateLogger();

        builder.Services.AddDbContext<AggregatorContext>(
            options => options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")),
        ServiceLifetime.Singleton);
        
        builder.Services.AddHostedService<AggregatorApiBackgroundService>();
        builder.Services.AddSingleton<DataBaseService>();
        builder.Services.AddSingleton<BackgroundMarketWorkerServices>();
        builder.Services.AddSingleton<AggregatorRepository>();
        builder.Services.AddSingleton<BackgroundOrderDeletionService>();

        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.AddConfiguration(builder.Configuration.GetSection("Logging"));
            loggingBuilder.AddConsole();
        });
        builder.Services.AddSingleton(Log.Logger);

        var app = builder.Build();

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
    public void ConfigureServices(IServiceCollection services)
    {
        var connString = WebApplication.CreateBuilder()
            .Configuration.GetConnectionString("DefaultConnection");


        object value = services.AddDbContext<AggregatorContext>(
            options => options.UseNpgsql(connString));
    }
}