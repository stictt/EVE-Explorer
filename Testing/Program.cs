﻿using ANALYTICS;
using Domain.Infrastructure;
using Loader.Infrastructure;
using Loader.Infrastructure.Api;
using Loader.Models.Api;
using Loader.Services;
using System.Diagnostics;
using System.Threading.RateLimiting;

internal class Program
{
    // InvTypeMaterials.csv Файл хранит Id предмета, и Id его ресурса для крафта и количество оного или его переработка. проверить
    // Покрайней мере руда, там показывается как переработка. Это переработка. 

    private async static Task Main(string[] args) // 1.0 Amarr , 0.9 Dodixie , 0.9 Rens 0.5 Hek
    {
        MarketOrderBuilder marketOrderBuilder =
            new MarketOrderBuilder(15, new List<int> { 10000002 }, new LoggerFactoryBase(Console.WriteLine));

        RequestApiScheduler<OrderApi> requestApiScheduler =
            new RequestApiScheduler<OrderApi>(new LoggerFactoryBase(Console.WriteLine), marketOrderBuilder);

        var items = await requestApiScheduler.Start();

        Stopwatch stopwatchApi = new Stopwatch(); 
        stopwatchApi.Start();

        OrderHistoryRecycler orderHistoryRecycler = new OrderHistoryRecycler();
        var t = await orderHistoryRecycler.Recycle(10000002, items.Select(x=>x.TypeId).GroupBy(x=>x).Select(x=>x.Key).ToList());

        stopwatchApi.Stop();
        Console.WriteLine("Api request - {0:f}",stopwatchApi.ElapsedMilliseconds);
        Console.WriteLine( t.Count);

        BinaryCachingService binaryCachingService = new BinaryCachingService();
        OrderHistoryMonthList orderHistoryMonthList = new OrderHistoryMonthList();
        orderHistoryMonthList.List = t;

        Console.WriteLine( binaryCachingService.TrySave<OrderHistoryMonthList>(Paths.OrderHistoryMonthPath, orderHistoryMonthList, out var re));
        Console.WriteLine(re.Message);
    }
}