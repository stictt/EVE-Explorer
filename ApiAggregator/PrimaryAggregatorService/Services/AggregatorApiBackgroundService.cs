using PrimaryAggregatorService.Infrastructure;

namespace PrimaryAggregatorService.Services
{
    public class AggregatorApiBackgroundService : BackgroundService
    {
        private readonly BackgroundMarketWorkerServices _marketWorker;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ILogger _logger;
        private readonly BackgroundOrderDeletionService _backgroundOrderDeletion;
        private CancellationToken _token;
        public AggregatorApiBackgroundService(BackgroundMarketWorkerServices marketWorker
            , ILoggerFactory loggerFactory, BackgroundOrderDeletionService backgroundOrderDeletion) 
        {
            _loggerFactory = loggerFactory;
            _marketWorker = marketWorker;
            _logger = loggerFactory.CreateLogger<AggregatorApiBackgroundService>();
            _backgroundOrderDeletion = backgroundOrderDeletion;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _token = stoppingToken;
            StartApiWorkers().NoAwait(_logger);
        }
        
        private async Task StartApiWorkers()
        {
            Task.Run(() => { _marketWorker.Start(_token).NoAwait(_logger); }).NoAwait(_logger);
            Task.Run(() => { _backgroundOrderDeletion.Start(_token).NoAwait(_logger); }).NoAwait(_logger);
        }
    }
}
