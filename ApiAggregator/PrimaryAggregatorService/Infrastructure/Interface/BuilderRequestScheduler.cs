using PrimaryAggregatorService.Infrastructure.Exceptions;
using PrimaryAggregatorService.Models.Api;
using System.Collections.Concurrent;

namespace PrimaryAggregatorService.Infrastructure.Interface
{
    public abstract class BuilderRequestScheduler
    {
        public int ParallelCount { get; protected set; }
        public Action<BaseResponseHttp, ILogger> CheckResponseAndThrow { get; protected set; }

        public Func<
            ILogger,
            TransferHandlingErrorStatusCodeException,
            SettingsSchedulerErrorSource> ExpectedErrorHandler { get; protected set; }
        public abstract List<IPlanRequest> GetPlanRequests();

        public abstract void UpdateQueryPlan(BaseResponseHttp responseHttp, BlockingCollection<IPlanRequest> planRequests);

    }
}
