using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using PrimaryAggregatorService.Infrastructure.Api;
using PrimaryAggregatorService.Infrastructure.Exceptions;
using PrimaryAggregatorService.Infrastructure.Interface;
using PrimaryAggregatorService.Models.Api;
using System;
using System.Collections.Concurrent;
using System.Threading.Channels;

namespace PrimaryAggregatorService.Services
{
#pragma warning disable CS4014 

    public class RequestApiScheduler<T>
    {
        private Channel<BaseResponseHttp> _resultRequests = Channel.CreateUnbounded <BaseResponseHttp>();
        Channel<IPlanRequest> _channelPlanRequest = Channel.CreateUnbounded<IPlanRequest>();
        Channel<Task<BaseResponseHttp>> _channelResponse = Channel.CreateUnbounded<Task<BaseResponseHttp>>();
        private CancellationTokenSource _tokenSource = new ();
        private BuilderRequestScheduler _builderRequest;
        private ILogger _logger;
        private readonly SemaphoreSlim _semaphore;

        private int _maxRetryCount = 20;
        private int _retryCount = 0;
        private int _countRequestExecutor = 0;
        private bool isWait = false;
        public RequestApiScheduler(ILogger logger, BuilderRequestScheduler builderRequestScheduler) 
        {
            _countRequestExecutor = builderRequestScheduler.ParallelCount;
            _logger = logger;
            _builderRequest = builderRequestScheduler;
            _semaphore = new SemaphoreSlim (builderRequestScheduler.ParallelCount);
        }

        public async Task<List<T>> Start()
        {
            _builderRequest.GetPlanRequests()
                   .ForEach(x => _channelPlanRequest.Writer.TryWrite(x));
            ExecuteRequestPlan();
            await ProcessResponse();
            _logger.LogInformation("Количество ордеров {0}", _resultRequests.Reader.Count);
            return GetResult();
        }

        private List<T> GetResult()
        {
            List<T> values = new List<T> ();
            while (_resultRequests.Reader.Count > 0)
            {
                _resultRequests.Reader.TryRead(out var item);
                JToken jToken = JToken.Parse(item.Message);
                values.AddRange(jToken.ToObject<List<T>>());
            }

            return values;
        }

        private async Task ExecuteRequestPlan()
        {
            while (!_tokenSource.Token.IsCancellationRequested)
            {
                if (!_channelPlanRequest.Reader.TryRead(out var request)) { await Task.Delay(20); continue; }

                await _semaphore.WaitAsync(_tokenSource.Token).ConfigureAwait(false);
                if (_tokenSource.Token.IsCancellationRequested) { return; }
                var http = new BaseRequestExecutorApi(_logger, _builderRequest.CheckResponseAndThrow);
                _channelResponse.Writer.TryWrite(http.Execute(request, _tokenSource.Token));

            }
        }

        private async Task ProcessResponse()
        {
            while (!_tokenSource.Token.IsCancellationRequested)
            {
                while (_channelResponse.Reader.Count > 0)
                {
                    if (!_channelResponse.Reader.TryRead(out var response)) { await Task.Delay(20); continue; }

                    Parallel.Invoke(async () =>
                    {
                        await ProcessTaskResponse(response);
                        _countRequestExecutor = _semaphore.Release();
                    });

                }
                if (!IsContinue())
                {
                    return;
                }
            }
        }

        private async Task ProcessTaskResponse(Task<BaseResponseHttp> responseHttp)
        {
            try
            {
                BaseResponseHttp result = await responseHttp.ConfigureAwait(false);
                _builderRequest.UpdateQueryPlan(result)
                    .ForEach(x => _channelPlanRequest.Writer.TryWrite(x));
                _resultRequests.Writer.TryWrite(result);
            }
            catch(TransferHandlingErrorStatusCodeException exception)
            {
                var config = _builderRequest.ExpectedErrorHandler(_logger, exception);
                if (config.CountWaitMillisecond > 0)
                {
                    _logger.LogWarning("Превышен лимит обращений, идет ожидание");
                }
                if (config.IsStop)
                {
                    _retryCount++;
                }
                _channelPlanRequest.Writer.TryWrite(exception.ResponseHttp.PlanRequest);
            }
            catch (Exception e)
            {
                _logger.LogInformation("Неожиданное исключение.");
                _logger.LogInformation(e.Message);
                _retryCount++;
            }
        }

        private bool IsContinue()
        {
            if (_retryCount >= _maxRetryCount )
            {
                _tokenSource.Cancel();
                return false;
            }
            if (_builderRequest.ExpectedAmountRequest() <= _resultRequests.Reader.Count 
                && _semaphore.CurrentCount == _builderRequest.ParallelCount)
            {
                _tokenSource.Cancel();
                return false;
            }
            return true;
        }
    }
}
