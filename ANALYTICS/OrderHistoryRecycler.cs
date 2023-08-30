using Loader.Infrastructure.Api;
using Loader.Infrastructure;
using Loader.Models.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.RateLimiting;
using System.Threading.Tasks;
using Loader.Services;
using System.Collections.Concurrent;

namespace ANALYTICS
{
    public  class OrderHistoryRecycler
    {
        public async Task<List<OrderHistoryMonth>> Recycle(int regionID,List<int> typeID)
        {
            var _limiter = new SlidingWindowRateLimiter(
                new SlidingWindowRateLimiterOptions()
                {
                    Window = TimeSpan.FromSeconds(1),
                    SegmentsPerWindow = 10,
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 1,
                    PermitLimit = 10,
                    AutoReplenishment = true
                });
            BlockingCollection<OrderHistoryMonth> orderHistories = new BlockingCollection<OrderHistoryMonth>();
            /*foreach(var item in typeID)
            {
                MarketOrderHistoryBuilder marketOrderBuilder =
                    new MarketOrderHistoryBuilder(10, new List<int> { regionID }, item, new LoggerFactoryBase(Console.WriteLine), _limiter);

                RequestApiScheduler<OrderHistory> requestApiScheduler =
                    new RequestApiScheduler<OrderHistory>(new LoggerFactoryBase(Console.WriteLine), marketOrderBuilder);
                var items = await requestApiScheduler.Start();
                orderHistories.TryAdd(RecycleOrderHistory(items, item));
            }*/
            Parallel.ForEachAsync(typeID, async  (item,t) => 
            {
                MarketOrderHistoryBuilder marketOrderBuilder =
                    new MarketOrderHistoryBuilder(10, new List<int> { regionID }, item, new LoggerFactoryBase(Console.WriteLine), _limiter);

                RequestApiScheduler<OrderHistory> requestApiScheduler =
                    new RequestApiScheduler<OrderHistory>(new LoggerFactoryBase(Console.WriteLine), marketOrderBuilder);
                var items = await requestApiScheduler.Start();
                orderHistories.TryAdd(RecycleOrderHistory(items, item));
            }).Wait();

            return orderHistories.ToList();
        }

        private OrderHistoryMonth RecycleOrderHistory(List<OrderHistory> orderHistories,int typeID)
        {
            var temp = orderHistories.Where(x => x.Date > DateTime.Now.AddMonths(-30)).ToList();

            if (temp.Count == 0) { return new OrderHistoryMonth(); }

            OrderHistoryMonth orderHistory = new OrderHistoryMonth();
            orderHistory.TypeId = typeID;
            orderHistory.AverageVolume = (long)temp.Average(x => x.Volume);
            orderHistory.Rating = orderHistory.AverageVolume * temp.Average(x => x.Average);
            orderHistory.Month = DateTime.Now.AddMonths(-30);

            return orderHistory;
        }
    }
}
