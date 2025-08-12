// MoonIndustryForm.cs — «все руды» (Ore+Ice+Moon+Fullerene), поиск+порог рейтинга,
// разворот цепочек до листьев, округление вверх, сохранение настроек.
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

using Domain.Services;             // SdeAggregateDTO, BlueprintRecipeDTO, Kind
using Domain.Models.ResourceDTO;   // ItemExtDTO
using Loader.Infrastructure;       // BinaryCachingService, Paths
using ANALYTICS;
using Domain.Infrastructure;
using Domain.Models;
using static Domain.Models.InMemoryRecipeCatalog;

namespace TestForm
{
    public partial class MoonIndustryForm : Form
    {
        private readonly SdeAggregateDTO _sde;
        private readonly IPriceProvider _priceProvider;
        private readonly HistoryStats _hist = new();

        private int _lastRegionId = 10000002; // Jita

        // сохраняемое состояние
        private bool _lastUseOreIfLiquid;
        private bool _lastAllOres;
        private string _lastSearch = "";
        private double _lastMinRatingBn;          // NEW
        private double _lastMinOreRatingBn;
        private double _lastRefineYield;
        private double _lastSalesTaxPct;
        private double _lastBuyTaxPct;
        private double _lastReactMEpct, _lastReactTEpct;
        private double _lastBpoMEpct, _lastBpoTEpct;
        private double _lastReactJobTaxPct, _lastBpoJobTaxPct;
        private bool _lastInputsUseBuy = true;
        private int _lastModeIndex;

        // данные таблицы
        private List<RowVM> _lastRows = new();        // полный список
        private List<RowVM> _filteredRows = new();    // после поиска/фильтра
        private string? _sortProp;
        private bool _sortAsc = false;

        private const int ACT_MANUFACTURING = 1;
        private const int ACT_REACTION = 11;

        private Dictionary<int, BlueprintRecipeDTO> _reactionByProduct = new();
        private Dictionary<int, BlueprintRecipeDTO> _mfgByProduct = new();

        private InMemoryRecipeCatalog _catalog;
        private CraftPlanner _planner;
        private IRoundingPolicy _round;
        private CraftParamsSet _ps;

        private readonly string _settingsPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoonIndustryForm.settings.bin");

        public MoonIndustryForm(SdeAggregateDTO sde, IPriceProvider priceProvider)
        {
            InitializeComponent();

            _sde = sde;
            _priceProvider = priceProvider;

            _reactionByProduct = _sde.Recipes
                .Where(r => r.ActivityID == ACT_REACTION)
                .SelectMany(r => r.Products.Select(p => (p.TypeID, r)))
                .GroupBy(x => x.TypeID)
                .ToDictionary(g => g.Key, g => g.First().r);

            _mfgByProduct = _sde.Recipes
                .Where(r => r.ActivityID == ACT_MANUFACTURING)
                .SelectMany(r => r.Products.Select(p => (p.TypeID, r)))
                .GroupBy(x => x.TypeID)
                .ToDictionary(g => g.Key, g => g.First().r);

            InitCraftKernel();
            ApplyDarkTweaks();

            // загрузка настроек
            LoadSettingsSafe();

            try
            {
                rbModeOre.Checked = (_lastModeIndex == 0);
                rbModeReactions.Checked = (_lastModeIndex == 1);
                rbModeBlueprints.Checked = (_lastModeIndex == 2);

                chkUseOre.Checked = _lastUseOreIfLiquid;
                chkAllOres.Checked = _lastAllOres;

                txtSearch.Text = _lastSearch ?? "";
                numMinRatingBn.Value = (decimal)_lastMinRatingBn;

                numMinOreRatingBn.Value = (decimal)_lastMinOreRatingBn;
                numRefine.Value = (decimal)_lastRefineYield;

                numSalesTax.Value = (decimal)_lastSalesTaxPct;
                numBuyTax.Value = (decimal)_lastBuyTaxPct;

                numReactME.Value = (decimal)_lastReactMEpct;
                numReactTE.Value = (decimal)_lastReactTEpct;
                numReactJobTax.Value = (decimal)_lastReactJobTaxPct;

                numBpoME.Value = (decimal)_lastBpoMEpct;
                numBpoTE.Value = (decimal)_lastBpoTEpct;
                numBpoJobTax.Value = (decimal)_lastBpoJobTaxPct;

                rbInputsUseBuy.Checked = _lastInputsUseBuy;
                rbInputsUseSell.Checked = !_lastInputsUseBuy;
            }
            catch { /* ignore */ }

            btnCalc.Click += async (_, __) => await RecalcAsync();
            txtSearch.TextChanged += (_, __) => ApplySearchFilter();
            numMinRatingBn.ValueChanged += (_, __) => ApplySearchFilter();

            grid.CellDoubleClick += Grid_CellDoubleClick;
            grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
            grid.CellFormatting += Grid_CellFormatting;

            _ = RecalcAsync();
        }

        private void InitCraftKernel()
        {
            _catalog = new InMemoryRecipeCatalog();
            _round = new DefaultRoundingPolicy();
            _planner = new CraftPlanner(_catalog, _round);
            _ps = new CraftParamsSet
            {
                Manufacturing = new CraftParams { ME = 0m, TE = 0m, FacilityMatMul = 1m, FacilityTimeMul = 1m, TaxRate = 0m },
                Reaction = new CraftParams { ME = 0m, TE = 0m, FacilityMatMul = 1m, FacilityTimeMul = 1m, TaxRate = 0m }
            };

            _catalog.LoadReactions(MapFromSdeReactions());
            _catalog.LoadManufacturing(MapFromSdeManufacturing());
        }

        private IEnumerable<Recipe> MapFromSdeReactions()
        {
            foreach (var rec in _sde.Recipes.Where(r => r.ActivityID == ACT_REACTION))
            {
                var main = rec.Products.FirstOrDefault();
                if (main == null) continue;

                yield return new Recipe
                {
                    Id = new RecipeId { Kind = CraftKind.Reaction, BlueprintTypeId = 0, Activity = Activity.Reaction },
                    Output = new Product { Item = new ItemRef { TypeId = main.TypeID, Name = FindItem(main.TypeID)?.Name }, QtyPerRun = main.Quantity },
                    Inputs = rec.Materials.Select(m => new Ingredient
                    {
                        Item = new ItemRef { TypeId = m.TypeID, Name = FindItem(m.TypeID)?.Name },
                        QtyPerRun = m.Quantity
                    }).ToList(),
                    Byproducts = rec.Products.Skip(1).Select(p => new Product
                    {
                        Item = new ItemRef { TypeId = p.TypeID, Name = FindItem(p.TypeID)?.Name },
                        QtyPerRun = p.Quantity
                    }).ToList(),
                    BaseTimePerRun = TimeSpan.FromSeconds(Math.Max(1, rec.BaseTimeSeconds))
                };
            }
        }

        private IEnumerable<Recipe> MapFromSdeManufacturing()
        {
            foreach (var rec in _sde.Recipes.Where(r => r.ActivityID == ACT_MANUFACTURING))
            {
                var main = rec.Products.FirstOrDefault();
                if (main == null) continue;

                yield return new Recipe
                {
                    Id = new RecipeId { Kind = CraftKind.Blueprint, BlueprintTypeId = 0, Activity = Activity.Manufacturing },
                    Output = new Product { Item = new ItemRef { TypeId = main.TypeID, Name = FindItem(main.TypeID)?.Name }, QtyPerRun = main.Quantity },
                    Inputs = rec.Materials.Select(m => new Ingredient
                    {
                        Item = new ItemRef { TypeId = m.TypeID, Name = FindItem(m.TypeID)?.Name },
                        QtyPerRun = m.Quantity
                    }).ToList(),
                    Byproducts = rec.Products.Skip(1).Select(p => new Product
                    {
                        Item = new ItemRef { TypeId = p.TypeID, Name = FindItem(p.TypeID)?.Name },
                        QtyPerRun = p.Quantity
                    }).ToList(),
                    BaseTimePerRun = TimeSpan.FromSeconds(Math.Max(1, rec.BaseTimeSeconds))
                };
            }
        }

        private void ApplyDarkTweaks()
        {
            try { UiStyle.ApplyDark(this); } catch { }

            Color caption = Color.Gainsboro;
            void styleContainer(Control root)
            {
                foreach (Control c in root.Controls)
                {
                    if (c is GroupBox gb)
                    {
                        gb.ForeColor = caption;
                        foreach (Control x in gb.Controls)
                            if (x is Label or CheckBox or RadioButton) x.ForeColor = caption;
                    }
                    styleContainer(c);
                }
            }
            styleContainer(panelTop);

            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.Gainsboro;
        }

        // ===== модели =====
        private sealed class RowVM
        {
            public int ProductTypeID { get; set; }
            public string Name { get; set; } = "";

            public bool IsOreMode { get; set; }
            public bool IsReactionMode { get; set; }
            public bool IsBlueprintMode { get; set; }

            public double Rating { get; set; }
            public double AvgVolume { get; set; }

            public double UnitVolume { get; set; }
            public double UnitsPerCycle { get; set; }
            public double CycleSecondsEff { get; set; }

            public double InputsCostIsk { get; set; }
            public double BuyTaxIsk { get; set; }
            public double ReactionTaxIsk { get; set; }
            public double BpoTaxIsk { get; set; }
            public double RevenueSellIsk { get; set; }
            public double SalesTaxIsk { get; set; }

            public double ProfitPerCycle { get; set; }
            public double ProfitPerUnit { get; set; }
            public double ProfitPerM3 { get; set; }
            public double ProfitPerHour { get; set; }

            public List<PartLine> Inputs { get; set; } = new();
            public List<PartLine> Outputs { get; set; } = new();

            public List<(int typeId, double qty)> RootMaterials { get; set; } = new();
            public HashSet<int> ReactionRootTypeIds { get; set; } = new();
        }

        private sealed class PartLine
        {
            public int TypeID { get; set; }
            public string Name { get; set; } = "";
            public double Quantity { get; set; }
            public double UnitPrice { get; set; }
            public double Sum => UnitPrice * Quantity;
        }

        private sealed class HistoryStats
        {
            private readonly Dictionary<int, (double rating, double avgVol)> _byType = new();

            public HistoryStats()
            {
                try
                {
                    var cache = new BinaryCachingService();
                    if (cache.TryLoad<OrderHistoryMonthList>(Paths.OrderHistoryMonthPath, out var data, out var _) && data != null)
                    {
                        foreach (var i in data.List)
                            _byType[i.TypeId] = (i.Rating, i.AverageVolume);
                    }
                }
                catch { }
            }

            public bool TryGet(int typeId, out double rating, out double avgVol)
            {
                if (_byType.TryGetValue(typeId, out var v))
                { rating = v.rating; avgVol = v.avgVol; return true; }
                rating = 0; avgVol = 0; return false;
            }

            public double TryRatingBn(int typeId)
                => _byType.TryGetValue(typeId, out var v) ? (v.rating / 1_000_000_000.0) : 0.0;
        }

        // удобное округление вверх
        private static double CeilQty(double v) => Math.Ceiling(v - 1e-9);

        // ===== пересчёт =====
        private async System.Threading.Tasks.Task RecalcAsync()
        {
            btnCalc.Enabled = false;
            statusLabel.Text = "Сбор цен...";
            progress.Value = 0;

            int regionId = _lastRegionId;

            bool useOreIfLiquid = chkUseOre.Checked;
            bool includeAllOres = chkAllOres.Checked;
            double minOreRatingBn = (double)numMinOreRatingBn.Value;
            double refineYield = (double)numRefine.Value / 100.0;

            double salesTax = (double)numSalesTax.Value / 100.0;
            double buyTax = (double)numBuyTax.Value / 100.0;

            double reactME = (double)numReactME.Value / 100.0;
            double reactTE = (double)numReactTE.Value / 100.0;
            double reactJobTaxPct = (double)numReactJobTax.Value / 100.0;

            double bpoME = (double)numBpoME.Value / 100.0;
            double bpoTE = (double)numBpoTE.Value / 100.0;
            double bpoJobTaxPct = (double)numBpoJobTax.Value / 100.0;

            bool inputsUseBuy = rbInputsUseBuy.Checked;

            int mode = rbModeOre.Checked ? 0 : rbModeReactions.Checked ? 1 : 2;

            // сохранить last*
            _lastUseOreIfLiquid = useOreIfLiquid;
            _lastAllOres = includeAllOres;
            _lastSearch = txtSearch.Text ?? "";
            _lastMinRatingBn = (double)numMinRatingBn.Value;
            _lastMinOreRatingBn = minOreRatingBn;
            _lastRefineYield = refineYield * 100.0;
            _lastSalesTaxPct = salesTax * 100.0;
            _lastBuyTaxPct = buyTax * 100.0;
            _lastReactMEpct = reactME * 100.0;
            _lastReactTEpct = reactTE * 100.0;
            _lastBpoMEpct = bpoME * 100.0;
            _lastBpoTEpct = bpoTE * 100.0;
            _lastReactJobTaxPct = reactJobTaxPct * 100.0;
            _lastBpoJobTaxPct = bpoJobTaxPct * 100.0;
            _lastInputsUseBuy = inputsUseBuy;
            _lastModeIndex = mode;

            _ps.Reaction.ME = (decimal)reactME;
            _ps.Reaction.TE = (decimal)reactTE;
            _ps.Manufacturing.ME = (decimal)bpoME;
            _ps.Manufacturing.TE = (decimal)bpoTE;

            try
            {
                var rows = new List<RowVM>(512);
                var needPrices = new HashSet<int>();

                if (mode == 0)
                    BuildModeOre(rows, needPrices, refineYield, includeAllOres);
                else if (mode == 1)
                    BuildModeReactions(rows, needPrices);
                else
                    BuildModeBlueprints(rows, needPrices);

                foreach (var r in rows)
                {
                    foreach (var p in r.Inputs) needPrices.Add(p.TypeID);
                    foreach (var p in r.Outputs) needPrices.Add(p.TypeID);
                    needPrices.Add(r.ProductTypeID);
                    foreach (var rm in r.RootMaterials) needPrices.Add(rm.typeId);
                }

                var prices = await _priceProvider.GetBestPricesAsync(needPrices, regionId, new System.Threading.CancellationToken());
                progress.Value = 50;
                statusLabel.Text = "Расчёт...";

                double PriceBuy(int id) => prices.TryGetValue(id, out var p) ? p.bestBuy : 0;
                double PriceSell(int id) => prices.TryGetValue(id, out var p) ? p.bestSell : 0;

                foreach (var r in rows)
                {
                    double inputsCost = 0;
                    foreach (var i in r.Inputs)
                    {
                        double unit = inputsUseBuy ? PriceBuy(i.TypeID) : PriceSell(i.TypeID);
                        i.UnitPrice = unit;
                        inputsCost += i.Quantity * unit;
                    }

                    double buyTaxIsk = inputsCost * buyTax;

                    double reactBase = 0, bpoBase = 0;
                    if (!r.IsOreMode)
                    {
                        foreach (var (typeId, qty) in r.RootMaterials)
                        {
                            // базу таксов берём из SELL, чтобы не занижать
                            double unit = PriceSell(typeId);
                            var add = qty * unit;
                            bpoBase += add;
                            if (r.ReactionRootTypeIds.Contains(typeId))
                                reactBase += add;
                        }
                    }
                    double reactTax = reactBase * reactJobTaxPct;
                    double bpoTax = (r.IsBlueprintMode ? bpoBase * bpoJobTaxPct : 0);

                    double revenue = 0;
                    foreach (var o in r.Outputs)
                    {
                        double unitSell = PriceSell(o.TypeID);
                        o.UnitPrice = unitSell;
                        revenue += o.Quantity * unitSell;
                    }

                    double sellTax = revenue * salesTax;

                    r.InputsCostIsk = inputsCost;
                    r.BuyTaxIsk = buyTaxIsk;
                    r.ReactionTaxIsk = reactTax;
                    r.BpoTaxIsk = bpoTax;
                    r.RevenueSellIsk = revenue;
                    r.SalesTaxIsk = sellTax;

                    r.ProfitPerCycle = revenue - sellTax - inputsCost - buyTaxIsk - reactTax - bpoTax;

                    double mainUnits = Math.Max(1, r.UnitsPerCycle);
                    r.ProfitPerUnit = r.ProfitPerCycle / mainUnits;
                    r.ProfitPerM3 = r.UnitVolume > 0 ? r.ProfitPerUnit / r.UnitVolume : 0;
                    r.ProfitPerHour = (r.CycleSecondsEff > 0) ? r.ProfitPerCycle * (3600.0 / r.CycleSecondsEff) : 0;
                }

                if (useOreIfLiquid && _lastMinOreRatingBn > 0 && mode == 0)
                    rows = rows.Where(r => _hist.TryRatingBn(r.ProductTypeID) >= minOreRatingBn).ToList();

                var sorted = rows.OrderByDescending(r => r.ProfitPerHour)
                                 .ThenByDescending(r => r.ProfitPerCycle)
                                 .ToList();

                _lastRows = sorted;
                _sortProp = null;

                ApplySearchFilter(); // сразу применяем поиск/порог
                statusLabel.Text = $"Готово: {_filteredRows.Count} позиций";
                progress.Value = 100;

                SaveSettingsSafe();
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Ошибка: " + ex.Message;
            }
            finally
            {
                btnCalc.Enabled = true;
            }
        }

        // ---- поиск + фильтр рейтинга ----
        private void ApplySearchFilter()
        {
            var s = (txtSearch.Text ?? "").Trim().ToLowerInvariant();
            double minBn = (double)numMinRatingBn.Value;

            IEnumerable<RowVM> src = _lastRows;

            if (minBn > 0)
                src = src.Where(r => _hist.TryRatingBn(r.ProductTypeID) >= minBn);

            if (!string.IsNullOrEmpty(s))
                src = src.Where(r =>
                    (!string.IsNullOrEmpty(r.Name) && r.Name.ToLowerInvariant().Contains(s))
                    || r.ProductTypeID.ToString().Contains(s));

            _filteredRows = src.ToList();
            grid.DataSource = new BindingList<RowVM>(_filteredRows);
            FormatGrid(grid);
        }

        // ------- режимы -------
        private void BuildModeOre(List<RowVM> rows, HashSet<int> needPrices, double refineYield, bool includeAllOres)
        {
            // только добываемые ресурсы:
            // MoonOre / Fullerene всегда, плюс (если галка) Ore и IceOre.
            var ores = _sde.Items.Values
                .Where(i => i.Published)
                .Where(i =>
                       i.Kind == Kind.MoonOre ||
                       i.Kind == Kind.Fullerene ||
                       (includeAllOres && (i.Kind == Kind.Ore || i.Kind == Kind.IceOre)))
                .ToList();

            foreach (var ore in ores)
            {
                var row = new RowVM
                {
                    ProductTypeID = ore.TypeID,
                    Name = ore.Name,
                    IsOreMode = true,
                    UnitVolume = ore.Volume,
                    UnitsPerCycle = 1,
                    CycleSecondsEff = 0
                };

                if (_hist.TryGet(ore.TypeID, out var rating, out var avg))
                { row.Rating = rating; row.AvgVolume = avg; }

                row.Inputs.Add(new PartLine { TypeID = ore.TypeID, Name = ore.Name, Quantity = 1 });
                row.RootMaterials.Add((ore.TypeID, 1));

                if (_sde.Reprocessing.TryGetValue(ore.TypeID, out var plan))
                {
                    var portion = Math.Max(1, plan.PortionSize);
                    foreach (var outp in plan.Outputs)
                    {
                        var qty = (outp.Quantity / (double)portion) * refineYield;
                        row.Outputs.Add(new PartLine
                        {
                            TypeID = outp.TypeID,
                            Name = outp.Name ?? outp.TypeID.ToString(),
                            Quantity = qty
                        });
                        needPrices.Add(outp.TypeID);
                    }
                }

                needPrices.Add(ore.TypeID);
                rows.Add(row);
            }
        }

        private void BuildModeReactions(List<RowVM> rows, HashSet<int> needPrices)
        {
            var reactionRecipes = _sde.Recipes.Where(r => r.ActivityID == ACT_REACTION).ToList();
            var allReactionInputs = new HashSet<int>(reactionRecipes.SelectMany(r => r.Materials).Select(m => m.TypeID));
            var terminal = reactionRecipes.Where(r => r.Products.All(p => !allReactionInputs.Contains(p.TypeID))).ToList();

            var opt = new BuildOptions
            {
                Mode = KindResolutionMode.Mixed,
                AllowedActivities = new List<Activity> { Activity.Reaction },
                AllowReplacements = false
            };

            foreach (var rec in terminal)
            {
                var main = rec.Products.FirstOrDefault();
                if (main == null) continue;

                var item = FindItem(main.TypeID);
                var root = _planner.BuildMixed(
                    new ItemRef { TypeId = main.TypeID, Name = item?.Name },
                    targetQty: (decimal)Math.Max(1, main.Quantity),
                    ps: _ps,
                    opt: opt);

                var flat = CraftAnalysis.FlattenLeaves(root);

                var row = new RowVM
                {
                    ProductTypeID = main.TypeID,
                    Name = item?.Name ?? main.TypeID.ToString(),
                    IsReactionMode = true,
                    UnitsPerCycle = main.Quantity,
                    UnitVolume = item?.Volume ?? 0,
                    CycleSecondsEff = CraftAnalysis.ComputeNodeTime(root, _round).TotalSeconds
                };

                if (_hist.TryGet(main.TypeID, out var rating, out var avg))
                { row.Rating = rating; row.AvgVolume = avg; }

                row.Inputs = flat.Select(kv => new PartLine
                {
                    TypeID = kv.Key,
                    Name = FindItem(kv.Key)?.Name ?? kv.Key.ToString(),
                    Quantity = CeilQty((double)kv.Value)   // округляем вверх до целого
                })
                .OrderBy(p => p.Name)
                .ToList();

                foreach (var m in rec.Materials)
                {
                    double need = m.Quantity * (1.0 - (double)_ps.Reaction.ME);
                    row.RootMaterials.Add((m.TypeID, need));
                    row.ReactionRootTypeIds.Add(m.TypeID);
                    needPrices.Add(m.TypeID);
                }

                foreach (var p in rec.Products)
                {
                    var it = FindItem(p.TypeID);
                    row.Outputs.Add(new PartLine { TypeID = p.TypeID, Name = it?.Name ?? p.TypeID.ToString(), Quantity = p.Quantity });
                    needPrices.Add(p.TypeID);
                }

                foreach (var i in row.Inputs) needPrices.Add(i.TypeID);
                rows.Add(row);
            }
        }

        private void BuildModeBlueprints(List<RowVM> rows, HashSet<int> needPrices)
        {
            var mfg = _sde.Recipes.Where(r => r.ActivityID == ACT_MANUFACTURING).ToList();
            var reactionProducts = new HashSet<int>(
                _sde.Recipes.Where(r => r.ActivityID == ACT_REACTION)
                            .SelectMany(r => r.Products)
                            .Select(p => p.TypeID));

            // смешанный разворот до листьев
            var opt = new BuildOptions
            {
                Mode = KindResolutionMode.Mixed,
                AllowedActivities = new List<Activity> { Activity.Manufacturing, Activity.Reaction },
                AllowReplacements = false
            };

            foreach (var rec in mfg)
            {
                var main = rec.Products.FirstOrDefault();
                if (main == null) continue;

                var item = FindItem(main.TypeID);
                var row = new RowVM
                {
                    ProductTypeID = main.TypeID,
                    Name = item?.Name ?? main.TypeID.ToString(),
                    IsBlueprintMode = true,
                    UnitsPerCycle = main.Quantity,
                    UnitVolume = item?.Volume ?? 0,
                    CycleSecondsEff = Math.Max(1, rec.BaseTimeSeconds) * (1.0 - (double)_ps.Manufacturing.TE)
                };

                if (_hist.TryGet(main.TypeID, out var rating, out var avg))
                { row.Rating = rating; row.AvgVolume = avg; }

                // 1) низшие материалы (листья)
                var root = _planner.BuildMixed(
                    new ItemRef { TypeId = main.TypeID, Name = item?.Name },
                    targetQty: (decimal)Math.Max(1, main.Quantity),
                    ps: _ps,
                    opt: opt);

                var flat = CraftAnalysis.FlattenLeaves(root);
                row.Inputs = flat.Select(kv => new PartLine
                {
                    TypeID = kv.Key,
                    Name = FindItem(kv.Key)?.Name ?? kv.Key.ToString(),
                    Quantity = CeilQty((double)kv.Value)
                })
                .OrderBy(x => x.Name)
                .ToList();

                foreach (var i in row.Inputs) needPrices.Add(i.TypeID);

                // 2) верхние материалы рецепта — база для таксов
                foreach (var m in rec.Materials)
                {
                    var needPerRun = m.Quantity * (1.0 - (double)_ps.Manufacturing.ME);
                    row.RootMaterials.Add((m.TypeID, needPerRun));
                    if (reactionProducts.Contains(m.TypeID))
                        row.ReactionRootTypeIds.Add(m.TypeID);
                    needPrices.Add(m.TypeID);
                }

                // 3) выходы
                foreach (var p in rec.Products)
                {
                    var it = FindItem(p.TypeID);
                    row.Outputs.Add(new PartLine
                    {
                        TypeID = p.TypeID,
                        Name = it?.Name ?? p.TypeID.ToString(),
                        Quantity = p.Quantity
                    });
                    needPrices.Add(p.TypeID);
                }

                rows.Add(row);
            }
        }

        private ItemExtDTO? FindItem(int typeId)
            => _sde.Items.TryGetValue(typeId, out var it) ? it : null;

        // ===== двойной клик -> детализация =====
        private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _filteredRows.Count) return;
            var r = _filteredRows[e.RowIndex];

            // считаем «на 1 цикл» и разворачиваем до листьев
            var inputsLeaf = new List<(int TypeID, string Name, double Qty)>();

            if (r.IsReactionMode || r.IsBlueprintMode)
            {
                var allowed = new List<Activity> { Activity.Manufacturing, Activity.Reaction };
                var opt = new BuildOptions { Mode = KindResolutionMode.Mixed, AllowedActivities = allowed };

                var leafMap = CraftFacade.GetResourcesForUnits(
                    _planner,
                    _catalog,
                    new ItemRef { TypeId = r.ProductTypeID, Name = r.Name },
                    targetUnits: (decimal)Math.Max(1.0, r.UnitsPerCycle),
                    ps: _ps,
                    opt: opt);

                inputsLeaf = leafMap
                    .Select(kv => (kv.Key, FindItem(kv.Key)?.Name ?? kv.Key.ToString(), CeilQty((double)kv.Value)))
                    .ToList();
            }
            else
            {
                // режим руды – как есть
                inputsLeaf = r.Inputs.Select(i => (i.TypeID, i.Name, i.Quantity)).ToList();
            }

            var payload = new
            {
                ProductTypeID = r.ProductTypeID,
                Name = r.Name,
                UnitsPerCycle = r.UnitsPerCycle,
                CycleSecondsEff = r.CycleSecondsEff,
                Inputs = inputsLeaf.Select(x => new { TypeID = x.Item1, Name = x.Item2, Quantity = x.Item3 }).ToList(),
                Outputs = r.Outputs.Select(o => new { o.TypeID, o.Name, Quantity = o.Quantity }).ToList(),
                ChainNotes = Array.Empty<string>()
            };

            try
            {
                using var dlg = new MoonItemDetailsForm(payload, _priceProvider, _lastRegionId);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.ToString(), "Ошибка открытия деталей", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ===== сортировка =====
        private void Grid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || _filteredRows.Count == 0) return;

            var col = grid.Columns[e.ColumnIndex];
            var prop = col.DataPropertyName;
            if (string.IsNullOrEmpty(prop)) return;

            if (_sortProp == prop) _sortAsc = !_sortAsc;
            else { _sortProp = prop; _sortAsc = true; }

            var pinfo = typeof(RowVM).GetProperty(prop);
            if (pinfo == null) return;

            var sorted = (_sortAsc
                ? _filteredRows.OrderBy(r => pinfo.GetValue(r, null))
                : _filteredRows.OrderByDescending(r => pinfo.GetValue(r, null))).ToList();

            _filteredRows = sorted;
            grid.DataSource = new BindingList<RowVM>(_filteredRows);

            foreach (DataGridViewColumn c in grid.Columns)
                c.HeaderCell.SortGlyphDirection = SortOrder.None;
            col.HeaderCell.SortGlyphDirection = _sortAsc ? SortOrder.Ascending : SortOrder.Descending;
        }

        // ===== формат «до 1 знака» =====
        private static readonly HashSet<string> _colsN1 = new(StringComparer.OrdinalIgnoreCase)
        {
            "UnitsPerCycle","UnitVolume","CycleSecondsEff",
            "InputsCostIsk","BuyTaxIsk","ReactionTaxIsk","BpoTaxIsk",
            "RevenueSellIsk","SalesTaxIsk",
            "ProfitPerCycle","ProfitPerUnit","ProfitPerM3","ProfitPerHour"
        };

        private static readonly HashSet<string> _colsN0 = new(StringComparer.OrdinalIgnoreCase)
        {
            "Rating","AvgVolume"
        };

        private void Grid_CellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.Value is null) return;
            var g = (DataGridView)sender!;
            var prop = g.Columns[e.ColumnIndex].DataPropertyName;
            if (string.IsNullOrEmpty(prop)) return;

            double d;
            if (e.Value is double dd) d = dd;
            else if (!double.TryParse(Convert.ToString(e.Value, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out d))
                return;

            var culture = CultureInfo.CurrentCulture;

            if (_colsN0.Contains(prop))
            {
                e.Value = d.ToString("N0", culture);
                e.FormattingApplied = true;
                return;
            }

            if (_colsN1.Contains(prop))
            {
                string fmt = (Math.Abs(d - Math.Round(d)) < 0.05) ? "N0" : "N1";
                e.Value = d.ToString(fmt, culture);
                e.FormattingApplied = true;
            }
        }

        private static void FormatGrid(DataGridView g)
        {
            g.AutoGenerateColumns = false;

            if (g.Columns.Count == 0)
            {
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProductTypeID", HeaderText = "TypeID", Width = 80, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Название", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.Programmatic });

                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Rating", HeaderText = "Rating", Width = 110, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AvgVolume", HeaderText = "AvgVol", Width = 100, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });

                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UnitsPerCycle", HeaderText = "шт/цикл", Width = 80, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UnitVolume", HeaderText = "м³/шт", Width = 70, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CycleSecondsEff", HeaderText = "сек/цикл", Width = 90, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });

                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "InputsCostIsk", HeaderText = "Входы", Width = 120, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BuyTaxIsk", HeaderText = "Налог покупки", Width = 120, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReactionTaxIsk", HeaderText = "Налог реакц.", Width = 110, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "BpoTaxIsk", HeaderText = "Налог BPO", Width = 110, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });

                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RevenueSellIsk", HeaderText = "Выручка (SELL)", Width = 130, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SalesTaxIsk", HeaderText = "Налог продажи", Width = 120, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });

                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProfitPerCycle", HeaderText = "Профит/цикл", Width = 110, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProfitPerUnit", HeaderText = "Профит/шт", Width = 110, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProfitPerM3", HeaderText = "Профит/м³", Width = 110, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProfitPerHour", HeaderText = "Профит/час", Width = 110, SortMode = DataGridViewColumnSortMode.Programmatic, ValueType = typeof(double) });
            }

            foreach (DataGridViewColumn c in g.Columns)
            {
                if (c.ValueType == typeof(double))
                    c.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }
        }



        private void SaveSettingsSafe()
        {
            try
            {
                var s = new FormSettings
                {
                    ModeIndex = rbModeOre.Checked ? 0 : rbModeReactions.Checked ? 1 : 2,
                    UseOre = chkUseOre.Checked,
                    AllOres = chkAllOres.Checked,
                    Search = txtSearch.Text ?? "",
                    MinRatingBn = (double)numMinRatingBn.Value,
                    MinOreRatingBn = (double)numMinOreRatingBn.Value,
                    RefinePct = (double)numRefine.Value,

                    SalesTaxPct = (double)numSalesTax.Value,
                    BuyTaxPct = (double)numBuyTax.Value,

                    ReactMEpct = (double)numReactME.Value,
                    ReactTEpct = (double)numReactTE.Value,
                    ReactJobTaxPct = (double)numReactJobTax.Value,

                    BpoMEpct = (double)numBpoME.Value,
                    BpoTEpct = (double)numBpoTE.Value,
                    BpoJobTaxPct = (double)numBpoJobTax.Value,

                    InputsUseBuy = rbInputsUseBuy.Checked,

                    Location = this.WindowState == FormWindowState.Normal ? this.Location : this.RestoreBounds.Location,
                    Size = this.WindowState == FormWindowState.Normal ? this.Size : this.RestoreBounds.Size,
                    WindowState = this.WindowState
                };

                using var fs = File.Open(_settingsPath, FileMode.Create, FileAccess.Write);
                var bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
#pragma warning disable SYSLIB0011
                bf.Serialize(fs, s);
#pragma warning restore SYSLIB0011
            }
            catch { /* ignore */ }
        }

        private void LoadSettingsSafe()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return;

                using var fs = File.OpenRead(_settingsPath);
                var bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
#pragma warning disable SYSLIB0011
                var s = (FormSettings?)bf.Deserialize(fs);
#pragma warning restore SYSLIB0011
                if (s == null) return;

                _lastModeIndex = s.ModeIndex;
                _lastUseOreIfLiquid = s.UseOre;
                _lastAllOres = s.AllOres;
                _lastSearch = s.Search ?? "";
                _lastMinRatingBn = s.MinRatingBn;
                _lastMinOreRatingBn = s.MinOreRatingBn;
                _lastRefineYield = s.RefinePct;

                _lastSalesTaxPct = s.SalesTaxPct;
                _lastBuyTaxPct = s.BuyTaxPct;

                _lastReactMEpct = s.ReactMEpct;
                _lastReactTEpct = s.ReactTEpct;
                _lastReactJobTaxPct = s.ReactJobTaxPct;

                _lastBpoMEpct = s.BpoMEpct;
                _lastBpoTEpct = s.BpoTEpct;
                _lastBpoJobTaxPct = s.BpoJobTaxPct;

                _lastInputsUseBuy = s.InputsUseBuy;

                this.StartPosition = FormStartPosition.Manual;
                this.Location = s.Location;
                this.Size = s.Size;
                this.WindowState = s.WindowState;
            }
            catch { /* ignore */ }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettingsSafe();
            base.OnFormClosing(e);
        }
    }
}
