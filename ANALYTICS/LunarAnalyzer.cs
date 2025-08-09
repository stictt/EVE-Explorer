using System;
using System.Collections.Generic;
using System.Linq;
using Domain.Services;       // SdeAggregateDTO, BlueprintRecipeDTO, RecipeLine, Kind
using Domain.Models.ResourceDTO;

namespace Domain.Analytics
{
    /// <summary>
    /// Универсальный калькулятор по луне в 3 режимах:
    ///  1) Ore -> Moon materials (маржа от BUY руды)
    ///  2) Терминальная реакция (Activity=11) — profit за 1 run, входы: рынок или через руду
    ///  3) Чертёж (Activity=1), требующий реакций — profit за 1 run
    /// Считает время (с учётом ТЕ), налог реакций (1% по 30d средней), МЕ реакций и МЕ чертежа отдельно.
    /// Учитывает RefineYield (0..1) для рефайна руды (и при режиме ViaOre).
    /// </summary>
    public sealed class LunarAnalyzer
    {
        private readonly SdeAggregateDTO _sde;

        // Цены:
        //  - avg30d: для налога (может вернуть null)
        //  - nowPrice: (buy,sell) для текущих расчётов
        public delegate double? AvgGetter(int typeId);
        public delegate (double buy, double sell) NowPxGetter(int typeId);

        private readonly AvgGetter _avg30d;
        private readonly NowPxGetter _nowPx;

        /// <summary>
        /// Доля выхода при рефайне руды (0..1). Применяется:
        ///  - в EvaluateOreRefine (к каждому материальному выходу);
        ///  - в режиме ViaOre при разворачивании реакций (материал с 1 ед. руды).
        /// По умолчанию 1.0 (100%).
        /// </summary>
        public double RefineYield { get; set; } = 1.0;

        public LunarAnalyzer(SdeAggregateDTO sde, AvgGetter avg30dGetter, NowPxGetter nowGetter)
        {
            _sde = sde;
            _avg30d = avg30dGetter;
            _nowPx = nowGetter;
        }

        // --------- Общие настройки эффективности ---------
        public sealed class Eff
        {
            /// <summary>Материальная эффективность реакций (0..1), 0.1 = -10% материалов.</summary>
            public double ReactionME { get; set; } = 0.0;
            /// <summary>Сокращение времени реакций (0..1), 0.2 = -20% времени.</summary>
            public double ReactionTE { get; set; } = 0.0;

            /// <summary>Материальная эффективность чертежа (manufacturing) (0..1).</summary>
            public double BpME { get; set; } = 0.0;
            /// <summary>Сокращение времени чертежа (manufacturing) (0..1).</summary>
            public double BpTE { get; set; } = 0.0;
        }

        public enum InputCostMode
        {
            MarketMaterials, // Входные лунные материалы берём по рынку
            ViaOre           // Стоимость материалов считаем через руду (рефайн) с учётом RefineYield
        }

        // --------- DTO результатов ---------

        public sealed class OreRefineResult
        {
            public int OreTypeID { get; set; }
            public string OreName { get; set; } = "";
            public bool IsCompressed { get; set; }
            public double UnitVolume { get; set; }

            public int PortionSize { get; set; }
            public double RefineYield { get; set; }
            public List<(int typeId, string name, double qtyPerUnit, double buy, double sell)> Outputs { get; set; } = new();

            public double OreBuy { get; set; }    // цена руды BUY / unit
            public double OreSell { get; set; }   // цена руды SELL / unit
            public double RefineSellPerUnit { get; set; } // сумма SELL материалов / unit (с учётом RefineYield)
            public double MarginPerUnit { get; set; }     // RefineSellPerUnit - OreBuy

            // Для «вместимости корабля»
            public double UnitsPerShip { get; set; }
            public double MarginPerShip { get; set; }
        }

        public sealed class ReactionResult
        {
            public int ProductTypeID { get; set; }
            public string ProductName { get; set; } = "";
            public double QtyOutPerRun { get; set; }

            public InputCostMode CostMode { get; set; }

            public Dictionary<int, double> BaseInputs { get; } = new(); // базовые (лунные материалы или руда) и их qty per 1 run
            public double BaseInputsCost { get; set; }  // стоимость базовых входов (по выбранному режиму)
            public double TotalTaxIsk { get; set; }     // суммарный налог по всем реакциям в цепочке
            public double RevenueBuy { get; set; }      // выручка (BUY)
            public double RevenueSell { get; set; }     // выручка (SELL)

            public double TimeSeconds { get; set; }     // суммарное время цепочки реакций per 1 run (с учётом TE и кратности)
            public double ProfitToBuy { get; set; }     // RevenueBuy - BaseInputsCost - TotalTaxIsk
            public double ProfitToSell { get; set; }    // RevenueSell - BaseInputsCost - TotalTaxIsk

            public double ProfitPerHour_ToBuy => TimeSeconds > 0 ? ProfitToBuy * 3600.0 / TimeSeconds : 0;
            public double ProfitPerHour_ToSell => TimeSeconds > 0 ? ProfitToSell * 3600.0 / TimeSeconds : 0;
        }

        public sealed class BlueprintResult
        {
            public int ProductTypeID { get; set; }
            public string ProductName { get; set; } = "";
            public double QtyOutPerRun { get; set; }

            public InputCostMode CostMode { get; set; }
            public Eff Efficiency { get; set; } = new();

            public Dictionary<int, double> BaseInputs { get; } = new(); // итоговые базовые входы (для закупки): лунные материалы или руда (по режиму) + прочие покупаемые материалы BP
            public double BaseInputsCost { get; set; }  // полная стоимость входов
            public double TotalTaxIsk { get; set; }     // только налоги реакций
            public double TimeSeconds { get; set; }     // время: реакции (с TE реакций) + мфг (с TE BP)

            public double RevenueBuy { get; set; }
            public double RevenueSell { get; set; }
            public double ProfitToBuy => RevenueBuy - BaseInputsCost - TotalTaxIsk;
            public double ProfitToSell => RevenueSell - BaseInputsCost - TotalTaxIsk;

            public double ProfitPerHour_ToBuy => TimeSeconds > 0 ? ProfitToBuy * 3600.0 / TimeSeconds : 0;
            public double ProfitPerHour_ToSell => TimeSeconds > 0 ? ProfitToSell * 3600.0 / TimeSeconds : 0;
        }

        // ===================== 1) ORE -> MOON MATERIALS =====================

        /// <summary>
        /// Расчёт маржи рефайна руды: маржа от BUY руды против суммы SELL материалов.
        /// Учитывает RefineYield. Если capacityM3 задан, добавляем «per ship».
        /// </summary>
        public OreRefineResult EvaluateOreRefine(int oreTypeId, double? capacityM3 = null)
        {
            if (!_sde.Items.TryGetValue(oreTypeId, out var ore))
                throw new InvalidOperationException($"Неизвестная руда {oreTypeId}.");

            if (!_sde.Reprocessing.TryGetValue(oreTypeId, out var plan))
                throw new InvalidOperationException($"Для руды {ore.Name} нет плана рефайна.");

            var orePx = _nowPx(oreTypeId);
            var res = new OreRefineResult
            {
                OreTypeID = oreTypeId,
                OreName = ore.Name,
                IsCompressed = ore.IsCompressed,
                UnitVolume = ore.Volume,
                PortionSize = plan.PortionSize,
                RefineYield = RefineYield
            };

            // Выход на 1 единицу руды: (quantity / PortionSize) * RefineYield
            double refineSell = 0;
            foreach (var o in plan.Outputs)
            {
                var unit = (o.Quantity / Math.Max(1.0, plan.PortionSize)) * RefineYield;
                var px = _nowPx(o.TypeID);
                refineSell += unit * (px.sell > 0 ? px.sell : px.buy);

                var name = _sde.Items.TryGetValue(o.TypeID, out var it) ? it.Name : o.TypeID.ToString();
                res.Outputs.Add((o.TypeID, name, unit, px.buy, px.sell));
            }

            res.OreBuy = orePx.buy;
            res.OreSell = orePx.sell;
            res.RefineSellPerUnit = refineSell;
            res.MarginPerUnit = refineSell - orePx.buy;

            if (capacityM3.HasValue && res.UnitVolume > 0)
            {
                res.UnitsPerShip = Math.Floor(capacityM3.Value / res.UnitVolume);
                res.MarginPerShip = res.UnitsPerShip * res.MarginPerUnit;
            }

            return res;
        }

        // ===================== 2) ТЕРМИНАЛЬНАЯ РЕАКЦИЯ =====================

        public List<(int productTypeId, string name, double qtyOut)> FindTerminalReactions()
        {
            var usedAsMat = new HashSet<int>(
                _sde.Recipes.Where(r => r.ActivityID == 11)
                            .SelectMany(r => r.Materials.Select(m => m.TypeID)));

            var term = new List<(int, string name, double)>();
            foreach (var r in _sde.Recipes.Where(x => x.ActivityID == 11))
            {
                foreach (var p in r.Products)
                {
                    if (!usedAsMat.Contains(p.TypeID))
                    {
                        var name = _sde.Items.TryGetValue(p.TypeID, out var it) ? it.Name : p.TypeID.ToString();
                        term.Add((p.TypeID, name, p.Quantity));
                    }
                }
            }
            return term.OrderBy(t => t.name).ToList();
        }

        public ReactionResult EvaluateReaction(int productTypeId, InputCostMode mode, Eff eff)
        {
            if (!_sde.RecipesByProduct.TryGetValue(productTypeId, out var list))
                throw new InvalidOperationException($"Продукт {productTypeId} не производится реакцией.");

            var rx = list.FirstOrDefault(r => r.ActivityID == 11);
            if (rx == null) throw new InvalidOperationException($"Для {productTypeId} нет реакции.");

            var outLine = rx.Products.First(p => p.TypeID == productTypeId);
            var name = _sde.Items.TryGetValue(productTypeId, out var itn) ? itn.Name : productTypeId.ToString();

            var result = new ReactionResult
            {
                ProductTypeID = productTypeId,
                ProductName = name,
                QtyOutPerRun = outLine.Quantity,
                CostMode = mode
            };

            var ctx = new AccumCtx(_sde, _avg30d, _nowPx, eff, RefineYield);
            var (baseMap, tax, timeSec, baseCost) = ExpandReactionToBase(rx, runs: 1.0, mode, ctx);

            foreach (var kv in baseMap)
                result.BaseInputs[kv.Key] = kv.Value;

            result.TotalTaxIsk = tax;
            result.TimeSeconds = timeSec;
            result.BaseInputsCost = baseCost;

            var p = _nowPx(productTypeId);
            result.RevenueBuy = p.buy * result.QtyOutPerRun;
            result.RevenueSell = p.sell * result.QtyOutPerRun;
            result.ProfitToBuy = result.RevenueBuy - result.BaseInputsCost - result.TotalTaxIsk;
            result.ProfitToSell = result.RevenueSell - result.BaseInputsCost - result.TotalTaxIsk;

            return result;
        }

        // ===================== 3) ЧЕРТЕЖ, ТРЕБУЮЩИЙ РЕАКЦИЙ =====================

        public List<(int productTypeId, string name, double qtyOut)> FindBlueprintsRequiringReactions()
        {
            var rxProducts = new HashSet<int>(
                _sde.Recipes.Where(r => r.ActivityID == 11)
                            .SelectMany(r => r.Products.Select(p => p.TypeID)));

            var res = new List<(int, string name, double)>();

            foreach (var r in _sde.Recipes.Where(r => r.ActivityID == 1)) // Manufacturing
            {
                if (!r.Materials.Any(m => rxProducts.Contains(m.TypeID)))
                    continue;

                foreach (var p in r.Products)
                {
                    var name = _sde.Items.TryGetValue(p.TypeID, out var it) ? it.Name : p.TypeID.ToString();
                    res.Add((p.TypeID, name, p.Quantity));
                }
            }

            return res.OrderBy(x => x.name).ToList();
        }

        public BlueprintResult EvaluateBlueprint(int productTypeId, InputCostMode mode, Eff eff)
        {
            if (!_sde.RecipesByProduct.TryGetValue(productTypeId, out var list))
                throw new InvalidOperationException($"Продукт {productTypeId} не производится мфг.");

            var bp = list.FirstOrDefault(r => r.ActivityID == 1);
            if (bp == null) throw new InvalidOperationException($"Для {productTypeId} нет Manufacturing рецепта.");

            var outLine = bp.Products.First(p => p.TypeID == productTypeId);
            var name = _sde.Items.TryGetValue(productTypeId, out var itn) ? itn.Name : productTypeId.ToString();

            var result = new BlueprintResult
            {
                ProductTypeID = productTypeId,
                ProductName = name,
                QtyOutPerRun = outLine.Quantity,
                CostMode = mode,
                Efficiency = eff
            };

            var bpMeMul = 1.0 - Clamp01(eff.BpME);
            var bpTeMul = 1.0 - Clamp01(eff.BpTE);
            double bpTime = Math.Max(0, bp.BaseTimeSeconds) * bpTeMul;

            var baseMap = new Dictionary<int, double>();
            double totalTax = 0;
            double totalTime = bpTime;
            double baseCost = 0;

            var rxProducts = new HashSet<int>(
                _sde.Recipes.Where(r => r.ActivityID == 11).SelectMany(r => r.Products.Select(p => p.TypeID)));

            var ctx = new AccumCtx(_sde, _avg30d, _nowPx, eff, RefineYield);

            foreach (var m in bp.Materials)
            {
                var needQty = m.Quantity * bpMeMul;

                if (rxProducts.Contains(m.TypeID))
                {
                    if (!_sde.RecipesByProduct.TryGetValue(m.TypeID, out var lst)) continue;
                    var rx = lst.FirstOrDefault(r => r.ActivityID == 11);
                    if (rx == null) continue;

                    var outRx = rx.Products.FirstOrDefault(p => p.TypeID == m.TypeID);
                    if (outRx == null || outRx.Quantity <= 0) continue;

                    double runs = needQty / outRx.Quantity;

                    var (bMap, tax, tSec, bCost) = ExpandReactionToBase(rx, runs, mode, ctx);
                    totalTax += tax;
                    totalTime += tSec;
                    baseCost += bCost;

                    Merge(baseMap, bMap);
                }
                else
                {
                    if (_sde.Items.TryGetValue(m.TypeID, out var it))
                    {
                        var px = _nowPx(m.TypeID);
                        baseCost += needQty * (px.sell > 0 ? px.sell : px.buy);
                        Add(baseMap, m.TypeID, needQty);
                    }
                }
            }

            var p = _nowPx(productTypeId);
            result.RevenueBuy = p.buy * result.QtyOutPerRun;
            result.RevenueSell = p.sell * result.QtyOutPerRun;

            result.BaseInputsCost = baseCost;
            result.TotalTaxIsk = totalTax;
            result.TimeSeconds = totalTime;

            foreach (var kv in baseMap) result.BaseInputs[kv.Key] = kv.Value;

            return result;
        }

        // ===================== ВСПОМОГАТЕЛЬНОЕ =====================

        private static double Clamp01(double v) => v < 0 ? 0 : (v > 1 ? 1 : v);

        private sealed class AccumCtx
        {
            public readonly SdeAggregateDTO Sde;
            public readonly AvgGetter Avg;
            public readonly NowPxGetter Px;
            public readonly Eff Eff;
            public readonly double RefineYield;

            public AccumCtx(SdeAggregateDTO sde, AvgGetter avg, NowPxGetter px, Eff eff, double refineYield)
            {
                Sde = sde; Avg = avg; Px = px; Eff = eff; RefineYield = refineYield;
            }
        }

        /// <summary>
        /// Разворачивает одну реакцию (activity=11) в базовые входы по выбранному режиму,
        /// учитывает МЕ/ТЕ реакций, возвращает:
        ///  - baseMap: базовые входы (typeId->qty) (в режиме MarketMaterials — лунные материалы, в ViaOre — руда)
        ///  - tax: 1% * сумма входов (по avg30d) * runs (на каждом шаге)
        ///  - timeSec: суммарное реакционное время (с TE, с кратностью runs, + рекурсия)
        ///  - baseCost: стоимость базовых входов (по рынку/через руду) для этой реакции и рекурсивных
        /// </summary>
        private (Dictionary<int, double> baseMap, double tax, double timeSec, double baseCost)
            ExpandReactionToBase(BlueprintRecipeDTO rx, double runs, InputCostMode mode, AccumCtx ctx)
        {
            var baseMap = new Dictionary<int, double>();
            double tax = 0;
            double time = 0;
            double cost = 0;

            var meMul = 1.0 - Clamp01(ctx.Eff.ReactionME);
            var teMul = 1.0 - Clamp01(ctx.Eff.ReactionTE);

            // Налог шага (по 30d средней, уже с МЕ реакций)
            double inputsAvgSum = 0;
            foreach (var m in rx.Materials)
            {
                var need = m.Quantity * meMul;
                var avg = ctx.Avg(m.TypeID) ?? 0;
                inputsAvgSum += avg * need;
            }
            tax += 0.01 * inputsAvgSum * runs;

            // Время шага:
            time += Math.Max(0, rx.BaseTimeSeconds) * teMul * runs;

            // Разворачиваем материалы
            foreach (var m in rx.Materials)
            {
                var needQty = m.Quantity * meMul;

                // Если материал сам — продукт реакции, спускаемся ниже
                if (ctx.Sde.RecipesByProduct.TryGetValue(m.TypeID, out var subList))
                {
                    var sub = subList.FirstOrDefault(r => r.ActivityID == 11);
                    if (sub != null)
                    {
                        var outLine = sub.Products.FirstOrDefault(p => p.TypeID == m.TypeID);
                        if (outLine != null && outLine.Quantity > 0)
                        {
                            double subRuns = runs * (needQty / outLine.Quantity);
                            var (bmap, ttax, ttime, tcost) = ExpandReactionToBase(sub, subRuns, mode, ctx);
                            Merge(baseMap, bmap);
                            tax += ttax;
                            time += ttime;
                            cost += tcost;
                            continue;
                        }
                    }
                }

                // Иначе это «лист» для реакций:
                if (mode == InputCostMode.MarketMaterials)
                {
                    // Базой считаем именно лунные материалы (их будем покупать)
                    Add(baseMap, m.TypeID, runs * needQty);
                    var px = ctx.Px(m.TypeID);
                    cost += runs * needQty * (px.sell > 0 ? px.sell : px.buy);
                }
                else // ViaOre
                {
                    // Стоимость через руду: проверяем лучшую руду для этого материала, учитывая RefineYield
                    var best = FindBestOreForMaterial(ctx.Sde, ctx.Px, m.TypeID, ctx.RefineYield);
                    if (best == null)
                    {
                        // фолбэк — как рынок материалов
                        Add(baseMap, m.TypeID, runs * needQty);
                        var px = ctx.Px(m.TypeID);
                        cost += runs * needQty * (px.sell > 0 ? px.sell : px.buy);
                    }
                    else
                    {
                        var (oreId, perOneOut) = best.Value; // perOneOut — сколько этого материала даёт 1 ед. руды с учётом RefineYield
                        if (perOneOut <= 0)
                        {
                            // фолбэк
                            Add(baseMap, m.TypeID, runs * needQty);
                            var px = ctx.Px(m.TypeID);
                            cost += runs * needQty * (px.sell > 0 ? px.sell : px.buy);
                        }
                        else
                        {
                            var oreUnits = runs * needQty / perOneOut;
                            Add(baseMap, oreId, oreUnits);
                            var px = ctx.Px(oreId);
                            cost += oreUnits * (px.sell > 0 ? px.sell : px.buy);
                        }
                    }
                }
            }

            return (baseMap, tax, time, cost);
        }

        /// <summary>
        /// Возвращает (oreTypeId, materialPerOneUnitOfOreWithYield) с минимальной ценой материала,
        /// где materialPerOne = (quantity / PortionSize) * RefineYield.
        /// Проверяются все виды руды (включая сжатую), у которых есть план рефайна на нужный материал.
        /// </summary>
        private static (int oreId, double matPerOne)? FindBestOreForMaterial(
            SdeAggregateDTO sde, NowPxGetter px, int materialTypeId, double refineYield)
        {
            (int oreId, double perOne, double costPerMat)? best = null;

            foreach (var it in sde.Items.Values)
            {
                // Разрешаем обычную/лунную/лёд — если в рефайне есть нужный материал
                if (it.Kind != Kind.Ore && it.Kind != Kind.MoonOre && it.Kind != Kind.IceOre)
                    continue;
                if (!sde.Reprocessing.TryGetValue(it.TypeID, out var plan)) continue;

                var outp = plan.Outputs.FirstOrDefault(o => o.TypeID == materialTypeId);
                if (outp == null || outp.Quantity <= 0) continue;

                var perOne = (outp.Quantity / Math.Max(1.0, plan.PortionSize)) * Math.Max(0.0, Math.Min(1.0, refineYield));
                if (perOne <= 0) continue;

                var p = px(it.TypeID);
                var oreUnitPrice = (p.sell > 0 ? p.sell : p.buy);
                if (oreUnitPrice <= 0) continue;

                var costPerMat = oreUnitPrice / perOne;

                // При равной цене — лёгкий приоритет сжатой руды (логистика)
                bool take =
                    best == null
                    || costPerMat < best.Value.costPerMat
                    || (Math.Abs(costPerMat - best.Value.costPerMat) < 1e-9 && it.IsCompressed);

                if (take)
                    best = (it.TypeID, perOne, costPerMat);
            }

            if (best == null) return null;
            return (best.Value.oreId, best.Value.perOne);
        }

        private static void Add(Dictionary<int, double> map, int key, double delta)
        {
            if (map.TryGetValue(key, out var cur)) map[key] = cur + delta;
            else map[key] = delta;
        }

        private static void Merge(Dictionary<int, double> into, Dictionary<int, double> add)
        {
            foreach (var kv in add) Add(into, kv.Key, kv.Value);
        }

        // -------------------- Утилиты для UI --------------------

        /// <summary>
        /// Сгенерировать текст "Name\tQty" по мапе базовых входов.
        /// </summary>
        public string MakeClipboardText(Dictionary<int, double> baseInputs, int decimals = 0)
        {
            var lines = new List<string>(baseInputs.Count);
            foreach (var kv in baseInputs.OrderBy(kv => _sde.Items.TryGetValue(kv.Key, out var it) ? it.Name : kv.Key.ToString()))
            {
                var name = _sde.Items.TryGetValue(kv.Key, out var it) ? it.Name : kv.Key.ToString();
                var qty = decimals <= 0 ? Math.Round(kv.Value).ToString("N0") : kv.Value.ToString("N" + decimals);
                lines.Add($"{name}\t{qty}");
            }
            return string.Join(Environment.NewLine, lines);
        }
    }
}
