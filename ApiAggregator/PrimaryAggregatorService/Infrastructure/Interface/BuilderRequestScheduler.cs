using PrimaryAggregatorService.Infrastructure.Exceptions;
using PrimaryAggregatorService.Models.Api;
using System.Collections.Concurrent;

namespace PrimaryAggregatorService.Infrastructure.Interface
{
    public abstract class BuilderRequestScheduler
    {
        public int ParallelCount { get; private set; }
        public Action<BaseResponseHttp, ILogger> CheckResponseAndThrow { get; private set; }

        public Func<
            ILogger,
            TransferHandlingErrorStatusCodeException,
            SettingsSchedulerErrorSource> ExpectedErrorHandler { get; private set; }
        public abstract List<IPlanRequest> GetPlanRequests();

        public abstract List<IPlanRequest> UpdateQueryPlan(BaseResponseHttp responseHttp, BlockingCollection<IPlanRequest> planRequests);

    }
}
