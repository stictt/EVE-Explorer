using AnalyticalMicroservice.Models;
using Grpc.Net.Client;
using Market_orders;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AnalyticalMicroservice.Services.GRPC
{
    public class GRPCOrderService 
    {
        private readonly ConnercionGRPC _connercion;
        private readonly ILogger _logger;
        public GRPCOrderService([FromServices] IOptions<ConnercionGRPC> settings
            , ILoggerFactory factory)
        {
            _connercion = settings.Value;
            _logger = factory.CreateLogger<GRPCOrderService>();
        }

        public async Task<OrdersResponse> GetOrderAsync(OrdersRequest request)
        {
            _logger.LogInformation("Start grpc Order request.");
            using var channel = GrpcChannel.ForAddress(_connercion.ConnectionStrings);
            var client = new OrderService.OrderServiceClient(channel);

            return await client.GetOrdersAsync(request);
        }
    }
}
