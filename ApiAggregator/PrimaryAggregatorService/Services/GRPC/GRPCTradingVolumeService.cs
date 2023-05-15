using Grpc.Core;
using Market_orders;

namespace PrimaryAggregatorService.Services.GRPC
{
    public class GRPCTradingVolumeService : TradingVolumeService.TradingVolumeServiceBase
    {
        private readonly DataGenerationService _generationService;
        public GRPCTradingVolumeService(DataGenerationService dataGenerationService)
        {
            _generationService = dataGenerationService;
        }
        
        public async override Task<TradingVolumesResponse> GetTradingVolume(TradingVolumeRequest request, ServerCallContext context)
        {
            return await _generationService.GenerateTradingVolume(request);
        }
    }
}
