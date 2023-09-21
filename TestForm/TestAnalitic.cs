using ANALYTICS;
using Domain.Infrastructure;
using Domain.Services;
using Loader.Infrastructure;
using Loader.Infrastructure.Api;
using Loader.Models.Api;
using Loader.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestForm
{
    public class TestAnalitic
    { 
        List<int> ints = new List<int>() { 30000142, 30000144 };
        public async Task<List<Model>> GetOrder()
        {
            BinaryCachingService binaryCachingService = new BinaryCachingService();
            binaryCachingService.TryLoad<OrderHistoryMonthList>(Paths.OrderHistoryMonthPath, out var dataLoad, out var re);
            CsvService csvService = new CsvService(new CSVMapService(), new LoggerFactoryBase().CreateLogger<CsvService>());

            var InvTypes =  csvService.GetBaseInvTypes();

            var items = (await GetMarketAsync()).Where(x=>ints.Contains(x.SystemId));

            var max = items.Where(x => x.IsBuyOrder).ToList();
            var min = items.Where(x => false == x.IsBuyOrder).ToList();

            var result = min.GroupBy(x => x.TypeId).Select(x =>
                {
                    var buy = max.Where(xx => xx.TypeId == x.Key).Max();
                    var sel = x.Min();
                    if (buy == null || sel == null) { return null; }
                   return new Model()
                    {
                        Margin = 100 - (buy.Price  / (sel.Price  / 100)),
                        averageVolume = dataLoad.List.Where(y => y.TypeId == x.Key).FirstOrDefault()?.AverageVolume ?? 0,
                        buyPrice = buy?.Price ?? 0,
                        sellPrice = x.Min()?.Price ?? 0,
                        ratingType = (dataLoad.List.Where(y => y.TypeId == x.Key).FirstOrDefault()?.Rating ?? 0)/ 1_000_000_000,
                        typeID = x.Key,
                        Name = InvTypes.Where(y => y.TypeID == x.Key).FirstOrDefault()?.TypeName ?? "nan",
                    };
                }).ToList() ;

            return result.Where(x=>x != null)
                //.Where(x=>x.Margin > 7)
                .Where(x=>x.averageVolume > 100)
                .Where(x=>x.buyPrice > 0)
                .Where(x => x.sellPrice > 0)
                .Where(x=>x.ratingType > 1)
                .OrderByDescending(x=>x.Margin)
                .Take(1000)
                .ToList();
        }



        public async Task<List<OrderApi>> GetMarketAsync()
        {
            MarketOrderBuilder marketOrderBuilder =
                new MarketOrderBuilder(15, new List<int> { 10000002 }, new LoggerFactoryBase());

            RequestApiScheduler<OrderApi> requestApiScheduler =
                new RequestApiScheduler<OrderApi>(new LoggerFactoryBase(), marketOrderBuilder);

            var items = await requestApiScheduler.Start();
            return items;
        }
    }
}
