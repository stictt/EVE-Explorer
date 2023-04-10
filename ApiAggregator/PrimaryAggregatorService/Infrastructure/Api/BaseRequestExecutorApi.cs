using PrimaryAggregatorService.Infrastructure.Interface;
using PrimaryAggregatorService.Models.Api;
using static System.Net.WebRequestMethods;

namespace PrimaryAggregatorService.Infrastructure.Api
{
    public class BaseRequestExecutorApi
    {
        private HttpClient _httpClient;
        private Action<BaseResponseHttp, ILogger> _сheckResponseAndThrow;
        private ILogger _logger;

        public BaseRequestExecutorApi(ILogger logger,Action<BaseResponseHttp, ILogger> сheckResponseAndThrow) 
        {
            _httpClient = new HttpClient();
            _logger = logger;
            _сheckResponseAndThrow = сheckResponseAndThrow;
        }

        public async Task<BaseResponseHttp> Execute(IPlanRequest uriBuilder,CancellationToken token, HttpMethod method = null)
        {
            BaseResponseHttp result = new BaseResponseHttp();

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
