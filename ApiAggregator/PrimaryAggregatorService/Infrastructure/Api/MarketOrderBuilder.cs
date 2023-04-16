using PrimaryAggregatorService.Infrastructure.Exceptions;
using PrimaryAggregatorService.Infrastructure.Interface;
using PrimaryAggregatorService.Models.Api;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Reflection.PortableExecutable;

namespace PrimaryAggregatorService.Infrastructure.Api
{
    public class MarketOrderBuilder : BuilderRequestScheduler
    {
        private static string Api = @"https://esi.evetech.net/latest/markets/{0}/orders/?datasource=tranquility&order_type=all&page={1}";
        private List<int> _marketOrders = new List<int>();
        public MarketOrderBuilder(int parallelCount,List<int> regionsId) 
        {
            ParallelCount = parallelCount;
            CheckResponseAndThrow += CheckResponseAndThrowHandler;
            ExpectedErrorHandler += ErrorHandler;
            _marketOrders = regionsId;
        }

        public override List<IPlanRequest> GetPlanRequests()
        {
            List<IPlanRequest> result = new();
            _marketOrders.ForEach(x => 
            {
                result.Add(new PlanRequestMarketApi()
                {
                    Page = 1,
                    PatternURL = Api,
                    RegionID = x
                });
            });
            return result;
        }

        public override void UpdateQueryPlan(BaseResponseHttp responseHttp, BlockingCollection<IPlanRequest> planRequests)
        {
            int page = GetNumberPage(responseHttp);

            if (page <= 1 ){ return; }
            if (((PlanRequestMarketApi)responseHttp.PlanRequest).Page > 1) { return; }
            Enumerable.Range(2, page - 1)
                .Select(x => new PlanRequestMarketApi()
                {
                    Page = x,
                    PatternURL = Api,
                    RegionID = ((PlanRequestMarketApi)responseHttp.PlanRequest).RegionID
                })
                .ToList()
                .ForEach(x =>
                {
                    planRequests.TryAdd(x);
                });
        }
        private SettingsSchedulerErrorSource ErrorHandler (ILogger logger,
            TransferHandlingErrorStatusCodeException transferHandlingError)
        {
            SettingsSchedulerErrorSource schedulerErrorSource = new();
            switch (transferHandlingError.ResponseHttp.StatusCode)
            {
                case (HttpStatusCode)420:
                    schedulerErrorSource.CountWaitMillisecond = GetWaitMillisecond(transferHandlingError);
                    return schedulerErrorSource;
                default:
                    schedulerErrorSource.IsStop = true;
                    return schedulerErrorSource;
            }
        }
        private int GetNumberPage(BaseResponseHttp responseHttp)
        {
            var header = responseHttp.Headers;
            int countPage = Int32.Parse(header.GetValues("x-pages").FirstOrDefault() ?? "0");
            return countPage;
        }
        private int GetWaitMillisecond(TransferHandlingErrorStatusCodeException transferHandlingError)
        {
            var header = transferHandlingError.ResponseHttp.Headers;
            int errorLimitReset = Int32.Parse(header.GetValues("x-esi-error-limit-reset").FirstOrDefault() ?? "0");
            return errorLimitReset * 1000 + 20;
        }

        private void CheckResponseAndThrowHandler(BaseResponseHttp baseResponse, ILogger logger)
        {
            switch (baseResponse.StatusCode)
            {
                case HttpStatusCode.InternalServerError:
                case HttpStatusCode.NotFound:
                case HttpStatusCode.BadGateway:
                case HttpStatusCode.ServiceUnavailable:
                case HttpStatusCode.BadRequest:
                case HttpStatusCode.GatewayTimeout:
                case HttpStatusCode.RequestTimeout:
                    baseResponse.Error = true;
                    throw new TransferHandlingErrorStatusCodeException(baseResponse);

                case (HttpStatusCode)420:
                    baseResponse.Error = true;
                    throw new TransferHandlingErrorStatusCodeException(baseResponse);

                case (HttpStatusCode)422:
                    baseResponse.Error = true;
                    throw new TransferHandlingErrorStatusCodeException(baseResponse);

                case (HttpStatusCode)200:
                case (HttpStatusCode)304:
                    return;

                default:
                    baseResponse.Error = true;
                    throw new TransferHandlingErrorStatusCodeException(baseResponse);
            }
        }
    }
}
