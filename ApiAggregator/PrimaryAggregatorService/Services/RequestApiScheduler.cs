using Newtonsoft.Json.Linq;
using PrimaryAggregatorService.Infrastructure.Api;
using PrimaryAggregatorService.Infrastructure.Exceptions;
using PrimaryAggregatorService.Infrastructure.Interface;
using PrimaryAggregatorService.Models.Api;
using System;
using System.Collections.Concurrent;

namespace PrimaryAggregatorService.Services
{
#pragma warning disable CS4014 

    public class RequestApiScheduler<T>
    {
        private BlockingCollection<BaseResponseHttp> _resultRequests = new ();
        private BlockingCollection<IPlanRequest> _taskQueuePrimaryProcessing = new ();
        private BlockingCollection<IPlanRequest> _taskQueueSecondaryProcessing = new();
        private BlockingCollection<SettingsSchedulerErrorSource> _taskQueueErrorProcessing = new();
        private CancellationTokenSource _tokenSource = new ();
        private BuilderRequestScheduler _builderRequest;
        private ILogger _logger;
        private readonly SemaphoreSlim _semaphore;

        private int _maxRetryCount = 20;
        private int _retryCount = 0;

        private int _countRequestExecutor = 0;
        public RequestApiScheduler(ILogger logger, BuilderRequestScheduler builderRequestScheduler) 
        {
            _logger = logger;
            _builderRequest = builderRequestScheduler;
            _semaphore = new SemaphoreSlim (builderRequestScheduler.ParallelCount);
        }

        public async Task<List<T>> Start()
        {
            _taskQueuePrimaryProcessing = new();
            _taskQueueSecondaryProcessing = new();
            _resultRequests = new();
            _builderRequest.GetPlanRequests().ForEach(x => _taskQueuePrimaryProcessing.Add(x));
            while (_taskQueuePrimaryProcessing.Count > 0)
            {
                await Execute();
                if (!await CheckErrorsAndContinue(new TimeSpan(0, 0, 0, 7, 500)))
                {
                    _logger.LogWarning("Query plan failed");
                    return new List<T>();
                }
                MergeQueue();
            }
            return GetResult();
        }

        private List<T> GetResult()
        {
            List<T> values = new List<T> ();
            _resultRequests.ToList().ForEach(x => 
            {
                JToken jToken = JToken.Parse(x.Message);
                values.AddRange(jToken.ToObject<List<T>>());//To do переделать
            });
            return values;
        }

        private async Task Execute()
        {
            _countRequestExecutor = 1;
            while (_taskQueuePrimaryProcessing.Count > 0 && !_tokenSource.Token.IsCancellationRequested) 
            { 
                await _semaphore.WaitAsync(_tokenSource.Token).ConfigureAwait(false);
                if (_tokenSource.Token.IsCancellationRequested) { return; }

                _taskQueuePrimaryProcessing.TryTake(out IPlanRequest request);
                ExecuteRequest(request);
            }
        }

        private void ExecuteRequest(IPlanRequest request)
        {
            var http = new BaseRequestExecutorApi(_logger, _builderRequest.CheckResponseAndThrow);
            http.Execute(request, _tokenSource.Token)
                .ContinueWith((x) =>
                {
                    if (x.IsCanceled)
                    {
                        _taskQueueSecondaryProcessing.TryAdd(request);
                        _countRequestExecutor = _semaphore.Release();
                        return;
                    }
                    if (IsException(x, request)) 
                    {
                        _countRequestExecutor = _semaphore.Release();
                        return; 
                    }

                    _builderRequest.UpdateQueryPlan(x.Result, _taskQueueSecondaryProcessing);
                    _resultRequests.TryAdd(x.Result);
                    _countRequestExecutor = _semaphore.Release();

                }, TaskContinuationOptions.RunContinuationsAsynchronously);
        }

        private bool IsException(Task<BaseResponseHttp> response, IPlanRequest request)
        {
            if (response.IsFaulted
                            &&
                response.Exception?.InnerException is TransferHandlingErrorStatusCodeException exception)
            {

                _taskQueueSecondaryProcessing.TryAdd(request);
                _taskQueueErrorProcessing.TryAdd(_builderRequest.ExpectedErrorHandler(_logger, exception));
                _tokenSource.Cancel();
                return true;
            }
            else if (response.IsFaulted && response.Exception != null)
            {
                _taskQueueSecondaryProcessing.TryAdd(request);
                _logger.LogError(response.Exception.Message);
                _taskQueueErrorProcessing.TryAdd(new SettingsSchedulerErrorSource() { IsStop = true });
                _tokenSource.Cancel();
                return true;
            }
            return false;
        }

        private async Task<bool> CheckErrorsAndContinue(TimeSpan completionWaitDelay)
        {
            await WaitiCompletionOperations(completionWaitDelay);
            _tokenSource = new CancellationTokenSource();
            if (_taskQueueErrorProcessing.Count == 0) return true;
            var error = _taskQueueErrorProcessing.FirstOrDefault();

            if (error != null && error.IsStop)
            {
                return await CheckHandlingCriticalErrors(error);
            }
            await Task.Delay((error?.CountWaitMillisecond ?? 0) + 20);
            return true;
        }
        private async Task<bool> CheckHandlingCriticalErrors(SettingsSchedulerErrorSource settingsSchedulerErrorSource)
        {
            _retryCount++;
            if (_maxRetryCount <= _retryCount) { return false; }
            await Task.Delay(100* _retryCount);
            _taskQueueErrorProcessing = new BlockingCollection<SettingsSchedulerErrorSource>();
            return true;
        }
        private async Task WaitiCompletionOperations(TimeSpan timeSpan)
        {
            var time = DateTime.Now.Ticks;
            while (_countRequestExecutor > 0)
            {
                if ((time + timeSpan.Ticks) < DateTime.Now.Ticks) { return; }
                await Task.Delay(20);

            }
            _taskQueueErrorProcessing = new BlockingCollection<SettingsSchedulerErrorSource>();
        }

        private void MergeQueue()
        {
            while (_taskQueueSecondaryProcessing.TryTake(out var item))
            {
                _taskQueuePrimaryProcessing.TryAdd(item);
            }
        }
    }
}
