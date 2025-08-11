// MoonIndustryForm.cs — обновлённая версия с CraftPlanner
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

using Domain.Services;             // SdeAggregateDTO, BlueprintRecipeDTO, RecipeLine, Kind
using Domain.Models.ResourceDTO;   // ItemExtDTO
using Loader.Infrastructure;       // BinaryCachingService, Paths
using ANALYTICS;
using Domain.Infrastructure;
using Domain.Models;               // OrderHistoryMonthList + наше ядро крафта (Activity, Recipe, CraftPlanner, ...)
using static Domain.Models.InMemoryRecipeCatalog; // IRoundingPolicy, DefaultRoundingPolicy

namespace TestForm
{
    public partial class MoonIndustryForm : Form
    {
        // ---- Внешние зависимости ----
        private readonly SdeAggregateDTO _sde;
        private readonly IPriceProvider _priceProvider;

        // ---- История: Rating и AverageVolume ----
        private readonly HistoryStats _hist = new();

        // ---- Последние параметры для пересчёта (сохраняем) ----
        private int _lastRegionId = 10000002; // Jita
        private bool _lastUseOreIfLiquid;
        private double _lastMinOreRatingBn;
        private double _lastRefineYield;
        private double _lastSalesTaxPct;
        private double _lastReactMEpct, _lastReactTEpct;
        private double _lastBpoMEpct, _lastBpoTEpct;
        private int _lastModeIndex;

        // источник для грида (чтобы пересортировывать без пересчёта)
        private List<RowVM> _lastRows = new();

        // сортировка грида
        private string? _sortProp;
        private bool _sortAsc = false;

        // ---- константы activity ----
        private const int ACT_MANUFACTURING = 1;
        private const int ACT_REACTION = 11;

        // Индексы рецептов (старые): "что создаёт этот тип" — оставляю для вспом. фильтров/поиска
        private Dictionary<int, BlueprintRecipeDTO> _reactionByProduct = new();
        private Dictionary<int, BlueprintRecipeDTO> _mfgByProduct = new();

        // ---- Новое ядро крафта ----
        private InMemoryRecipeCatalog _catalog;
        private CraftPlanner _planner;
        private IRoundingPolicy _round;
        private CraftParamsSet _ps;

        public MoonIndustryForm(SdeAggregateDTO sde, IPriceProvider priceProvider)
        {
            InitializeComponent();

            _sde = sde;
            _priceProvider = priceProvider;

            // Индексы (оставляем для удобной фильтрации наборов)
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

            // Инициализируем универсальное ядро (каталог, планировщик, параметры)
            InitCraftKernel(); // загружает рецепты из _sde в InMemoryRecipeCatalog

            ApplyDarkTweaks();
            LoadSettings();

            // значения в контролы
            try
            {
                rbModeOre.Checked = (_lastModeIndex == 0);
                rbModeReactions.Checked = (_lastModeIndex == 1);
                rbModeBlueprints.Checked = (_lastModeIndex == 2);

                chkUseOre.Checked = _lastUseOreIfLiquid;
                numMinOreRatingBn.Value = (decimal)_lastMinOreRatingBn;
                numRefine.Value = (decimal)_lastRefineYield;
                numSalesTax.Value = (decimal)_lastSalesTaxPct;

                numReactME.Value = (decimal)_lastReactMEpct;
                numReactTE.Value = (decimal)_lastReactTEpct;

                numBpoME.Value = (decimal)_lastBpoMEpct;
                numBpoTE.Value = (decimal)_lastBpoTEpct;
            }
            catch { /* игнор, если что-то не влезло в диапазон */ }

            // события
            btnCalc.Click += async (_, __) => await RecalcAsync();
            grid.CellDoubleClick += Grid_CellDoubleClick;
            grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;

            // первичный расчёт
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

            // Загрузка рецептов из SDE → каталог
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
                    Id = new RecipeId
                    {
                        Kind = CraftKind.Reaction,
                        BlueprintTypeId = 0, // в агрегате может не быть ID формулы — нам не критично
                        Activity = Activity.Reaction
                    },
                    Output = new Product
                    {
                        Item = new ItemRef { TypeId = main.TypeID, Name = FindItem(main.TypeID)?.Name },
                        QtyPerRun = main.Quantity
                    },
                    Inputs = rec.Materials
                        .Select(m => new Ingredient
                        {
                            Item = new ItemRef { TypeId = m.TypeID, Name = FindItem(m.TypeID)?.Name },
                            QtyPerRun = m.Quantity
                        })
                        .ToList(),
                    Byproducts = rec.Products.Skip(1)
                        .Select(p => new Product
                        {
                            Item = new ItemRef { TypeId = p.TypeID, Name = FindItem(p.TypeID)?.Name },
                            QtyPerRun = p.Quantity
                        })
                        .ToList(),
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
                    Id = new RecipeId
                    {
                        Kind = CraftKind.Blueprint,
                        BlueprintTypeId = 0,
                        Activity = Activity.Manufacturing
                    },
                    Output = new Product
                    {
                        Item = new ItemRef { TypeId = main.TypeID, Name = FindItem(main.TypeID)?.Name },
                        QtyPerRun = main.Quantity
                    },
                    Inputs = rec.Materials
                        .Select(m => new Ingredient
                        {
                            Item = new ItemRef { TypeId = m.TypeID, Name = FindItem(m.TypeID)?.Name },
                            QtyPerRun = m.Quantity
                        })
                        .ToList(),
                    Byproducts = rec.Products.Skip(1)
                        .Select(p => new Product
                        {
                            Item = new ItemRef { TypeId = p.TypeID, Name = FindItem(p.TypeID)?.Name },
                            QtyPerRun = p.Quantity
                        })
                        .ToList(),
                    BaseTimePerRun = TimeSpan.FromSeconds(Math.Max(1, rec.BaseTimeSeconds))
                };
            }
        }

        private void ApplyDarkTweaks()
        {
            try { UiStyle.ApplyDark(this); } catch { /* если нет — не критично */ }

            // чтобы заголовки/лейблы не “сливались” на тёмной теме
            Color caption = Color.Gainsboro;
            foreach (var gb in panelTop.Controls.OfType<GroupBox>())
            {
                gb.ForeColor = caption;
                foreach (var c in gb.Controls)
                    if (c is Label or CheckBox or RadioButton)
                        ((Control)c).ForeColor = caption;
            }

            // заголовки грида
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.Gainsboro;
        }

        // =============== МОДЕЛИ ДАННЫХ UI ===============
        private sealed class RowVM
        {
            public int ProductTypeID { get; set; }
            public string Name { get; set; } = "";

            public bool IsOreMode { get; set; }
            public bool IsReactionMode { get; set; }
            public bool IsBlueprintMode { get; set; }

            // история
            public double Rating { get; set; }
            public double AvgVolume { get; set; }

            // товар
            public double UnitVolume { get; set; }          // м3/шт
            public double UnitsPerCycle { get; set; }       // шт/цикл
            public double CycleSecondsEff { get; set; }     // сек/цикл (с TE)

            // деньги
            public double InputsBuyIsk { get; set; }
            public double ReactionTaxIsk { get; set; }      // 1% от средней стоимости входов
            public double RevenueSellIsk { get; set; }
            public double SalesTaxIsk { get; set; }
            public double ProfitPerCycle { get; set; }
            public double ProfitPerUnit { get; set; }       // Профит/шт
            public double ProfitPerM3 { get; set; }         // Профит/м3
            public double ProfitPerHour { get; set; }       // Профит/час

            public List<PartLine> Inputs { get; set; } = new();
            public List<PartLine> Outputs { get; set; } = new();
        }

        private sealed class PartLine
        {
            public int TypeID { get; set; }
            public string Name { get; set; } = "";
            public double Quantity { get; set; }   // на цикл (или на 1 руду — в режиме руды)
            public double UnitPrice { get; set; }  // заполнится после загрузки цен
            public double Sum => UnitPrice * Quantity;
        }

        // =============== ИСТОРИЯ: Rating/AvgVolume (Jita) ===============
        private sealed class HistoryStats
        {
            private readonly Dictionary<int, (double rating, double avgVol)> _byType = new();

            public HistoryStats()
            {
                try
                {
                    var cache = new BinaryCachingService();
                    if (cache.TryLoad<OrderHistoryMonthList>(Paths.OrderHistoryMonthPath, out var data, out var res) && data != null)
                    {
                        foreach (var i in data.List)
                            _byType[i.TypeId] = (i.Rating, i.AverageVolume);
                    }
                }
                catch { /* без истории тоже можно жить */ }
            }

            public bool TryGet(int typeId, out double rating, out double avgVol)
            {
                if (_byType.TryGetValue(typeId, out var v))
                { rating = v.rating; avgVol = v.avgVol; return true; }
                rating = 0; avgVol = 0; return false;
            }

            public double? TryAvgPrice(int typeId)
            {
                if (_byType.TryGetValue(typeId, out var v) && v.avgVol > 0) return v.rating / v.avgVol;
                return null;
            }

            public double TryRatingBn(int typeId)
                => _byType.TryGetValue(typeId, out var v) ? v.rating / 1_000_000_000.0 : 0.0;
        }

        // =============== ПЕРЕСЧЁТ ===============
        private async System.Threading.Tasks.Task RecalcAsync()
        {
            btnCalc.Enabled = false;
            statusLabel.Text = "Сбор цен...";
            progress.Value = 0;

            // читаем параметры
            int regionId = _lastRegionId;

            bool useOreIfLiquid = chkUseOre.Checked;
            double minOreRatingBn = (double)numMinOreRatingBn.Value;
            double refineYield = (double)numRefine.Value / 100.0;

            double salesTax = (double)numSalesTax.Value / 100.0;

            double reactME = (double)numReactME.Value / 100.0;
            double reactTE = (double)numReactTE.Value / 100.0;

            double bpoME = (double)numBpoME.Value / 100.0;
            double bpoTE = (double)numBpoTE.Value / 100.0;

            int mode = rbModeOre.Checked ? 0 : rbModeReactions.Checked ? 1 : 2;

            // запомним
            _lastUseOreIfLiquid = useOreIfLiquid;
            _lastMinOreRatingBn = minOreRatingBn;
            _lastRefineYield = refineYield * 100.0;
            _lastSalesTaxPct = salesTax * 100.0;
            _lastReactMEpct = reactME * 100.0;
            _lastReactTEpct = reactTE * 100.0;
            _lastBpoMEpct = bpoME * 100.0;
            _lastBpoTEpct = bpoTE * 100.0;
            _lastModeIndex = mode;

            // применим ME/TE в набор параметров ядра
            _ps.Reaction.ME = (decimal)reactME;
            _ps.Reaction.TE = (decimal)reactTE;
            _ps.Reaction.FacilityMatMul = 1m;
            _ps.Reaction.FacilityTimeMul = 1m;

            _ps.Manufacturing.ME = (decimal)bpoME;
            _ps.Manufacturing.TE = (decimal)bpoTE;
            _ps.Manufacturing.FacilityMatMul = 1m;
            _ps.Manufacturing.FacilityTimeMul = 1m;

            try
            {
                var rows = new List<RowVM>(256);
                var needPrices = new HashSet<int>();

                if (mode == 0)
                    BuildModeOre(rows, needPrices, refineYield);
                else if (mode == 1)
                    BuildModeReactions(rows, needPrices);         // <— теперь через CraftPlanner (Mixed/Reaction)
                else
                    BuildModeBlueprints(rows, needPrices);        // <— гибрид: корень BPO, дети только Reaction

                foreach (var r in rows)
                {
                    foreach (var p in r.Inputs) needPrices.Add(p.TypeID);
                    foreach (var p in r.Outputs) needPrices.Add(p.TypeID);
                    needPrices.Add(r.ProductTypeID);
                }

                var prices = await _priceProvider.GetBestPricesAsync(needPrices, regionId, new System.Threading.CancellationToken());
                progress.Value = 50;
                statusLabel.Text = "Расчёт...";

                foreach (var r in rows)
                {
                    // входы BUY
                    double inputsBuy = 0;
                    foreach (var i in r.Inputs)
                    {
                        double unitBuy = TryBestBuy(prices, i.TypeID);
                        i.UnitPrice = unitBuy;
                        inputsBuy += i.Quantity * unitBuy;
                    }

                    // налог 1% от средней стоимости входов (для mode!=0)
                    double reactTax = 0;
                    if (!r.IsOreMode)
                    {
                        foreach (var i in r.Inputs)
                        {
                            var avg = _hist.TryAvgPrice(i.TypeID) ?? TryBestBuy(prices, i.TypeID);
                            reactTax += i.Quantity * avg;
                        }
                        reactTax *= 0.01; // 1%
                    }

                    // выручка SELL
                    double revenue = 0;
                    foreach (var o in r.Outputs)
                    {
                        double unitSell = TryBestSell(prices, o.TypeID);
                        o.UnitPrice = unitSell;
                        revenue += o.Quantity * unitSell;
                    }

                    // налог на продажу
                    double sellTax = revenue * salesTax;

                    r.InputsBuyIsk = inputsBuy;
                    r.ReactionTaxIsk = reactTax;
                    r.RevenueSellIsk = revenue;
                    r.SalesTaxIsk = sellTax;

                    r.ProfitPerCycle = revenue - sellTax - inputsBuy - reactTax;

                    double mainUnits = Math.Max(1, r.UnitsPerCycle);
                    r.ProfitPerUnit = r.ProfitPerCycle / mainUnits;
                    r.ProfitPerM3 = r.UnitVolume > 0 ? r.ProfitPerUnit / r.UnitVolume : 0;

                    r.ProfitPerHour = (r.CycleSecondsEff > 0)
                        ? r.ProfitPerCycle * (3600.0 / r.CycleSecondsEff)
                        : 0;
                }

                // фильтр руды по рейтингу (в млрд) — применяем в режиме руды
                if (useOreIfLiquid && _lastMinOreRatingBn > 0 && mode == 0)
                    rows = rows.Where(r => _hist.TryRatingBn(r.ProductTypeID) >= minOreRatingBn).ToList();

                // сортировка по умолчанию
                var sorted = rows
                    .OrderByDescending(r => r.ProfitPerHour)
                    .ThenByDescending(r => r.ProfitPerCycle)
                    .ToList();

                _lastRows = sorted;
                _sortProp = null; // сброс признака внешней сортировки
                Bind(sorted);

                statusLabel.Text = $"Готово: {sorted.Count} позиций";
                progress.Value = 100;

                SaveSettings();
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

        private void Bind(List<RowVM> rows)
        {
            grid.DataSource = new BindingList<RowVM>(rows);
            FormatGrid(grid);
        }

        // =============== РЕЖИМ 0: РУДА -> МАТЕРИАЛЫ (без изменений) ===============
        private void BuildModeOre(List<RowVM> rows, HashSet<int> needPrices, double refineYield)
        {
            var ores = _sde.Items.Values
                .Where(i => i.Published)
                .Where(i => i.Kind == Kind.MoonOre || i.Kind == Kind.Fullerene)
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
                {
                    row.Rating = rating;
                    row.AvgVolume = avg;
                }

                row.Inputs.Add(new PartLine { TypeID = ore.TypeID, Name = ore.Name, Quantity = 1 });

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

        // =============== РЕЖИМ 1: КОНЕЧНЫЕ РЕАКЦИИ (через CraftPlanner Mixed / Reaction) ===============
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
                    targetQty: (decimal)Math.Max(1, main.Quantity), // на 1 цикл
                    ps: _ps,
                    opt: opt);

                var flat = CraftAnalysis.FlattenLeaves(root); // базовые ресурсы
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
                {
                    row.Rating = rating;
                    row.AvgVolume = avg;
                }

                row.Inputs = flat
                    .Select(kv => new PartLine
                    {
                        TypeID = kv.Key,
                        Name = FindItem(kv.Key)?.Name ?? kv.Key.ToString(),
                        Quantity = (double)kv.Value
                    })
                    .OrderBy(p => p.Name)
                    .ToList();

                foreach (var i in row.Inputs) needPrices.Add(i.TypeID);

                foreach (var p in rec.Products)
                {
                    var it = FindItem(p.TypeID);
                    row.Outputs.Add(new PartLine { TypeID = p.TypeID, Name = it?.Name ?? p.TypeID.ToString(), Quantity = p.Quantity });
                    needPrices.Add(p.TypeID);
                }

                rows.Add(row);
            }
        }

        // =============== РЕЖИМ 2: BPO (корень Manufacturing, разворачиваем ТОЛЬКО реакционные входы) ===============
        private void BuildModeBlueprints(List<RowVM> rows, HashSet<int> needPrices)
        {
            var mfg = _sde.Recipes.Where(r => r.ActivityID == ACT_MANUFACTURING).ToList();
            var reactionProducts = new HashSet<int>(_sde.Recipes.Where(r => r.ActivityID == ACT_REACTION).SelectMany(r => r.Products).Select(p => p.TypeID));
            var onlyWithReactionInputs = mfg.Where(r => r.Materials.Any(m => reactionProducts.Contains(m.TypeID))).ToList();

            // дочернее разворачивание — только реакциями
            var childOpt = new BuildOptions
            {
                Mode = KindResolutionMode.Mixed,
                AllowedActivities = new List<Activity> { Activity.Reaction },
                AllowReplacements = false
            };

            foreach (var rec in onlyWithReactionInputs)
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
                    CycleSecondsEff = Math.Max(1, rec.BaseTimeSeconds) * (1.0 - (double)_ps.Manufacturing.TE) // время корневого BPO
                };

                if (_hist.TryGet(main.TypeID, out var rating, out var avg))
                {
                    row.Rating = rating;
                    row.AvgVolume = avg;
                }

                // Собираем базовый BOM: на 1 цикл корневого BPO применяем BPO-ME к материалам,
                // и если материал производим реакцией — разворачиваем его через планировщик (Reaction-only).
                var baseInputs = new List<PartLine>();
                foreach (var m in rec.Materials)
                {
                    var needPerRun = m.Quantity * (1.0 - (double)_ps.Manufacturing.ME); // ME корня (BPO)

                    if (_catalog.ResolveByProductOrNull(m.TypeID, Activity.Reaction) != null)
                    {
                        var subRoot = _planner.BuildMixed(
                            new ItemRef { TypeId = m.TypeID, Name = FindItem(m.TypeID)?.Name },
                            targetQty: (decimal)needPerRun,
                            ps: _ps,                               // Важно: используем текущие _ps (ME реакций берём из полей "Reactions" формы если хочешь — сейчас он равен BPO-ME только если ты так установишь)
                            opt: childOpt);

                        var flat = CraftAnalysis.FlattenLeaves(subRoot);
                        baseInputs.AddRange(flat.Select(kv => new PartLine
                        {
                            TypeID = kv.Key,
                            Name = FindItem(kv.Key)?.Name ?? kv.Key.ToString(),
                            Quantity = (double)kv.Value
                        }));
                    }
                    else
                    {
                        baseInputs.Add(new PartLine
                        {
                            TypeID = m.TypeID,
                            Name = FindItem(m.TypeID)?.Name ?? m.TypeID.ToString(),
                            Quantity = needPerRun
                        });
                    }
                }

                row.Inputs = Aggregate(baseInputs);
                foreach (var i in row.Inputs) needPrices.Add(i.TypeID);

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

        // объединить одинаковые позиции (после развертки)
        private List<PartLine> Aggregate(IEnumerable<PartLine> src)
            => src.GroupBy(x => x.TypeID)
                  .Select(g => new PartLine
                  {
                      TypeID = g.Key,
                      Name = g.First().Name,
                      Quantity = g.Sum(z => z.Quantity)
                  })
                  .OrderBy(x => x.Name)
                  .ToList();

        private ItemExtDTO? FindItem(int typeId)
            => _sde.Items.TryGetValue(typeId, out var it) ? it : null;

        private static double TryBestBuy(Dictionary<int, (double bestBuy, double bestSell)> prices, int typeId)
            => prices.TryGetValue(typeId, out var p) ? p.bestBuy : 0;

        private static double TryBestSell(Dictionary<int, (double bestBuy, double bestSell)> prices, int typeId)
            => prices.TryGetValue(typeId, out var p) ? p.bestSell : 0;

        // =============== UI: двойной клик -> форма деталей ===============
        private void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.RowIndex >= _lastRows.Count) return;
            var r = _lastRows[e.RowIndex];

            // Для деталей строим "чистый" BOM на 1 цикл в Strict:
            var inputsStrict = new List<(int TypeID, string Name, double Qty)>();
            if (r.IsReactionMode)
            {
                var perRun = CraftFacade.GetResourcesPerRunStrict(
                    _planner,
                    new ItemRef { TypeId = r.ProductTypeID, Name = r.Name },
                    Activity.Reaction,
                    _ps,
                    _round);

                inputsStrict = perRun.Select(kv => (kv.Key, FindItem(kv.Key)?.Name ?? kv.Key.ToString(), (double)kv.Value)).ToList();
            }
            else if (r.IsBlueprintMode)
            {
                var perRun = CraftFacade.GetResourcesPerRunStrict(
                    _planner,
                    new ItemRef { TypeId = r.ProductTypeID, Name = r.Name },
                    Activity.Manufacturing,
                    _ps,
                    _round);

                inputsStrict = perRun.Select(kv => (kv.Key, FindItem(kv.Key)?.Name ?? kv.Key.ToString(), (double)kv.Value)).ToList();
            }
            else
            {
                // режим руды — показываем как есть (вход=руда)
                inputsStrict = r.Inputs.Select(i => (i.TypeID, i.Name, i.Quantity)).ToList();
            }

            var payload = new
            {
                ProductTypeID = r.ProductTypeID,
                Name = r.Name,
                UnitsPerCycle = r.UnitsPerCycle,
                CycleSecondsEff = r.CycleSecondsEff,
                // входы/выходы (цены не подгружаем заново в деталях, там формат N0/N3)
                Inputs = inputsStrict.Select(x => new { TypeID = x.TypeID, Name = x.Name, Quantity = x.Qty, UnitPrice = 0.0, Sum = 0.0 }).ToList(),
                Outputs = r.Outputs.Select(o => new { o.TypeID, o.Name, Quantity = o.Quantity, o.UnitPrice, o.Sum }).ToList(),
                ChainNotes = Array.Empty<string>()
            };

            try
            {
                using var dlg = new MoonItemDetailsForm(payload);
                dlg.StartPosition = FormStartPosition.CenterParent;
                dlg.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка открытия деталей", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // =============== UI: сортировка по клику на заголовок ===============
        private void Grid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || _lastRows.Count == 0) return;

            var col = grid.Columns[e.ColumnIndex];
            var prop = col.DataPropertyName;
            if (string.IsNullOrEmpty(prop)) return;

            if (_sortProp == prop) _sortAsc = !_sortAsc;
            else { _sortProp = prop; _sortAsc = true; }

            IEnumerable<RowVM> query = _lastRows;

            var pinfo = typeof(RowVM).GetProperty(prop);
            if (pinfo != null)
            {
                query = _sortAsc
                    ? _lastRows.OrderBy(r => pinfo.GetValue(r, null))
                    : _lastRows.OrderByDescending(r => pinfo.GetValue(r, null));
            }

            var sorted = query.ToList();
            _lastRows = sorted;
            Bind(sorted);

            foreach (DataGridViewColumn c in grid.Columns)
                c.HeaderCell.SortGlyphDirection = SortOrder.None;
            col.HeaderCell.SortGlyphDirection = _sortAsc ? SortOrder.Ascending : SortOrder.Descending;
        }

        private static void FormatGrid(DataGridView g)
        {
            g.AutoGenerateColumns = false;

            if (g.Columns.Count == 0)
            {
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProductTypeID", HeaderText = "TypeID", Width = 80, SortMode = DataGridViewColumnSortMode.Programmatic });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Название", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill, SortMode = DataGridViewColumnSortMode.Programmatic });

                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Rating", HeaderText = "Rating", Width = 100, SortMode = DataGridViewColumnSortMode.Programmatic });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "AvgVolume", HeaderText = "AvgVol", Width = 100, SortMode = DataGridViewColumnSortMode.Programmatic });

                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UnitsPerCycle", HeaderText = "шт/цикл", Width = 80, SortMode = DataGridViewColumnSortMode.Programmatic });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UnitVolume", HeaderText = "м³/шт", Width = 70, SortMode = DataGridViewColumnSortMode.Programmatic });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CycleSecondsEff", HeaderText = "сек/цикл", Width = 90, SortMode = DataGridViewColumnSortMode.Programmatic });

                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "InputsBuyIsk", HeaderText = "Входы (BUY)", Width = 120, SortMode = DataGridViewColumnSortMode.Programmatic });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ReactionTaxIsk", HeaderText = "Налог реакц.", Width = 120, SortMode = DataGridViewColumnSortMode.Programmatic });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RevenueSellIsk", HeaderText = "Выручка (SELL)", Width = 130, SortMode = DataGridViewColumnSortMode.Programmatic });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "SalesTaxIsk", HeaderText = "Налог продажи", Width = 120, SortMode = DataGridViewColumnSortMode.Programmatic });

                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProfitPerCycle", HeaderText = "Профит/цикл", Width = 110, SortMode = DataGridViewColumnSortMode.Programmatic });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProfitPerUnit", HeaderText = "Профит/шт", Width = 110, SortMode = DataGridViewColumnSortMode.Programmatic });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProfitPerM3", HeaderText = "Профит/м³", Width = 110, SortMode = DataGridViewColumnSortMode.Programmatic });
                g.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "ProfitPerHour", HeaderText = "Профит/час", Width = 110, SortMode = DataGridViewColumnSortMode.Programmatic });
            }

            string money = "N0";
            string qty = "N3";

            void fmt(string col, string f = "N0")
            {
                if (!g.Columns.Contains(col)) return;
                g.Columns[col].DefaultCellStyle.Format = f;
                g.Columns[col].DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
            }

            fmt("Rating", "N0");
            fmt("AvgVolume", "N0");
            fmt("UnitsPerCycle", qty);
            fmt("UnitVolume", "N3");
            fmt("CycleSecondsEff", "N0");
            fmt("InputsBuyIsk", money);
            fmt("ReactionTaxIsk", money);
            fmt("RevenueSellIsk", money);
            fmt("SalesTaxIsk", money);
            fmt("ProfitPerCycle", money);
            fmt("ProfitPerUnit", money);
            fmt("ProfitPerM3", money);
            fmt("ProfitPerHour", money);
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            SaveSettings();
        }

        private void SaveSettings()
        {
            try
            {
                var st = new SavedState
                {
                    ModeIndex = rbModeOre.Checked ? 0 : rbModeReactions.Checked ? 1 : 2,
                    UseOre = chkUseOre.Checked,
                    MinOreRatingBn = (double)numMinOreRatingBn.Value,
                    RefineYieldPct = (double)numRefine.Value,
                    SalesTaxPct = (double)numSalesTax.Value,
                    ReactMEpct = (double)numReactME.Value,
                    ReactTEpct = (double)numReactTE.Value,
                    BpoMEpct = (double)numBpoME.Value,
                    BpoTEpct = (double)numBpoTE.Value
                };

                var cache = new BinaryCachingService();
                cache.TrySave("moon_industry_form_state.bin", st, out var _);
            }
            catch { }
        }

        private void LoadSettings()
        {
            try
            {
                var cache = new BinaryCachingService();
                if (cache.TryLoad<SavedState>("moon_industry_form_state.bin", out var st, out var _)
                    && st != null)
                {
                    _lastModeIndex = st.ModeIndex;
                    _lastUseOreIfLiquid = st.UseOre;
                    _lastMinOreRatingBn = st.MinOreRatingBn;
                    _lastRefineYield = st.RefineYieldPct;
                    _lastSalesTaxPct = st.SalesTaxPct;
                    _lastReactMEpct = st.ReactMEpct;
                    _lastReactTEpct = st.ReactTEpct;
                    _lastBpoMEpct = st.BpoMEpct;
                    _lastBpoTEpct = st.BpoTEpct;
                }
            }
            catch { }
        }

        private sealed class SavedState : ResourceCaching
        {
            public int ModeIndex { get; set; }
            public bool UseOre { get; set; }
            public double MinOreRatingBn { get; set; }
            public double RefineYieldPct { get; set; }
            public double SalesTaxPct { get; set; }
            public double ReactMEpct { get; set; }
            public double ReactTEpct { get; set; }
            public double BpoMEpct { get; set; }
            public double BpoTEpct { get; set; }
        }
    }
}
