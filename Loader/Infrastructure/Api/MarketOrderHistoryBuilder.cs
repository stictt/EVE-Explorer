using Domain.Infrastructure.Interface;
using Loader.Infrastructure.Exceptions;
using Loader.Infrastructure.Interface;
using Loader.Models.Api;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Reflection.PortableExecutable;
using System.Threading.RateLimiting;

namespace Loader.Infrastructure.Api
{
    public class MarketOrderHistoryBuilder : BuilderRequestScheduler
    {
        private static string Api = @"https://esi.evetech.net/latest/markets/{0}/history/?datasource=tranquility&type_id={1}";
        private List<int> _marketOrders = new List<int>();
        private ILoggerFactoryBase _loggerFactory;
        private ILoggerBase _logger;
        private int _orderCount = 0;
        private int _parallelCount = 0;
        private RateLimiter _limiter;
        private int _typeId = 0;
        public MarketOrderHistoryBuilder(int parallelCount,List<int> regionsId,int typeId, ILoggerFactoryBase loggerFactory, RateLimiter rateLimiter) 
        {
            _typeId = typeId;
            _loggerFactory = loggerFactory;
            _logger = _loggerFactory.CreateLogger<MarketOrderBuilder>();
            _parallelCount = parallelCount;
            CheckResponseAndThrow += CheckResponseAndThrowHandler;
            ExpectedErrorHandler += ErrorHandler;
            _marketOrders = regionsId;
            _limiter = rateLimiter;
           
        }

        public override List<IPlanRequest> GetPlanRequests()
        {
            List<IPlanRequest> result = new();
            _marketOrders.ForEach(x => 
            {
                result.Add(new PlanRequestMarketHistoryApi()
                {
                    TypeId = _typeId,
                    PatternURL = Api,
                    RegionID = x
                });
            });
            return result;
        }

        public override List<IPlanRequest> UpdateQueryPlan(BaseResponseHttp responseHttp)
        {
            return new();
        }
        private SettingsSchedulerErrorSource ErrorHandler (ILoggerBase logger,
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

        private int GetWaitMillisecond(TransferHandlingErrorStatusCodeException transferHandlingError)
        {
            var header = transferHandlingError.ResponseHttp.Headers;
            int errorLimitReset = Int32.Parse(header.GetValues("x-esi-error-limit-reset").FirstOrDefault() ?? "0");
            return errorLimitReset * 1000 + 20;
        }

        private void CheckResponseAndThrowHandler(BaseResponseHttp baseResponse, ILoggerBase logger)
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

        public override int ExpectedAmountRequest()
        {
            return _orderCount;
        }

        public override RateLimiter GetLimiter()
        {
            return _limiter;
        }
    }
}
