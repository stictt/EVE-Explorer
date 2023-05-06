using PrimaryAggregatorService.Infrastructure;
using PrimaryAggregatorService.Models;
using System.Threading.RateLimiting;

namespace PrimaryAggregatorService.Services
{
    public class BackgroundOrderDeletionService
    {
        private readonly AggregatorRepository _repository;
        private readonly RateLimiter _limiter;
        private readonly ILogger _logger;
        public BackgroundOrderDeletionService(AggregatorRepository aggregatorRepository, ILoggerFactory loggerFactory) 
        {
            _repository = aggregatorRepository;
            _logger = loggerFactory.CreateLogger<BackgroundOrderDeletionService>();

            _limiter = new SlidingWindowRateLimiter(
                new SlidingWindowRateLimiterOptions()
                {
                    Window = TimeSpan.FromMinutes(60), 
                    SegmentsPerWindow = 1,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 1,
                    PermitLimit = 1,
                    AutoReplenishment = true
                });
        }

        public async Task Start(CancellationToken token) 
        {
            while (!token.IsCancellationRequested)
            {
                await _limiter.AcquireAsync(1, token);
                if (token.IsCancellationRequested) { return; }

                await _repository.DeleteOldOrders();//проверку на исключения
            }
        }
    }
}
