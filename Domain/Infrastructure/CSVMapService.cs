using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Domain.Infrastructure;
using Domain.Models.BaseResourceModels;
using Domain.Models.ResourceDTO;

namespace Domain.Services
{
    public class CSVMapService
    {
        // -------------------- простое маппирование invTypes -> AvailableGameResourceDTO --------------------
        public List<AvailableGameResourceDTO> MapingAvailableGameResources(List<InvType> baseInvTypes)
        {
            var result = new List<AvailableGameResourceDTO>(baseInvTypes?.Count ?? 0);
            if (baseInvTypes == null) return result;
            foreach (var x in baseInvTypes)
                result.Add(ParseBaseInvTypeToAvailableGameResourceDTO(x));
            return result;
        }

        private AvailableGameResourceDTO ParseBaseInvTypeToAvailableGameResourceDTO(InvType value)
        {
            if (value == null) return new AvailableGameResourceDTO();

            return new AvailableGameResourceDTO
            {
                TypeID = value.TypeID,
                GroupID = value.GroupID,
                TypeName = value.TypeName,
                Description = value.Description,
                Mass = value.Mass,
                Volume = ParseDouble(value.Volume),
                Capacity = value.Capacity,
                PortionSize = value.PortionSize,
                BasePrice = ParseDoubleNullable(value.BasePrice),
                MarketGroupID = ParseIntNullable(value.MarketGroupID),
                IconID = ParseIntNullable(value.IconID)
            };
        }

        private double ParseDouble(string value)
        {
            double result = 0;
            try { result = double.Parse(value, CultureInfo.InvariantCulture); } catch { }
            return result;
        }
        private double? ParseDoubleNullable(string value)
        {
            try { if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var v)) return v; }
            catch { }
            return null;
        }
        private int? ParseIntNullable(string value)
        {
            try { if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)) return v; }
            catch { }
            return null;
        }

        // -------------------- основной агрегатор SDE --------------------
        public SdeAggregateDTO BuildAggregate(
            List<InvType> types,
            List<InvGroup> groups,
            List<InvCategory> cats,
            List<InvMarketGroup> mgs,
            List<IndustryActivity> ia,
            List<IndustryActivityMaterial> iam,
            List<IndustryActivityProduct> iap,
            List<IndustryBlueprint> ibl,
            List<InvTypeMaterial> itm)
        {
            types ??= new List<InvType>();
            groups ??= new List<InvGroup>();
            cats ??= new List<InvCategory>();
            mgs ??= new List<InvMarketGroup>();
            ia ??= new List<IndustryActivity>();
            iam ??= new List<IndustryActivityMaterial>();
            iap ??= new List<IndustryActivityProduct>();
            ibl ??= new List<IndustryBlueprint>();
            itm ??= new List<InvTypeMaterial>();

            // --- Индексы справочников ---
            var groupsById = groups.GroupBy(g => g.GroupID).ToDictionary(g => g.Key, g => g.First());
            var catsById = cats.GroupBy(c => c.CategoryID).ToDictionary(c => c.Key, c => c.First());
            var mgById = mgs.GroupBy(m => m.MarketGroupID).ToDictionary(m => m.Key, m => m.First());

            string[] BuildMarketPath(int? mgId)
            {
                if (mgId == null || !mgById.TryGetValue(mgId.Value, out var node))
                    return Array.Empty<string>();
                var list = new List<string>(8);
                var cur = node;
                list.Add(cur.MarketGroupName ?? "");
                while (cur.ParentGroupID is int parentId && mgById.TryGetValue(parentId, out var parent))
                {
                    list.Add(parent.MarketGroupName ?? "");
                    cur = parent;
                }
                list.Reverse();
                return list.ToArray();
            }

            int[] BuildMarketPathIds(int? mgId)
            {
                if (mgId == null || !mgById.TryGetValue(mgId.Value, out var node))
                    return Array.Empty<int>();
                var list = new List<int>(8);
                var cur = node;
                list.Add(cur.MarketGroupID);
                while (cur.ParentGroupID is int parentId && mgById.TryGetValue(parentId, out var parent))
                {
                    list.Add(parent.MarketGroupID);
                    cur = parent;
                }
                list.Reverse();
                return list.ToArray();
            }

            // --- Флаг рефайна (наличие записей в invTypeMaterials) ---
            var hasReproc = new HashSet<int>(itm.Select(x => x.TypeID));

            // --- Расширенные предметы ---
            var items = new Dictionary<int, ItemExtDTO>(Math.Max(1024, types.Count));
            foreach (var t in types)
            {
                groupsById.TryGetValue(t.GroupID, out var g);
                catsById.TryGetValue(g?.CategoryID ?? -1, out var c);

                var mgId = TryParseInt(t.MarketGroupID);
                var pathNames = BuildMarketPath(mgId);
                var pathIds = BuildMarketPathIds(mgId);

                var meta = new ItemMeta
                {
                    TypeID = t.TypeID,
                    GroupID = t.GroupID,
                    CategoryID = g?.CategoryID ?? 0,
                    MarketGroupID = mgId
                };
                var cls = MarketClassifier.Classify(meta);

                var vol = TryParseDouble(t.Volume) ?? 0.0;
                var ps = t.PortionSize;
                var published = true;
                try { published = (t.Published != 0); } catch { }

                var isCompressed = LooksCompressed(t.TypeName, pathNames);

                items[t.TypeID] = new ItemExtDTO
                {
                    TypeID = t.TypeID,
                    Name = t.TypeName ?? "",
                    GroupID = t.GroupID,
                    GroupName = g?.GroupName ?? "",
                    CategoryID = g?.CategoryID ?? 0,
                    CategoryName = c?.CategoryName ?? "",
                    MarketGroupID = mgId,
                    MarketPath = pathNames,
                    Volume = vol,
                    PortionSize = ps,
                    Published = published,
                    Kind = cls.Kind,
                    HasReprocessing = hasReproc.Contains(t.TypeID),
                    IsCompressed = (cls.Kind == Kind.Ore || cls.Kind == Kind.IceOre) ? isCompressed : false
                };
            }

            // --- Industry recipes (нужны в целом, но не для компрессии) ---
            var timeByKey = ia.GroupBy(x => (x.TypeID, x.ActivityID))
                              .ToDictionary(g => g.Key, g => g.First().Time);

            var limByBp = ibl.GroupBy(x => x.BlueprintTypeID)
                             .ToDictionary(g => g.Key, g => (int?)g.First().MaxProductionLimit);

            var matsByKey = iam.GroupBy(x => (x.TypeID, x.ActivityID))
                               .ToDictionary(
                                   g => g.Key,
                                   g => g.Select(m => new RecipeLine
                                   {
                                       TypeID = m.MaterialTypeID,
                                       Name = items.TryGetValue(m.MaterialTypeID, out var it) ? it.Name : m.MaterialTypeID.ToString(),
                                       Quantity = m.Quantity
                                   }).ToList()
                               );

            var prodsByKey = iap.GroupBy(x => (x.TypeID, x.ActivityID))
                                .ToDictionary(
                                    g => g.Key,
                                    g => g.Select(p => new RecipeLine
                                    {
                                        TypeID = p.ProductTypeID,
                                        Name = items.TryGetValue(p.ProductTypeID, out var it) ? it.Name : p.ProductTypeID.ToString(),
                                        Quantity = p.Quantity
                                    }).ToList()
                                );

            var recipes = new List<BlueprintRecipeDTO>();
            var recipesByProduct = new Dictionary<int, List<BlueprintRecipeDTO>>();
            var allKeys = matsByKey.Keys.Union(prodsByKey.Keys).Union(timeByKey.Keys).Distinct();
            foreach (var key in allKeys)
            {
                var (bpId, act) = key;
                var bpName = items.TryGetValue(bpId, out var bp) ? bp.Name : $"Blueprint {bpId}";

                var rec = new BlueprintRecipeDTO
                {
                    BlueprintTypeID = bpId,
                    BlueprintName = bpName,
                    ActivityID = act,
                    BaseTimeSeconds = timeByKey.TryGetValue(key, out var tsec) ? tsec : 0,
                    MaxProductionLimit = limByBp.TryGetValue(bpId, out var lim) ? lim : null,
                    Materials = matsByKey.TryGetValue(key, out var ml) ? ml : new List<RecipeLine>(),
                    Products = prodsByKey.TryGetValue(key, out var pl) ? pl : new List<RecipeLine>()
                };
                recipes.Add(rec);
                foreach (var p in rec.Products)
                {
                    if (!recipesByProduct.TryGetValue(p.TypeID, out var list))
                        recipesByProduct[p.TypeID] = list = new List<BlueprintRecipeDTO>();
                    list.Add(rec);
                }
            }

            // --- Переработка (invTypeMaterials) ---
            var reproc = new Dictionary<int, ReprocessingPlanDTO>();
            foreach (var g in itm.GroupBy(x => x.TypeID))
            {
                items.TryGetValue(g.Key, out var inputItem);
                var plan = new ReprocessingPlanDTO
                {
                    InputTypeID = g.Key,
                    InputName = inputItem?.Name ?? g.Key.ToString(),
                    PortionSize = inputItem?.PortionSize ?? 1,
                    Outputs = g.Select(x => new RecipeLine
                    {
                        TypeID = x.MaterialTypeID,
                        Name = items.TryGetValue(x.MaterialTypeID, out var outItem) ? outItem.Name : x.MaterialTypeID.ToString(),
                        Quantity = x.Quantity
                    }).ToList()
                };
                reproc[g.Key] = plan;
            }

            // -------------------- КОМПРЕССИЯ: строим по ИМЕНАМ (а не industryActivity) --------------------
            // Логика:
            // - У руды: "Compressed X" => 100:1, "Batch Compressed X" => 1:1
            // - У льда: всегда 1:1
            // Сопоставление делаем по "базовому имени" X.
            var compression = new Dictionary<int, List<CompressionOption>>();
            var compressionByInput = new Dictionary<int, CompressionRule>();
            var compressionByOutput = new Dictionary<int, CompressionRule>();

            static string Normalize(string s)
            {
                s = s.Trim();
                // убираем приставки компрессии
                if (s.StartsWith("Batch Compressed ", StringComparison.OrdinalIgnoreCase))
                    s = s.Substring("Batch Compressed ".Length);
                else if (s.StartsWith("Compressed ", StringComparison.OrdinalIgnoreCase))
                    s = s.Substring("Compressed ".Length);
                // никаких лишних суффиксов не трогаем — базовое имя совпадает у обычной и сжатой
                return s.Trim().ToLowerInvariant();
            }

            bool IsOreOrIce(ItemExtDTO it) => it.Kind == Kind.Ore || it.Kind == Kind.IceOre;

            // индекс несжатых по (Kind, baseName)
            var baseIndex = new Dictionary<(Kind kind, string baseName), List<ItemExtDTO>>();
            foreach (var it in items.Values.Where(it => IsOreOrIce(it) && !it.IsCompressed))
            {
                var key = (it.Kind, Normalize(it.Name));
                if (!baseIndex.TryGetValue(key, out var list)) baseIndex[key] = list = new List<ItemExtDTO>();
                list.Add(it);
            }

            // перечисляем все сжатые руды/льды и мапим к базовым
            foreach (var comp in items.Values.Where(it => IsOreOrIce(it) && it.IsCompressed))
            {
                var key = (comp.Kind, Normalize(comp.Name));
                if (!baseIndex.TryGetValue(key, out var bases)) continue;

                double unitsPerComp;
                if (comp.Kind == Kind.IceOre)
                {
                    unitsPerComp = 1; // лёд всегда 1:1
                }
                else
                {
                    // руда
                    if (comp.Name.StartsWith("Batch Compressed ", StringComparison.OrdinalIgnoreCase))
                        unitsPerComp = 1;
                    else
                        unitsPerComp = 100;
                }

                foreach (var baseIt in bases)
                {
                    if (!compression.TryGetValue(baseIt.TypeID, out var list))
                        compression[baseIt.TypeID] = list = new List<CompressionOption>();

                    // Добавляем опцию: какой сжатый тип и сколько обычной нужно на 1 ед. сжатой
                    list.Add(new CompressionOption
                    {
                        OutTypeID = comp.TypeID,
                        OutName = comp.Name,
                        UnitsPerCompressed = unitsPerComp
                    });

                    // rule для быстрых обратных ссылок (по желанию)
                    var rule = new CompressionRule
                    {
                        InputTypeID = baseIt.TypeID,
                        OutputTypeID = comp.TypeID,
                        ActivityID = 0,
                        UnitsPerOutput = (int)unitsPerComp,
                        Source = "heuristic:name"
                    };

                    // приоритет: для руды оставим как «по умолчанию» 100:1, если он есть; иначе 1:1
                    if (!compressionByInput.TryGetValue(baseIt.TypeID, out var exist) ||
                        (exist.UnitsPerOutput == 1 && unitsPerComp == 100))
                    {
                        compressionByInput[baseIt.TypeID] = rule;
                    }
                    compressionByOutput[comp.TypeID] = rule;
                }
            }

            return new SdeAggregateDTO
            {
                Items = items,
                Recipes = recipes,
                RecipesByProduct = recipesByProduct,
                Reprocessing = reproc,
                Compression = compression,
                CompressionByInput = compressionByInput,
                CompressionByOutput = compressionByOutput
            };
        }

        // compressed? по имени/папке
        private static bool LooksCompressed(string? name, IEnumerable<string> marketPath)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                if (name.StartsWith("Compressed ", StringComparison.OrdinalIgnoreCase)) return true;
                if (name.StartsWith("Batch Compressed ", StringComparison.OrdinalIgnoreCase)) return true;
            }
            return marketPath.Any(p => p.IndexOf("Compressed", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        // -------------------- парс-утилиты --------------------
        private static double? TryParseDouble(string s)
            => double.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : (double?)null;

        private static int? TryParseInt(string s)
            => int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : (int?)null;
    }
}
