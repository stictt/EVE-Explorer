// Services/OrderHistoryMonthManager.cs
using Domain.Infrastructure;
using Loader.Infrastructure;
using Loader.Infrastructure.Api;
using Loader.Models.Api;
using Loader.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ANALYTICS;

public sealed class OrderHistoryMonthManager
{
    private readonly AppSettings _settings;
    private readonly Action<string> _log;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public OrderHistoryMonthManager(AppSettings settings, Action<string>? logSink = null)
    {
        _settings = settings;
        _log = logSink ?? Console.WriteLine;
    }

    public sealed class ProgressInfo
    {
        public string Phase { get; init; } = "";
        public int Done { get; init; }
        public int Total { get; init; }
    }

    public sealed class Result
    {
        public bool Updated { get; init; }
        public int Items { get; init; }
        public DateTime? GeneratedUtc { get; init; }
        public string? Error { get; init; }
    }

    /// <summary>
    /// Ручная пересборка OrderHistoryMonthList по текущему срезу ордеров.
    /// Никакой авто-логики и проверок устаревания.
    /// </summary>
    public async Task<Result> BuildAsync(
        IProgress<ProgressInfo>? progress,
        CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            // 1) Текущий срез ордеров → typeIds
            progress?.Report(new ProgressInfo { Phase = "Загрузка ордеров (срез)", Done = 0, Total = 1 });
            var typeIds = await LoadCurrentTypeIdsAsync(ct);
            progress?.Report(new ProgressInfo { Phase = "Загрузка ордеров (срез)", Done = 1, Total = 1 });

            if (typeIds.Count == 0)
                return new Result { Updated = false, Items = 0, Error = "Пустой срез ордеров." };

            // 2) История по typeIds (старая логика)
            var recycler = new OrderHistoryRecycler();
            var hist = await recycler.Recycle(
                _settings.RegionId,
                typeIds,
                maxParallel: _settings.Parallelism,
                limiter: null,
                progress: new Progress<(int done, int total)>(p =>
                {
                    progress?.Report(new ProgressInfo { Phase = "История по типам", Done = p.done, Total = p.total });
                }),
                ct: ct);

            // 3) Сохранение бинаря
            var list = new OrderHistoryMonthList { List = hist };
            var cache = new BinaryCachingService();
            var ok = cache.TrySave(Paths.OrderHistoryMonthPath, list, out var res);
            _log($"[History] Save: {ok}, {res?.Message}");

            if (!ok) return new Result { Updated = false, Items = hist.Count, Error = res?.Message };

            var now = DateTime.UtcNow;
            _settings.OrderHistoryMonthGeneratedUtc = now;
            SettingsService.Save(_settings);

            return new Result { Updated = true, Items = hist.Count, GeneratedUtc = now };
        }
        catch (OperationCanceledException)
        {
            return new Result { Updated = false, Error = "отменено" };
        }
        catch (Exception ex)
        {
            return new Result { Updated = false, Error = ex.Message };
        }
        finally
        {
            _gate.Release();
        }
    }

    /// <summary>
    /// Доп. вариант: пересобрать, если ты сам передаёшь точный список typeId (например, из другого сервиса).
    /// </summary>
    public async Task<Result> BuildForTypesAsync(
        List<int> typeIds,
        IProgress<ProgressInfo>? progress,
        CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (typeIds == null || typeIds.Count == 0)
                return new Result { Updated = false, Items = 0, Error = "Пустой список typeId." };

            var recycler = new OrderHistoryRecycler();
            var hist = await recycler.Recycle(
                _settings.RegionId,
                typeIds.Distinct().ToList(),
                maxParallel: _settings.Parallelism,
                limiter: null,
                progress: new Progress<(int done, int total)>(p =>
                {
                    progress?.Report(new ProgressInfo { Phase = "История по типам", Done = p.done, Total = p.total });
                }),
                ct: ct);

            var list = new OrderHistoryMonthList { List = hist };
            var cache = new BinaryCachingService();
            var ok = cache.TrySave(Paths.OrderHistoryMonthPath, list, out var res);
            _log($"[History] Save: {ok}, {res.Message}");

            if (!ok) return new Result { Updated = false, Items = hist.Count, Error = res.Message };

            var now = DateTime.UtcNow;
            _settings.OrderHistoryMonthGeneratedUtc = now;
            SettingsService.Save(_settings);

            return new Result { Updated = true, Items = hist.Count, GeneratedUtc = now };
        }
        catch (OperationCanceledException)
        {
            return new Result { Updated = false, Error = "отменено" };
        }
        catch (Exception ex)
        {
            return new Result { Updated = false, Error = ex.Message };
        }
        finally
        {
            _gate.Release();
        }
    }

    public DateTime? GetLastGeneratedUtc() => _settings.OrderHistoryMonthGeneratedUtc;

    private async Task<List<int>> LoadCurrentTypeIdsAsync(CancellationToken ct)
    {
        var builder = new MarketOrderBuilder(_settings.Parallelism,
            new List<int> { _settings.RegionId },
            new LoggerFactoryBase(_log));

        var scheduler = new RequestApiScheduler<OrderApi>(new LoggerFactoryBase(_log), builder);
        var orders = await scheduler.Start(); // если есть Start(ct) — подставь ct
        return orders.Select(o => o.TypeId).Distinct().ToList();
    }
}
