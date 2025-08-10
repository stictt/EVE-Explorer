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
using Domain.Models;               // OrderHistoryMonthList

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

        // Индексы рецептов: "что создаёт этот тип"
        private Dictionary<int, BlueprintRecipeDTO> _reactionByProduct = new();
        private Dictionary<int, BlueprintRecipeDTO> _mfgByProduct = new();

        public MoonIndustryForm(SdeAggregateDTO sde, IPriceProvider priceProvider)
        {
            InitializeComponent();

            _sde = sde;
            _priceProvider = priceProvider;

            // построим индексы рецептов: продукт -> рецепт
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

            ApplyDarkTweaks();

            // загрузка настроек формы
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

            try
            {
                var rows = new List<RowVM>(256);
                var needPrices = new HashSet<int>();

                if (mode == 0)
                    BuildModeOre(rows, needPrices, refineYield);
                else if (mode == 1)
                    BuildModeReactions(rows, needPrices, reactME, reactTE);
                else
                    BuildModeBlueprints(rows, needPrices, bpoME, bpoTE);

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
                    // входы BUY (уже развернутые до базовых), ME применяется при разворачивании
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

        // =============== РЕЖИМ 0: РУДА -> МАТЕРИАЛЫ ===============
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

        // =============== РЕЖИМ 1: КОНЕЧНЫЕ РЕАКЦИИ ===============
        private void BuildModeReactions(List<RowVM> rows, HashSet<int> needPrices, double reactME, double reactTE)
        {
            var reactionRecipes = _sde.Recipes.Where(r => r.ActivityID == ACT_REACTION).ToList();

            var allReactionInputs = new HashSet<int>(
                reactionRecipes.SelectMany(r => r.Materials).Select(m => m.TypeID));

            var terminal = reactionRecipes
                .Where(r => r.Products.All(p => !allReactionInputs.Contains(p.TypeID)))
                .ToList();

            foreach (var rec in terminal)
            {
                var main = rec.Products.FirstOrDefault();
                if (main == null) continue;

                var item = FindItem(main.TypeID);
                var row = new RowVM
                {
                    ProductTypeID = main.TypeID,
                    Name = item?.Name ?? main.TypeID.ToString(),
                    IsReactionMode = true,
                    UnitsPerCycle = main.Quantity,
                    UnitVolume = item?.Volume ?? 0,
                    CycleSecondsEff = Math.Max(1, rec.BaseTimeSeconds) * (1.0 - reactTE)
                };

                if (_hist.TryGet(main.TypeID, out var rating, out var avg))
                {
                    row.Rating = rating;
                    row.AvgVolume = avg;
                }

                // Разворачиваем входы «до базы»
                var baseInputs = new List<PartLine>();
                foreach (var m in rec.Materials)
                    DecomposeToBaseInputs(m.TypeID, m.Quantity, reactME, baseInputs, new HashSet<int>());

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

        // =============== РЕЖИМ 2: BPO (где входы — продукты реакций) ===============
        private void BuildModeBlueprints(List<RowVM> rows, HashSet<int> needPrices, double bpoME, double bpoTE)
        {
            var mfg = _sde.Recipes.Where(r => r.ActivityID == ACT_MANUFACTURING).ToList();

            var reactionProducts = new HashSet<int>(
                _sde.Recipes.Where(r => r.ActivityID == ACT_REACTION)
                            .SelectMany(r => r.Products)
                            .Select(p => p.TypeID));

            var onlyWithReactionInputs = mfg
                .Where(r => r.Materials.Any(m => reactionProducts.Contains(m.TypeID)))
                .ToList();

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
                    CycleSecondsEff = Math.Max(1, rec.BaseTimeSeconds) * (1.0 - bpoTE)
                };

                if (_hist.TryGet(main.TypeID, out var rating, out var avg))
                {
                    row.Rating = rating;
                    row.AvgVolume = avg;
                }

                var baseInputs = new List<PartLine>();
                foreach (var m in rec.Materials)
                    DecomposeToBaseInputs(m.TypeID, m.Quantity, bpoME, baseInputs, new HashSet<int>());

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

        // ---- Развёртка входа в базовые позиции ----
        // Если вход — продукт реакции, раскрываем его материалы рекурсивно (учитывая ME).
        // Иначе — это «база»: либо лунные материалы/минералы/пр., либо (если включено) руды,
        // подобранные под нужный базовый материал через рефайн + фильтр по ликвидности.
        private void DecomposeToBaseInputs(int typeId, double qty, double mePercent,
                                           List<PartLine> sink, HashSet<int> guard)
        {
            // защита от циклов
            if (!guard.Add(typeId)) return;

            // если это продукт реакции — раскрываем
            if (_reactionByProduct.TryGetValue(typeId, out var rx))
            {
                // во сколько раз нужно масштабировать материалы рецепта
                var mainOut = rx.Products.FirstOrDefault() ?? new RecipeLine { Quantity = 1 };
                double k = qty / Math.Max(1, (double)mainOut.Quantity);

                foreach (var m in rx.Materials)
                {
                    // применяем ME текущего узла
                    double need = k * m.Quantity * (1.0 - mePercent);
                    DecomposeToBaseInputs(m.TypeID, need, mePercent, sink, guard);
                }
                return;
            }

            // иначе это не продукт реакции — базовый ресурс
            // если включена покупка руды, пытаемся заменить материал на руду
            if (chkUseOre.Checked)
            {
                var replaced = TryReplaceMaterialWithOre(typeId, qty, mePercent);
                if (replaced != null) { sink.Add(replaced); return; }
            }

            var it = FindItem(typeId);
            sink.Add(new PartLine { TypeID = typeId, Name = it?.Name ?? typeId.ToString(), Quantity = qty });
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

        // попытка заменить лунный материал на руду (с учётом ликвидности)
        private PartLine? TryReplaceMaterialWithOre(int matTypeId, double matQty, double mePercent)
        {
            // найдём все руды, которые при рефайне дают нужный материал
            var ores = _sde.Reprocessing
                .Where(kv => kv.Value.Outputs.Any(o => o.TypeID == matTypeId))
                .Select(kv => kv.Key)
                .Select(FindItem)
                .Where(it => it != null)
                .Cast<ItemExtDTO>()
                .ToList();

            if (ores.Count == 0) return null;

            double minBn = (double)numMinOreRatingBn.Value;
            if (minBn > 0)
                ores = ores.Where(o => _hist.TryRatingBn(o.TypeID) >= minBn).ToList();

            if (ores.Count == 0) return null;

            ItemExtDTO? pick = null;
            double bestUnits = double.MaxValue;

            foreach (var ore in ores)
            {
                if (!_sde.Reprocessing.TryGetValue(ore.TypeID, out var plan)) continue;
                var portion = Math.Max(1, plan.PortionSize);

                var outLine = plan.Outputs.FirstOrDefault(o => o.TypeID == matTypeId);
                if (outLine == null) continue;

                var yield = (double)numRefine.Value / 100.0;
                var perOre = (outLine.Quantity / (double)portion) * yield;
                if (perOre <= 0) continue;

                // ME влияет на потребность материала, следовательно и на руду
                var needOre = (matQty * (1.0 - mePercent)) / perOre;
                if (needOre < bestUnits)
                {
                    bestUnits = needOre;
                    pick = ore;
                }
            }

            if (pick == null) return null;
            return new PartLine { TypeID = pick.TypeID, Name = pick.Name, Quantity = bestUnits };
        }

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

            var payload = new
            {
                ProductTypeID = r.ProductTypeID,
                Name = r.Name,
                UnitsPerCycle = r.UnitsPerCycle,
                CycleSecondsEff = r.CycleSecondsEff,
                Inputs = r.Inputs.Select(i => new { i.TypeID, i.Name, Quantity = i.Quantity, i.UnitPrice, i.Sum }).ToList(),
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
                    return;
                }
            }
            catch { }

            // дефолты
            _lastModeIndex = 1;
            _lastUseOreIfLiquid = true;
            _lastMinOreRatingBn = 0;
            _lastRefineYield = 55.0;
            _lastSalesTaxPct = 1.5;
            _lastReactMEpct = 0;
            _lastReactTEpct = 0;
            _lastBpoMEpct = 0;
            _lastBpoTEpct = 0;
        }

        [Serializable]
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
