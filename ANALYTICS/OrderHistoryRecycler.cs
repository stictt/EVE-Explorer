using Loader.Infrastructure.Api;
using Loader.Infrastructure;
using Loader.Models.Api;
using Loader.Services;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.RateLimiting;
using System.Threading.Tasks;

namespace ANALYTICS
{
    public sealed class OrderHistoryRecycler
    {
        /// <summary>
        /// Новая сигнатура под менеджер: управляемый параллелизм, общий лимитер, прогресс и отмена.
        /// ВНЕШНИХ обработок HTTP-кодов НЕТ — этим занимаются билдер/планировщик.
        /// </summary>
        public async Task<List<OrderHistoryMonth>> Recycle(
            int regionId,
            List<int> typeIds,
            int maxParallel = 10,
            SlidingWindowRateLimiter? limiter = null,
            IProgress<(int done, int total)>? progress = null,
            CancellationToken ct = default)
        {
            // Один общий лимитер для всех билдов истории — как в исходнике.
            var sharedLimiter = limiter ?? new SlidingWindowRateLimiter(new SlidingWindowRateLimiterOptions
            {
                Window = TimeSpan.FromSeconds(1),
                SegmentsPerWindow = 10,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 1,                 // как в оригинале
                PermitLimit = Math.Max(1, maxParallel),
                AutoReplenishment = true
            });

            var bag = new ConcurrentBag<OrderHistoryMonth>();
            var ids = (typeIds ?? new List<int>()).Distinct().ToList();
            var total = ids.Count;
            int done = 0;

            var po = new ParallelOptions
            {
                MaxDegreeOfParallelism = Math.Max(1, maxParallel),
                CancellationToken = ct
            };

            await Parallel.ForEachAsync(ids, po, async (typeId, token) =>
            {
                token.ThrowIfCancellationRequested();

                // ВАЖНО: никакого внешнего catch по статусам — всё в RequestApiScheduler
                var builder = new MarketOrderHistoryBuilder(
                    maxParallel,
                    new List<int> { regionId },
                    typeId,
                    new LoggerFactoryBase(Console.WriteLine),
                    sharedLimiter);

                var scheduler = new RequestApiScheduler<OrderHistory>(
                    new LoggerFactoryBase(Console.WriteLine),
                    builder);

                var items = await scheduler.Start(); // если есть перегрузка Start(token) — можно передать token

                bag.Add(AggregateMonth(items, typeId));

                var cur = Interlocked.Increment(ref done);
                progress?.Report((cur, total));
            });

            return bag.ToList();
        }

        /// <summary>
        /// Старый метод для обратной совместимости (как в оригинале): без прогресса/отмены/лимитера снаружи.
        /// </summary>
        public async Task<List<OrderHistoryMonth>> Recycle(int regionId, List<int> typeIds)
            => await Recycle(regionId, typeIds, maxParallel: 10, limiter: null, progress: null, ct: default);

        // --- Агрегация за последние 30 дней (UTC). Хочешь ровно как было — поменяем на AddMonths(-30) и DateTime.Now.
        private static OrderHistoryMonth AggregateMonth(List<OrderHistory> history, int typeId)
        {
            var now = DateTime.UtcNow;
            var from = now.AddDays(-30);

            var slice = history.Where(x => x.Date >= from && x.Date <= now).ToList();

            // Совместимость с оригиналом: при пустом срезе возвращаем ПОРОЖНИЙ объект без TypeId.
            if (slice.Count == 0) return new OrderHistoryMonth();

            var avgVol = slice.Average(x => (double)x.Volume);
            var avgPrice = slice.Average(x => x.Average);

            return new OrderHistoryMonth
            {
                TypeId = typeId,
                Month = from,
                AverageVolume = (long)avgVol,
                Rating = avgVol * avgPrice
            };
        }
    }
}
