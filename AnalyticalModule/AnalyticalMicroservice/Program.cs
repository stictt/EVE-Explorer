using AnalyticalMicroservice.Infrastructure;
using AnalyticalMicroservice.Services;
using AnalyticalMicroservice.Services.GRPC;

internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add services to the container.

        builder.Services.AddControllers();
        // Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen();
        builder.Services.AddSettings(builder.Configuration);

        builder.Services.AddScoped<GRPCTradingVolumeService>();
        builder.Services.AddScoped<GRPCOrderService>();

        builder.Services.AddScoped<RequestOrderService>();
        builder.Services.AddScoped<RequestTradingVolumeService>();

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}