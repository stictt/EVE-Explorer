using AnalyticalMicroservice.Models;
using Grpc.Core;
using Grpc.Net.Client;
using Market_orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AnalyticalMicroservice.Services.GRPC
{
    public class GRPCTradingVolumeService
    {
        private readonly ConnercionGRPC _connercion;
        private readonly ILogger _logger;
        public GRPCTradingVolumeService([FromServices] IOptions<ConnercionGRPC> settings
            , ILoggerFactory factory)
        {
            _connercion = settings.Value;
            _logger = factory.CreateLogger<GRPCTradingVolumeService>();
        }

        public async Task<OrdersResponse> GetTradingVolumeAsync(OrdersRequest request)
        {
            _logger.LogInformation("Start grpc Trading Volume request.");
            using var channel = GrpcChannel.ForAddress(_connercion.ConnectionStrings);
            var client = new OrderService.OrderServiceClient(channel);

            return await client.GetOrdersAsync(request);
        }
    }
}
