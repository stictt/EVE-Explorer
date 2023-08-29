using Domain.Infrastructure.Interface;
using Loader.Infrastructure.Interface;
using Loader.Models.Api;
using static System.Net.WebRequestMethods;

namespace Loader.Infrastructure.Api
{
    public class BaseRequestExecutorApi
    {
        private HttpClient _httpClient;
        private Action<BaseResponseHttp, ILoggerBase> _сheckResponseAndThrow;
        private ILoggerBase _logger;

        public BaseRequestExecutorApi(ILoggerFactoryBase logger, Action<BaseResponseHttp, ILoggerBase> сheckResponseAndThrow) 
        {
            _httpClient = new HttpClient();
            _logger = logger.CreateLogger<BaseRequestExecutorApi>();
            _сheckResponseAndThrow = сheckResponseAndThrow;
        }

        public async Task<BaseResponseHttp> Execute(IPlanRequest uriBuilder,CancellationToken token, HttpMethod method = null)
        {
            BaseResponseHttp result = new BaseResponseHttp();
            result.PlanRequest = uriBuilder;

            var request = new HttpRequestMessage(method ?? HttpMethod.Get, uriBuilder.GetURL());

            var response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);

            if (token.IsCancellationRequested) 
            {
                TaskCompletionSource<BaseResponseHttp> taskCancellation = new ();
                taskCancellation.SetCanceled();
                return await taskCancellation.Task;
            }

            result.StatusCode = response.StatusCode;
            result.Headers = response.Headers;
            result.Message = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            _сheckResponseAndThrow?.Invoke(result,_logger);

            return result;
        }
    }
}
