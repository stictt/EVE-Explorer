using Google.Protobuf.WellKnownTypes;
using Market_orders;
using PrimaryAggregatorService.Models.DataBases;
using static Market_orders.TradingVolumesResponse.Types;

namespace PrimaryAggregatorService.Services
{
    public class DataGenerationService
    {
        private readonly DataBaseService _dataBaseService;
        private readonly ILogger<DataGenerationService> _logger;
        public DataGenerationService(DataBaseService dataBaseService, ILoggerFactory loggerFactory) 
        {
            _logger = loggerFactory.CreateLogger<DataGenerationService>();
            _dataBaseService = dataBaseService;
        }

        public async Task<TradingVolumesResponse> GenerateTradingVolume(TradingVolumeRequest tradingVolume)
        {
            var second = tradingVolume.TimeRange.ToDateTime().Second;
            var time = new TimeSpan(0, 0, second);
            var orders = await _dataBaseService.GetRangeOrderAsync(tradingVolume.TypeId.ToList(),time);

            var orderVolumes = orders.Select(x => new OrderVolume()
            {
                IsBuyOrder = x.IsBuyOrder,
                MinVolume = x.MinVolume,
                Price = x.Price,
                TypeId = x.TypeId,
                VolumeRemain = x.VolumeRemain,
                PackageDate = x.PackageDate.ToTimestamp()
            }).ToList();

            var tradingVolumes = new TradingVolumesResponse();
            tradingVolumes.Volume.AddRange(orderVolumes);
            return tradingVolumes;
        }
        
        public async Task<OrdersResponse> GenerateOrdersResponse(OrdersRequest ordersRequest)
        {
            OrdersResponse result = new OrdersResponse();
            if (ordersRequest.OptionTimeRange == RequestTimeRangeOption.AllTime)
            {
                var orders = await _dataBaseService.GetRangeOrderAsync(ordersRequest.TypeId.ToList(),new TimeSpan(24));
                result.Orders.AddRange(MapOrder(orders));
                return result;
            }
            else if (ordersRequest.OptionTimeRange == RequestTimeRangeOption.Portion)
            {
                var orders = await _dataBaseService.GetRangeOrderAsync(ordersRequest.TypeId.ToList(),
                    new TimeSpan(0,0,(int)ordersRequest.TimeRange.Seconds));
                result.Orders.AddRange(MapOrder(orders));
                return result;
            }
            else
            {
                var orders = await _dataBaseService.GetLastRangeOrderAsync(ordersRequest.TypeId.ToList());
                result.Orders.AddRange(MapOrder(orders));
                return result;
            }
        }

        private List<Market_orders.OrdersResponse.Types.Order> MapOrder(List<OrderDTO> orderDTOs)
        {
            return orderDTOs.Select(x => new OrdersResponse.Types.Order()
            {
                Duration = x.Duration,
                IsBuyOrder = x.IsBuyOrder,
                Issued = Timestamp.FromDateTime(x.Issued),
                LocationId = x.LocationId,
                MinVolume = x.MinVolume,
                OrderId = x.OrderId,
                Price = x.Price,
                Range = x.Range.ToString(),
                SystemId = x.SystemId,
                TypeId = x.TypeId,
                VolumeRemain = x.VolumeRemain,
                VolumeTotal = x.VolumeTotal,
                PackageDate = x.PackageDate.ToTimestamp()

            }).ToList();
        }
    }
}
