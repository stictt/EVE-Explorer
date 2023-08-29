
using Domain.Infrastructure.Interface;
using Loader.Infrastructure.Exceptions;
using Loader.Models.Api;
using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace Loader.Infrastructure.Interface
{
    public abstract class BuilderRequestScheduler
    {
        public Action<BaseResponseHttp, ILoggerBase> CheckResponseAndThrow { get; protected set; }

        public Func<
            ILoggerBase,
            TransferHandlingErrorStatusCodeException,
            SettingsSchedulerErrorSource> ExpectedErrorHandler { get; protected set; }
        public abstract List<IPlanRequest> GetPlanRequests();

        public abstract List<IPlanRequest> UpdateQueryPlan(BaseResponseHttp responseHttp);

        public abstract int ExpectedAmountRequest();
        public abstract RateLimiter GetLimiter();
    }
}
