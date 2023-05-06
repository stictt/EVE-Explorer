using Newtonsoft.Json.Linq;
using PrimaryAggregatorService.Infrastructure.Api;
using PrimaryAggregatorService.Infrastructure.Exceptions;
using PrimaryAggregatorService.Infrastructure.Interface;
using PrimaryAggregatorService.Models.Api;
using System.Threading.RateLimiting;

namespace PrimaryAggregatorService.Services
{
#pragma warning disable CS4014 

    public class RequestApiScheduler<T>
    {
        private List<BaseResponseHttp> _resultRequests = new();
        private Dictionary<IPlanRequest, Task<BaseResponseHttp>> _mapRequest = new();
        private CancellationTokenSource _tokenSource = new();
        private BuilderRequestScheduler _builderRequest;
        private RateLimiter _limiter;
        private ILogger _logger;
        private ILoggerFactory _iLoggerFactory;

        private int _maxRetryCount = 5;
        private int _retryCount = 0;
        private bool IsError = false;

        public RequestApiScheduler(ILoggerFactory loggerFactory, BuilderRequestScheduler builderRequestScheduler)
        {
            _logger = loggerFactory.CreateLogger<RequestApiScheduler<T>>();
            _iLoggerFactory = loggerFactory;
            _builderRequest = builderRequestScheduler;
            _limiter = builderRequestScheduler.GetLimiter();
        }

        public async Task<List<T>> Start()
        {
            _builderRequest.GetPlanRequests()
                 .ForEach(x => _mapRequest.Add(x,null));
            while (!_tokenSource.IsCancellationRequested)
            {
                await ExecuteRequestPlan();
                await ExecuteResponses();
                if (await ErrorChecking()) 
                { 
                    _resultRequests.Clear();
                    _logger.LogWarning("Request operation failed - {0:f}",DateTime.Now);
                    break;
                }
                CheckingСompleted();
            }
            _logger.LogInformation("Result count orders page {0}", _resultRequests.Count);
            return GetResult();
        }

        private List<T> GetResult()
        {
            List<T> values = new List<T>();
            _resultRequests.ForEach(x =>
            {
                JToken jToken = JToken.Parse(x.Message);
                values.AddRange(jToken.ToObject<List<T>>()); //To DO переделать 
            });
            return values;
        }

        private async Task ExecuteRequestPlan()
        {
            foreach (var item in _mapRequest.ToList())
            {
                await _limiter.AcquireAsync(1,_tokenSource.Token);
                if (_tokenSource.IsCancellationRequested) { return; }
                var http = new BaseRequestExecutorApi(_iLoggerFactory, _builderRequest.CheckResponseAndThrow);
                _mapRequest[item.Key] = http.Execute(item.Key, _tokenSource.Token);
            }
        }

        private void CheckingСompleted()
        {
            if (_mapRequest.Count == 0)
            {
                _tokenSource.Cancel();
            }
        }

        private async ValueTask<bool> ErrorChecking()
        {
            if (_retryCount >= _maxRetryCount)
            {
                return true;
            }
            if (IsError)
            {
                await Task.Delay(++_retryCount * 200);
                IsError = false;
            }
            return false;
        }

        private async Task ExecuteResponses()
        {
            int countExeption = 0;
            Dictionary<IPlanRequest, Task<BaseResponseHttp>> repeatRequestMap = new();
            foreach (var item in _mapRequest)
            {
                BaseResponseHttp result;
                try
                {
                    result = await ProcessResponseAsync(item.Value);
                    _resultRequests.Add(result);
                    _builderRequest.UpdateQueryPlan(result)
                        .ForEach(x => repeatRequestMap.Add(x, null));
                }
                catch 
                {
                    repeatRequestMap.Add(item.Key, null);
                    countExeption++; 
                }
            }
            if (countExeption >= (_mapRequest.Count/100)*50) // Checking that more than 50%
            {
                IsError = true;
            }
            _mapRequest = repeatRequestMap;
        }

        private async Task<BaseResponseHttp> ProcessResponseAsync(Task<BaseResponseHttp> responseHttp)
        {

            TaskCompletionSource<BaseResponseHttp> errorResult = new TaskCompletionSource<BaseResponseHttp>();
            try
            {
                return await responseHttp;
            }
            catch (TransferHandlingErrorStatusCodeException exception)
            {
                var config = _builderRequest.ExpectedErrorHandler(_logger, exception);
                if (config.CountWaitMillisecond > 0) { _logger.LogWarning("Request limit exceeded"); }
                if (config.IsStop) { _logger.LogWarning("Undefined error, the server is not responding."); }

                errorResult.SetCanceled();
                return await errorResult.Task;
            }
            catch (TaskCanceledException e)
            {
                _logger.LogWarning("Request timeout exceeded.");
                errorResult.SetCanceled();
                return await errorResult.Task;
            }
            catch (OperationCanceledException e)
            {
                _logger.LogWarning("Cancel operation.");
                errorResult.SetCanceled();
                return await errorResult.Task;
            }
            catch (Exception e)
            {
                _logger.LogError("Unexpected Exception {0}:{1:f}", e.GetType().ToString(), DateTime.Now);
                _logger.LogError(e.Message);

                errorResult.SetException(e);
                return await errorResult.Task;
            }
        }
    }
}
