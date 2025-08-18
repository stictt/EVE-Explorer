using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace TestForm.Infrastructure
{
    public sealed class ProductionPlanner : IProductionPlanner
    {
        private readonly ISdePiRepository _repo;
        private readonly IPiConfigProvider _cfg;
        private readonly RecipeGraph _graph;

        public ProductionPlanner(ISdePiRepository repo, IPiConfigProvider cfg)
        {
            _repo = repo;
            _cfg = cfg;
            _graph = new RecipeGraph(repo);
        }

        // ---- helpers: CC caps, facility costs, per-factory I/O ----
        private (int cpu, int pw) GetEffectiveCcCaps(int ccLevel, double penalty)
        {
            if (!_cfg.Value.CommandCenterCapsByLevel.TryGetValue(ccLevel, out var caps))
                throw new InvalidOperationException($"CC level {ccLevel} not configured");
            var effCpu = (int)Math.Floor(caps.cpu * (1.0 - penalty));
            var effPw = (int)Math.Floor(caps.power * (1.0 - penalty));
            return (effCpu, effPw);
        }

        private (int cpu, int pw) GetFacilityCost(string facilityKind)
        {
            if (!_cfg.Value.Facilities.TryGetValue(facilityKind, out var c))
                throw new InvalidOperationException($"Facility '{facilityKind}' missing in config");
            return (c.cpu, c.power);
        }

        private static string ResolveFacilityKindForTier(RecipeNode node, int tier)
        {
            // Жёсткая привязка: Т1→Basic, T2/T3→Advanced, T4→HighTech
            return tier switch
            {
                1 => "Basic",
                2 => "Advanced",
                3 => "Advanced",
                4 => "HighTech",
                _ => "Advanced"
            };
        }

        private decimal GetPerFactoryThroughput(RecipeNode node)
        {
            if (node.CycleTimeSeconds <= 0 || node.OutputQty <= 0) return 0m;
            return (3600m / node.CycleTimeSeconds) * node.OutputQty;
        }

        private Dictionary<int, decimal> GetPerFactoryInputsPerHour(RecipeNode node)
        {
            var dict = new Dictionary<int, decimal>();
            if (node.CycleTimeSeconds <= 0 || node.OutputQty <= 0 || node.Inputs.Count == 0) return dict;
            var cyclesPerHour = 3600m / node.CycleTimeSeconds;
            foreach (var inp in node.Inputs)
                dict[inp.InputTypeId] = cyclesPerHour * inp.InputQty;
            return dict;
        }

        private static void EnsureMandatory(Dictionary<string, int> facs, IPiConfigProvider cfg)
        {
            // Launchpad и Storage — обязательны (если определены в Facilities)
            if (cfg.Value.Facilities.ContainsKey("Launchpad") && !facs.ContainsKey("Launchpad"))
                facs["Launchpad"] = 1;
            if (cfg.Value.Facilities.ContainsKey("Storage") && !facs.ContainsKey("Storage"))
                facs["Storage"] = 1;
        }

        /// <summary>
        /// Возвращает (out/h на планету, набор Facilities), учитывая:
        /// - либо шаблон (с проверкой CC caps),
        /// - либо автоподбор количества фабрик нужного типа (с обязательным Launchpad+Storage).
        /// </summary>
        private (decimal perPlanetOutPerHour, Dictionary<string, int> facilities) ComputePerPlanetCapacity(
            RecipeNode node, int tier, string facilityKind, int ccLevel, double ccPenalty,
            bool useTemplate, string? tplNameForNode)
        {
            var (effCpu, effPw) = GetEffectiveCcCaps(ccLevel, ccPenalty);

            var facs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            if (useTemplate && tplNameForNode != null && _cfg.Value.Templates != null
                && _cfg.Value.Templates.TryGetValue(tplNameForNode, out var tpl))
            {
                foreach (var kv in tpl.Facilities)
                    facs[kv.Key] = kv.Value;

                EnsureMandatory(facs, _cfg);

                int cpu = 0, pw = 0;
                foreach (var kv in facs)
                {
                    var (cCpu, cPw) = GetFacilityCost(kv.Key);
                    cpu += cCpu * kv.Value;
                    pw += cPw * kv.Value;
                }
                if (cpu > effCpu || pw > effPw)
                    throw new InvalidOperationException($"Template '{tplNameForNode}' exceeds CC caps (penalty {ccPenalty:P0}).");
            }
            else
            {
                // auto-fit: LP+Storage обязательны
                EnsureMandatory(facs, _cfg);

                int usedCpu = 0, usedPw = 0;
                if (facs.TryGetValue("Launchpad", out var lpCnt))
                {
                    var (lpCpu, lpPw) = GetFacilityCost("Launchpad");
                    usedCpu += lpCnt * lpCpu;
                    usedPw += lpCnt * lpPw;
                }
                if (facs.TryGetValue("Storage", out var stCnt))
                {
                    var (stCpu, stPw) = GetFacilityCost("Storage");
                    usedCpu += stCnt * stCpu;
                    usedPw += stCnt * stPw;
                }

                var (fCpu, fPw) = GetFacilityCost(facilityKind);
                var canCpu = (effCpu - usedCpu) / Math.Max(fCpu, 1);
                var canPw = (effPw - usedPw) / Math.Max(fPw, 1);
                var factories = Math.Max(0, Math.Min(canCpu, canPw));
                if (factories <= 0)
                    throw new InvalidOperationException($"Cannot place any '{facilityKind}' factories under CC caps.");

                facs[facilityKind] = factories;
            }

            var perFactory = GetPerFactoryThroughput(node);
            if (perFactory <= 0)
                throw new InvalidOperationException($"Zero throughput for node {node.OutputTypeId}.");

            var count = facs.TryGetValue(facilityKind, out var n) ? n : 0;
            var perPlanet = perFactory * count;

            return (perPlanet, facs);
        }

        public async Task<ProductionPlanResult> PlanAsync(
            int productTypeId, int planets, int ccLevel, PiMode mode, decimal taxRate, CancellationToken ct, PlannerSettings? settings = null)
        {
            settings ??= new PlannerSettings();

            // 1) Рецепт и тировка
            var recipe = _graph.Build(productTypeId);
            var tierCache = new Dictionary<int, int>();
            int GetTier(RecipeNode n)
            {
                if (tierCache.TryGetValue(n.OutputTypeId, out var t)) return t;
                if (n.Inputs.Count == 0) return tierCache[n.OutputTypeId] = 0;
                var maxChild = n.Inputs.Max(x => GetTier(x.Node!));
                return tierCache[n.OutputTypeId] = maxChild + 1;
            }
            _ = GetTier(recipe);

            // 2) Режимы: Market vs Extraction(Strict)
            int cutoff = settings.PurchaseCutoffTier ?? (mode switch
            {
                PiMode.Full => settings.StartProductionTier - 1,
                PiMode.TradeP0 => 0,
                PiMode.TradeP1 => 1,
                _ => settings.StartProductionTier - 1
            });
            cutoff = Math.Max(0, cutoff);

            var strict = settings.UseExtractionTemplate; // жёсткий режим без покупок
            if (strict && cutoff > 0) cutoff = 0;        // в strict всегда производим T1

            // 3) Нормализация BOM на 1 финальную фабрику
            var finalPerHour = recipe.CycleTimeSeconds > 0 ? (3600m / recipe.CycleTimeSeconds) * recipe.OutputQty : 0m;

            var demands = new Dictionary<(int typeId, int tier), decimal>();
            void Acc((int, int) key, decimal add)
            {
                if (!demands.TryGetValue(key, out var cur)) cur = 0;
                demands[key] = cur + add;
            }

            void Require(RecipeNode node, decimal desiredOutPerHour)
            {
                var nodeTier = tierCache[node.OutputTypeId];
                if (nodeTier <= cutoff || node.Inputs.Count == 0)
                {
                    Acc((node.OutputTypeId, nodeTier), desiredOutPerHour);
                    return;
                }
                var nodePerHour = node.CycleTimeSeconds > 0 ? (3600m / node.CycleTimeSeconds) * node.OutputQty : 0m;
                if (nodePerHour <= 0) return;
                var cycles = desiredOutPerHour / nodePerHour;
                foreach (var inp in node.Inputs)
                {
                    var child = inp.Node!;
                    var needChildPerHour = cycles * inp.InputQty;
                    Require(child, needChildPerHour);
                }
            }
            Require(recipe, finalPerHour);

            // 4) Проверка шаблонов
            string? extractionTplName = (settings.UseExtractionTemplate ? settings.ExtractionTemplateName : null);
            if (settings.UseExtractionTemplate)
            {
                if (string.IsNullOrWhiteSpace(extractionTplName))
                    throw new InvalidOperationException("Extraction template is enabled but name is empty.");
                if (_cfg.Value.Templates is null || !_cfg.Value.Templates.ContainsKey(extractionTplName))
                    throw new InvalidOperationException($"Extraction template '{extractionTplName}' not found in config.");
            }

            string? FactoryTemplateForTier(int tier)
            {
                if (!settings.UseFactoryTemplates) return null;
                if (settings.TierFactoryTemplates != null &&
                    settings.TierFactoryTemplates.TryGetValue(tier, out var name) &&
                    _cfg.Value.Templates != null &&
                    _cfg.Value.Templates.ContainsKey(name))
                    return name;
                return null;
            }

            // 5) Узлы, которые производим (tier > cutoff), финал — всегда
            var produceNodes = new Dictionary<int, RecipeNode>(); // typeId -> node
            void CollectProduceNodes(RecipeNode n)
            {
                var t = tierCache[n.OutputTypeId];
                if (t > cutoff) produceNodes[n.OutputTypeId] = n;
                foreach (var i in n.Inputs) CollectProduceNodes(i.Node!);
            }
            CollectProduceNodes(recipe);
            if (!produceNodes.ContainsKey(recipe.OutputTypeId))
                produceNodes[recipe.OutputTypeId] = recipe;

            // 6) Пер-планета способность и facilities (с правильным типом процессора по тиру)
            var nodeCapPerPlanet = new Dictionary<int, decimal>();
            var nodePlanetFacilities = new Dictionary<int, Dictionary<string, int>>();

            foreach (var kv in produceNodes)
            {
                var node = kv.Value;
                var tier = tierCache[node.OutputTypeId];
                var kind = ResolveFacilityKindForTier(node, tier);

                string? tplName = null;
                bool useTpl = false;
                if (tier <= 1)
                {
                    if (settings.UseExtractionTemplate)
                    {
                        tplName = extractionTplName!;
                        useTpl = true;
                    }
                }
                else
                {
                    tplName = FactoryTemplateForTier(tier);
                    useTpl = tplName != null;
                }

                var (perPlanet, facs) = ComputePerPlanetCapacity(node, tier, kind, ccLevel, settings.CcPenalty, useTpl, tplName);
                if (perPlanet <= 0)
                    throw new InvalidOperationException($"Per-planet capacity is zero for type {node.OutputTypeId}.");

                nodeCapPerPlanet[node.OutputTypeId] = perPlanet;
                nodePlanetFacilities[node.OutputTypeId] = facs;
            }

            // 7) Спрос на выпуск узлов для 1 "юнита" финального (capFinalPerPlanet)
            var capFinalPerPlanet = nodeCapPerPlanet[recipe.OutputTypeId];

            var requiredOutPerHourFor = new Dictionary<int, decimal>(); // typeId -> out/h
            void RequireProduced(RecipeNode n, decimal desiredOutPerHour)
            {
                var t = tierCache[n.OutputTypeId];
                if (t <= cutoff) return;

                if (!requiredOutPerHourFor.TryGetValue(n.OutputTypeId, out var cur)) cur = 0m;
                requiredOutPerHourFor[n.OutputTypeId] = cur + desiredOutPerHour;

                if (n.Inputs.Count == 0) return;

                var perFactoryIn = GetPerFactoryInputsPerHour(n);
                var perFactoryOut = GetPerFactoryThroughput(n);
                if (perFactoryOut <= 0) return;

                var scale = desiredOutPerHour / perFactoryOut;
                foreach (var inp in n.Inputs)
                {
                    var child = inp.Node!;
                    var needChildOutPerHour = scale * (perFactoryIn.TryGetValue(inp.InputTypeId, out var v) ? v : 0m);
                    RequireProduced(child, needChildOutPerHour);
                }
            }
            RequireProduced(recipe, capFinalPerPlanet);

            // 8) r_i = требуемые планеты/юнит по каждому узлу
            var r = new Dictionary<int, decimal>(); // typeId -> planets per unit (continuous)
            foreach (var kv in requiredOutPerHourFor)
            {
                var typeId = kv.Key;
                var needOutPerHour = kv.Value;
                var capPerPlanet = nodeCapPerPlanet[typeId];
                var planetsPerUnit = needOutPerHour / capPerPlanet;
                r[typeId] = planetsPerUnit;
            }

            var active = new HashSet<int>(r.Keys);
            int finalTypeId = recipe.OutputTypeId;

            // === 9A) STRICT (без покупок): проверка и дискретизация ===
            if (strict)
            {
                // сколько узлов необходимо одновременно
                int requiredMin = active.Count; // по одному на узел
                if (planets < requiredMin)
                {
                    return new ProductionPlanResult
                    {
                        ProductTypeId = productTypeId,
                        PlanetsUsed = 0,
                        OutputPerDay = 0,
                        RevenuePerDay = 0,
                        CostsPerDay = 0,
                        Recipe = recipe,
                        Note = $"STRICT mode: not enough planets. Need ≥{requiredMin}, have {planets}."
                    };
                }

                // старт: по 1 планете на узел
                var x = active.ToDictionary(id => id, _ => 1);
                int remaining = planets - requiredMin;

                // распределяем остаток пропорционально r_i (чем больше r_i, тем раньше получит следующую планету)
                decimal Ratio(int id) => x[id] / (r[id] == 0 ? 1m : r[id]);

                while (remaining > 0 && active.Count > 0)
                {
                    var target = active.OrderBy(Ratio).First();
                    x[target] = x[target] + 1;
                    remaining--;
                }

                // множитель "юнитов" и выпуск
                decimal unitsFactor = active.Min(id => x[id] / (r[id] == 0 ? 1m : r[id]));
                if (unitsFactor < 0) unitsFactor = 0;
                var outPerHour = unitsFactor * capFinalPerPlanet;
                var outPerDay = outPerHour * 24m;

                // формируем планеты
                var planetPlans = new List<PlanetPlan>();
                foreach (var id in active)
                {
                    var node = produceNodes[id];
                    var t = tierCache[id];

                    var facs = nodePlanetFacilities[id];
                    var perPlanetOut = nodeCapPerPlanet[id];
                    var perFactoryIn = GetPerFactoryInputsPerHour(node);
                    var perFactoryOut = GetPerFactoryThroughput(node);

                    var inputsPerPlanet = new Dictionary<int, decimal>();
                    if (perFactoryOut > 0)
                    {
                        var scale = perPlanetOut / perFactoryOut;
                        foreach (var inp in node.Inputs)
                            if (perFactoryIn.TryGetValue(inp.InputTypeId, out var v))
                                inputsPerPlanet[inp.InputTypeId] = v * scale;
                    }

                    var role = t switch
                    {
                        0 or 1 => "Extraction",
                        2 => "Factory-Advanced",
                        3 => "Factory-Advanced",
                        4 => "Factory-HighTech",
                        _ => "Factory"
                    };

                    for (int i = 0; i < x[id]; i++)
                    {
                        planetPlans.Add(new PlanetPlan
                        {
                            Role = role,
                            ProductTypeId = id,
                            Facilities = new Dictionary<string, int>(facs),
                            InputPerHour = new Dictionary<int, decimal>(inputsPerPlanet),
                            OutputPerHour = new Dictionary<int, decimal> { [id] = perPlanetOut }
                        });
                    }
                }

                var result = new ProductionPlanResult
                {
                    ProductTypeId = productTypeId,
                    PlanetsUsed = planetPlans.Count,
                    OutputPerDay = outPerDay,
                    RevenuePerDay = 0m,
                    CostsPerDay = 0m,
                    Recipe = recipe,
                    Note = $"MODE=Extraction/STRICT | Active={active.Count} | Units≈{(outPerDay / 24m / capFinalPerPlanet):0.###} | CC penalty {(settings.CcPenalty * 100):0.#}% | LP+Storage mandatory"
                };
                result.Planets.AddRange(planetPlans);

                foreach (var kv in demands.OrderBy(k => k.Key.tier).ThenBy(k => k.Key.typeId))
                {
                    result.RequiredInputs.Add(new MaterialDemand
                    {
                        TypeId = kv.Key.typeId,
                        Tier = kv.Key.tier,
                        QuantityPerHour = kv.Value
                    });
                }

                return await Task.FromResult(result);
            }

            // === 9B) MARKET: Greedy-покупка при дефиците планет ===
            decimal sumR = 0m;
            foreach (var id in active) sumR += r[id];

            var purchasedNodes = new HashSet<int>();
            bool CanPurchase(int id) => id != finalTypeId;

            while (planets < (int)Math.Ceiling(sumR))
            {
                var candidates = active.Where(CanPurchase).ToList();
                if (candidates.Count == 0 || active.Count <= 1) break;

                int victim = candidates.OrderByDescending(id => r[id]).First();
                active.Remove(victim);
                purchasedNodes.Add(victim);

                sumR = 0m;
                foreach (var id in active) sumR += r[id];
            }

            if (planets < (int)Math.Ceiling(sumR) && active.Count > 1)
            {
                foreach (var id in new List<int>(active))
                {
                    if (!CanPurchase(id)) continue;
                    purchasedNodes.Add(id);
                    active.Remove(id);
                }
                sumR = 0m;
                foreach (var id in active) sumR += r[id];
            }

            // 10) Дискретное распределение планет среди активных узлов (market)
            var xMarket = new Dictionary<int, int>();
            if (active.Count == 0)
            {
                return new ProductionPlanResult
                {
                    ProductTypeId = productTypeId,
                    PlanetsUsed = 0,
                    OutputPerDay = 0,
                    RevenuePerDay = 0,
                    CostsPerDay = 0,
                    Recipe = recipe,
                    Note = "No active production nodes."
                };
            }

            int requiredMinMarket = active.Count;
            if (planets < requiredMinMarket)
            {
                // крайний случай: оставляем только финал
                foreach (var id in new List<int>(active))
                    if (id != finalTypeId) { purchasedNodes.Add(id); active.Remove(id); }
                requiredMinMarket = 1;
            }

            foreach (var id in active) xMarket[id] = 1;
            int remainingMarket = Math.Max(0, planets - requiredMinMarket);

            decimal RatioM(int id) => xMarket[id] / (r[id] == 0 ? 1m : r[id]);
            while (remainingMarket > 0 && active.Count > 0)
            {
                var target = active.OrderBy(RatioM).First();
                xMarket[target] = xMarket[target] + 1;
                remainingMarket--;
            }

            decimal unitsFactorM = active.Min(id => xMarket[id] / (r[id] == 0 ? 1m : r[id]));
            if (unitsFactorM < 0) unitsFactorM = 0;
            var outPerHourM = unitsFactorM * capFinalPerPlanet;
            var outPerDayM = outPerHourM * 24m;

            var planetPlansM = new List<PlanetPlan>();
            foreach (var id in active)
            {
                var node = produceNodes[id];
                var t = tierCache[id];

                var facs = nodePlanetFacilities[id];
                var perPlanetOut = nodeCapPerPlanet[id];
                var perFactoryIn = GetPerFactoryInputsPerHour(node);
                var perFactoryOut = GetPerFactoryThroughput(node);

                var inputsPerPlanet = new Dictionary<int, decimal>();
                if (perFactoryOut > 0)
                {
                    var scale = perPlanetOut / perFactoryOut;
                    foreach (var inp in node.Inputs)
                        if (perFactoryIn.TryGetValue(inp.InputTypeId, out var v))
                            inputsPerPlanet[inp.InputTypeId] = v * scale;
                }

                var role = t switch
                {
                    0 or 1 => "Extraction",
                    2 => "Factory-Advanced",
                    3 => "Factory-Advanced",
                    4 => "Factory-HighTech",
                    _ => "Factory"
                };

                int count = xMarket.TryGetValue(id, out var c) ? c : 0;
                for (int i = 0; i < count; i++)
                {
                    planetPlansM.Add(new PlanetPlan
                    {
                        Role = role,
                        ProductTypeId = id,
                        Facilities = new Dictionary<string, int>(facs),
                        InputPerHour = new Dictionary<int, decimal>(inputsPerPlanet),
                        OutputPerHour = new Dictionary<int, decimal> { [id] = perPlanetOut }
                    });
                }
            }

            var resultM = new ProductionPlanResult
            {
                ProductTypeId = productTypeId,
                PlanetsUsed = planetPlansM.Count,
                OutputPerDay = outPerDayM,
                RevenuePerDay = 0m,
                CostsPerDay = 0m,
                Recipe = recipe
            };
            resultM.Planets.AddRange(planetPlansM);

            foreach (var kv in demands.OrderBy(k => k.Key.tier).ThenBy(k => k.Key.typeId))
            {
                resultM.RequiredInputs.Add(new MaterialDemand
                {
                    TypeId = kv.Key.typeId,
                    Tier = kv.Key.tier,
                    QuantityPerHour = kv.Value
                });
            }

            string purchasedNote = purchasedNodes.Count > 0
                ? $"Purchased nodes: {string.Join(", ", purchasedNodes.Select(id => $"{id} (T{tierCache[id]})"))}"
                : "No purchased nodes.";
            resultM.Note =
                $"MODE=Market | Active={active.Count} | PlanetsUsed={resultM.PlanetsUsed} | Units≈{unitsFactorM:0.###} | {purchasedNote} | " +
                $"CC penalty {(settings.CcPenalty * 100):0.#}% | LP+Storage mandatory";

            return await Task.FromResult(resultM);
        }
    }
}
