using AnalyticalMicroservice.Models;
using AnalyticalMicroservice.Services.GRPC;
using Google.Protobuf.WellKnownTypes;
using Market_orders;
using System.Linq;

namespace AnalyticalMicroservice.Services
{
    public class RequestOrderService
    {
        private readonly ILogger _logger;
        private readonly GRPCOrderService _orderService;
        public RequestOrderService(ILoggerFactory factory, GRPCOrderService gRPCOrder) 
        {
            _logger = factory.CreateLogger<RequestOrderService>();
            _orderService = gRPCOrder;
        }

        public async Task<List<Order>> SendAllAsync(List<int> typesId = null)
        {
            OrdersRequest request = new OrdersRequest();
            if (typesId == null) {  request.OptionTypeId = RequestTypeIdOption.AllTypeId; }
            else 
            {
                request.OptionTypeId = RequestTypeIdOption.Range;
                request.TypeId.AddRange(typesId);
            }
            request.OptionTimeRange = RequestTimeRangeOption.AllTime;

            var orders = await SendAsync(request);
            if(orders == null) { return new List<Order>(); }

            return ParseOrder(orders);
        }

        public async Task<List<Order>> SendPortionAsync(TimeSpan portionRange, List<int> typesId = null)
        {
            OrdersRequest request = new OrdersRequest();
            if (typesId == null) { request.OptionTypeId = RequestTypeIdOption.AllTypeId; }
            else
            {
                request.OptionTypeId = RequestTypeIdOption.Range;
                request.TypeId.AddRange(typesId);
            }
            request.OptionTimeRange = RequestTimeRangeOption.Portion;
            request.TimeRange.Seconds = portionRange.Seconds;
            var orders = await SendAsync(request);
            if (orders == null) { return new List<Order>(); }

            return ParseOrder(orders);
        }

        public async Task<List<Order>> SendLastDataAsync(List<int> typesId = null)
        {
            OrdersRequest request = new OrdersRequest();
            if (typesId == null) { request.OptionTypeId = RequestTypeIdOption.AllTypeId; }
            else
            {
                request.OptionTypeId = RequestTypeIdOption.Range;
                request.TypeId.AddRange(typesId);
            }
            request.OptionTimeRange = RequestTimeRangeOption.LastData;
            var orders = await SendAsync(request);
            if (orders == null) { return new List<Order>(); }

            return ParseOrder(orders);
        }

        private async Task<OrdersResponse> SendAsync(OrdersRequest ordersRequest)
        {
            try
            {
                return await _orderService.GetOrderAsync(ordersRequest);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return null;
        }

        private List<Order> ParseOrder(OrdersResponse ordersResponse)
        {
            List<Order> result = ordersResponse.Orders
                .Select(x =>
                {
                   return new Order()
                    {
                        Duration = x.Duration,
                        IsBuyOrder = x.IsBuyOrder,
                        Issued = x.Issued.ToDateTime(),
                        LocationId = x.LocationId,
                        MinVolume = x.MinVolume,
                        OrderId = x.OrderId,
                        PackageDate = x.PackageDate.ToDateTime(),
                        Price = x.Price,
                        Range = (RangeOrderMarket)System.Enum.Parse(typeof(RangeOrderMarket), x.Range),
                        SystemId = x.SystemId,
                        TypeId = x.TypeId,
                        VolumeRemain = x.VolumeRemain,
                        VolumeTotal = x.VolumeTotal
                    };
                }).ToList();

            return result;
        }
    }
}
