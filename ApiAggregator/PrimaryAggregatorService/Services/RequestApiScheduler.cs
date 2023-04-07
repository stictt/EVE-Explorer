using PrimaryAggregatorService.Infrastructure.Api;
using PrimaryAggregatorService.Models.Api;

namespace PrimaryAggregatorService.Services
{
    public class RequestApiScheduler
    {
        private List<BaseResponseHttp> _resultRequests = new ();
        private CancellationTokenSource _tokenSource = new ();
        public RequestApiScheduler(ILogger logger) 
        { 

        }
    }
}
