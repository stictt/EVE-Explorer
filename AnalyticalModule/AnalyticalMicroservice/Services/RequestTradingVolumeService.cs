using AnalyticalMicroservice.Models;
using AnalyticalMicroservice.Services.GRPC;
using Market_orders;

namespace AnalyticalMicroservice.Services
{
    public class RequestTradingVolumeService
    {
        private readonly ILogger _logger;
        private readonly GRPCTradingVolumeService _tradingVolumeService;
        public RequestTradingVolumeService(ILoggerFactory factory, GRPCTradingVolumeService gRPCTradingVolume) 
        {
            _logger = factory.CreateLogger<RequestOrderService>();
            _tradingVolumeService = gRPCTradingVolume;
        }

        public async Task<List<TradingVolume>> SendRangeAsync(List<int> typesId,TimeSpan range)
        {
            TradingVolumeRequest request = new TradingVolumeRequest();
            request.TimeRange.Seconds = range.Seconds;
            request.TypeId.AddRange(typesId);

            var orders = await SendAsync(request);
            if (orders == null) { return new List<TradingVolume>(); }

            return ParseTradingVolumes(orders);
        }

        private async Task<TradingVolumesResponse> SendAsync(TradingVolumeRequest request)
        {
            try
            {
                return await _tradingVolumeService.GetTradingVolumeAsync(request);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.Message);
            }
            return null;
        }

        private List<TradingVolume> ParseTradingVolumes(TradingVolumesResponse response)
        {
            List<TradingVolume> result = response.Volume
                .Select(x =>
                {
                    return new TradingVolume()
                    {
                        IsBuyOrder = x.IsBuyOrder,
                        MinVolume = x.MinVolume,
                        PackageDate = x.PackageDate.ToDateTime(),
                        Price = x.Price,
                        TypeId = x.TypeId,
                        VolumeRemain = x.VolumeRemain
                    };
                }).ToList();

            return result;
        }
    }
}
