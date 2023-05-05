using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using PrimaryAggregatorService.Infrastructure.Api;
using PrimaryAggregatorService.Models;
using PrimaryAggregatorService.Models.Api;
using System.Diagnostics;
using System.Threading.RateLimiting;

namespace PrimaryAggregatorService.Services
{
    public class BackgroundMarketWorkerServices
    {
        private readonly RateLimiter _limiter;
        private readonly DefaultRegionSettings _defaultRegion;
        private readonly ILogger _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly DataBaseService _dataBaseService;
        public BackgroundMarketWorkerServices( [FromServices] IOptions<DefaultRegionSettings> settings
            ,ILoggerFactory loggerFactory, DataBaseService baseService) 
        {
            _logger = loggerFactory.CreateLogger<BackgroundMarketWorkerServices>();
            _loggerFactory = loggerFactory;
            _defaultRegion = settings.Value;
            _dataBaseService = baseService;
            _limiter = new SlidingWindowRateLimiter(
                new SlidingWindowRateLimiterOptions()
                {
                    Window = TimeSpan.FromMinutes(5), // to do Move to settings
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
                Stopwatch stopwatchApi = new Stopwatch();
                Stopwatch stopwatchDataBase = new Stopwatch();
                await _limiter.AcquireAsync(1, token);
                if (token.IsCancellationRequested) { return; }
                stopwatchApi.Start();
                MarketOrderBuilder marketOrderBuilder =
                    new MarketOrderBuilder(15, new List<int> { _defaultRegion.DefaultRegionID }, _loggerFactory);

                RequestApiScheduler<OrderApi> requestApiScheduler =
                    new RequestApiScheduler<OrderApi>(_loggerFactory, marketOrderBuilder);

                var items = await requestApiScheduler.Start();
                stopwatchApi.Stop();
                items = items.Where(x => _defaultRegion.DefaultSystems.Contains(x.SystemId)).ToList();
                if (items.Count == 0) 
                {
                    _logger.LogWarning("{0:f} - The request failed",DateTime.Now);
                    continue;
                }
                stopwatchDataBase.Start();
                await _dataBaseService.AddRangeOrderApi(items);
                stopwatchDataBase.Stop();
                _logger.LogInformation("{0:f}: Api request - {1:f}, Data base insert - {2:f} "
                    , DateTime.Now, stopwatchApi.ElapsedMilliseconds, stopwatchDataBase.ElapsedMilliseconds);
            }
        }
    }
}
