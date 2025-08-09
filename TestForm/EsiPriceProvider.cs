using Loader.Infrastructure;
using Loader.Infrastructure.Api;
using Loader.Infrastructure.Interface;
using Loader.Models.Api;
using Loader.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestForm
{
    public interface IPriceProvider
    {
        Task<Dictionary<int, (double bestBuy, double bestSell)>> GetBestPricesAsync(
            IEnumerable<int> typeIds, int regionId, CancellationToken ct);
    }
    /// <summary>
    /// Провайдер цен ESI с авто-выбором билдера:
    /// - MarketOrderByIdBuilder для «малых» наборов typeId
    /// - MarketOrderBuilder для больших выборок
    /// Поддерживает фильтр по системам (по умолчанию Jita/Perimeter).
    /// </summary>
    public sealed class EsiPriceProvider : IPriceProvider
    {
        private readonly Action<string> _log;
        private readonly int[] _systemFilter;
        private readonly int _switchThreshold;

        /// <param name="log">куда логировать шаги (опционально)</param>
        /// <param name="switchThreshold">граница переключения на MarketOrderByIdBuilder</param>
        /// <param name="systemFilter">массив systemId, по которым фильтровать ордера (null/пусто — без фильтра)</param>
        public EsiPriceProvider(Action<string>? log = null,
                                int switchThreshold = 120,
                                int[]? systemFilter = null)
        {
            _log = log ?? Console.WriteLine;
            _switchThreshold = Math.Max(1, switchThreshold);
            _systemFilter = systemFilter ?? new[] { 30000142, 30000144 }; // Jita, Perimeter по умолчанию
        }

        public async Task<Dictionary<int, (double bestBuy, double bestSell)>> GetBestPricesAsync(
            IEnumerable<int> typeIds, int regionId, CancellationToken ct)
        {
            var ids = (typeIds ?? Array.Empty<int>()).Distinct().ToList();
            var result = new Dictionary<int, (double, double)>(ids.Count);

            if (ids.Count == 0)
                return result;

            // Выбор билдера
            BuilderRequestScheduler builder;
            var loggerFactory = new LoggerFactoryBase(_log);

            if (ids.Count <= _switchThreshold)
            {
                _log($"[EsiPriceProvider] Using MarketOrderByIdBuilder for {ids.Count} typeIds (region {regionId}).");
                builder = new MarketOrderByIdBuilder(
                    parallelCount: 15,
                    regionsId: new List<int> { regionId },
                    typeId: ids,
                    loggerFactory: loggerFactory);
            }
            else
            {
                _log($"[EsiPriceProvider] Using MarketOrderBuilder (full region scan) for {ids.Count} typeIds (region {regionId}).");
                builder = new MarketOrderBuilder(
                    parallelCount: 15,
                    regionsId: new List<int> { regionId },
                    loggerFactory: loggerFactory);
            }

            var scheduler = new RequestApiScheduler<OrderApi>(loggerFactory, builder);

            // Если у тебя есть перегрузка Start(ct) — подставь её здесь.
            var orders = await scheduler.Start();

            var wanted = new HashSet<int>(ids);

            IEnumerable<OrderApi> filtered = orders.Where(o => wanted.Contains(o.TypeId));

            if (_systemFilter is { Length: > 0 })
                filtered = filtered.Where(o => _systemFilter.Contains(o.SystemId));

            foreach (var g in filtered.GroupBy(o => o.TypeId))
            {
                double bestBuy = g.Where(o => o.IsBuyOrder)
                                  .Select(o => (double)o.Price)
                                  .DefaultIfEmpty(0)
                                  .Max();

                double bestSell = g.Where(o => !o.IsBuyOrder)
                                   .Select(o => (double)o.Price)
                                   .DefaultIfEmpty(0)
                                   .Min();

                result[g.Key] = (bestBuy, bestSell);
            }

            // гарантируем присутствие всех ключей
            foreach (var id in ids)
                if (!result.ContainsKey(id))
                    result[id] = (0, 0);

            return result;
        }
    }
}
