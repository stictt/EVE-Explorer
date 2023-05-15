using Grpc.Core;
using Market_orders;

namespace PrimaryAggregatorService.Services.GRPC
{
    public class GRPCOrderService : OrderService.OrderServiceBase
    {
        private readonly DataGenerationService _generationService;
        public GRPCOrderService(DataGenerationService generationService)
        {
            _generationService = generationService;
        }
        
        public override async Task<OrdersResponse> GetOrders(OrdersRequest request, ServerCallContext context)
        {
            return await _generationService.GenerateOrdersResponse(request);
        }
    }
}
