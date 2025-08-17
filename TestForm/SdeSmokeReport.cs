using Domain.Models.ResourceDTO;
using Loader.Infrastructure;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Domain.Services // подправь под свой namespace
{
    public sealed class SdeSmokeReport
    {
        public bool Ok { get; set; }
        public TimeSpan LoadTime { get; set; }

        public int ItemCount { get; set; }
        public int RecipeCount { get; set; }
        public int ManufacturingCount { get; set; } // activityID = 1
        public int ReactionCount { get; set; }      // activityID = 11
        public int RecipesByProductKeys { get; set; }
        public int ReprocessingCount { get; set; }

        public int MissingGroup { get; set; }
        public int MissingCategory { get; set; }
        public int WithMarketGroup { get; set; }
        public int WithMarketPath { get; set; }

        public Dictionary<Kind, int> KindHistogram { get; set; } = new();

        public int RecipeMissingItemRefs { get; set; } // материалов/продуктов, которых нет в Items
        public List<string> Warnings { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public static class SdeSmokeTester
    {
        /// <summary>
        /// Прогоняет полную загрузку SDE и базовые проверки.
        /// </summary>
        public static SdeSmokeReport Run(CsvService csv)
        {
           
            var sw = Stopwatch.StartNew();

            // 1) Сбор агрегата
            var sde = csv.BuildSdeAggregate();

            sw.Stop();

            var rep = new SdeSmokeReport
            {
                LoadTime = sw.Elapsed,
                ItemCount = sde.Items.Count,
                RecipeCount = sde.Recipes.Count,
                ManufacturingCount = sde.Recipes.Count(r => r.ActivityID == 1),
                ReactionCount = sde.Recipes.Count(r => r.ActivityID == 11),
                RecipesByProductKeys = sde.RecipesByProduct.Count,
                ReprocessingCount = sde.Reprocessing.Count
            };

            // 2) Покрытие справочников
            rep.MissingGroup = sde.Items.Values.Count(i => string.IsNullOrWhiteSpace(i.GroupName));
            rep.MissingCategory = sde.Items.Values.Count(i => string.IsNullOrWhiteSpace(i.CategoryName));
            rep.WithMarketGroup = sde.Items.Values.Count(i => i.MarketGroupID != null);
            rep.WithMarketPath = sde.Items.Values.Count(i => i.MarketPath != null && i.MarketPath.Length > 0);

            // 3) Гистограмма классификатора
            rep.KindHistogram = sde.Items.Values
                .GroupBy(i => i.Kind)
                .OrderBy(g => g.Key.ToString())
                .ToDictionary(g => g.Key, g => g.Count());

            // 4) Целостность рецептов: все ссылки на типы — известны
            int missingRefs = 0;
            foreach (var r in sde.Recipes)
            {
                foreach (var m in r.Materials)
                    if (!sde.Items.ContainsKey(m.TypeID)) missingRefs++;

                foreach (var p in r.Products)
                    if (!sde.Items.ContainsKey(p.TypeID)) missingRefs++;
            }
            rep.RecipeMissingItemRefs = missingRefs;
            if (missingRefs > 0)
                rep.Warnings.Add($"Найдено {missingRefs} ссылок в рецептах на неизвестные typeID.");

            // 5) Простые sanity-checks
            if (rep.ItemCount == 0) rep.Errors.Add("Items пустой — invTypes.csv не загружен?");
            if (rep.RecipeCount == 0) rep.Warnings.Add("Нет рецептов — проверь industryActivity*.csv и industryBlueprints.csv.");
            if (rep.ReprocessingCount == 0) rep.Warnings.Add("Нет планов рефайна — проверь invTypeMaterials.csv.");

            // Чуть более строгие проверки качества
            if (rep.WithMarketGroup > 0 && rep.WithMarketPath == 0)
                rep.Warnings.Add("У типов проставлены marketGroupID, но не строится MarketPath (проверь invMarketGroups.csv).");

            if (rep.MissingGroup > 0)
                rep.Warnings.Add($"У {rep.MissingGroup} типов не сопоставлена группа (invGroups.csv?).");

            if (rep.MissingCategory > 0)
                rep.Warnings.Add($"У {rep.MissingCategory} типов не сопоставлена категория (invCategories.csv?).");

            rep.Ok = rep.Errors.Count == 0;

            // 6) Вывод краткого отчёта


            return rep;
        }

        public static void Print(SdeSmokeReport r, Action<string> log)
        {
            log($"[SDE] Загрузка и сбор агрегата: {r.LoadTime.TotalMilliseconds:F0} ms");
            log($"[SDE] Items: {r.ItemCount}, Recipes: {r.RecipeCount} (mfg={r.ManufacturingCount}, rxn={r.ReactionCount}), " +
                $"Reproc: {r.ReprocessingCount}, RecipesByProduct keys: {r.RecipesByProductKeys}");
            log($"[SDE] Market: withGroup={r.WithMarketGroup}, withPath={r.WithMarketPath}");
            log($"[SDE] Missing: group={r.MissingGroup}, category={r.MissingCategory}");
            if (r.RecipeMissingItemRefs > 0)
                log($"[SDE] Missing item refs in recipes: {r.RecipeMissingItemRefs}");

            // Гистограмма типов
            var kinds = string.Join(", ",
                r.KindHistogram.Select(kv => $"{kv.Key}:{kv.Value}"));
            log($"[SDE] Kinds: {kinds}");

            foreach (var w in r.Warnings) log("WARN: " + w);
            foreach (var e in r.Errors) log("ERR : " + e);

            log($"[SDE] Result: {(r.Ok ? "OK" : "FAILED")}");
        }
    }
}
