using PrimaryAggregatorService.Infrastructure.Exceptions;
using PrimaryAggregatorService.Models.Api;
using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace PrimaryAggregatorService.Infrastructure.Interface
{
    public abstract class BuilderRequestScheduler
    {
        public Action<BaseResponseHttp, ILogger> CheckResponseAndThrow { get; protected set; }

        public Func<
            ILogger,
            TransferHandlingErrorStatusCodeException,
            SettingsSchedulerErrorSource> ExpectedErrorHandler { get; protected set; }
        public abstract List<IPlanRequest> GetPlanRequests();

        public abstract List<IPlanRequest> UpdateQueryPlan(BaseResponseHttp responseHttp);

        public abstract int ExpectedAmountRequest();
        public abstract RateLimiter GetLimiter();
    }
}
