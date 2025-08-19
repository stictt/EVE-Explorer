using ANALYTICS;                      // OrderHistoryMonthList
using Domain.Infrastructure;
using Loader.Infrastructure;          // BinaryCachingService, Paths
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TestForm;                       // IPriceProvider

namespace TestForm.Infrastructure
{
    public sealed class PlanetaryAnalysisForm : Form
    {
        private readonly IProductionPlanner _planner;
        private readonly ISdePiRepository? _repo;
        private readonly IPriceProvider? _prices;

        // рейтинг из OrderHistoryMonth
        private readonly HistoryStats _hist = new();

        // UI
        private TextBox _txtTypeId = null!;
        private NumericUpDown _numPlanets = null!;
        private NumericUpDown _numCc = null!;
        private ComboBox _cmbMode = null!;
        private NumericUpDown _numStartTier = null!;
        private NumericUpDown _numPenalty = null!;
        private CheckBox _chkUseExtractionTpl = null!;
        private TextBox _txtExtractionTpl = null!;
        private CheckBox _chkCutoffOverride = null!;
        private NumericUpDown _numCutoff = null!;

        private CheckBox _chkUseFactoryTpls = null!;
        private TextBox _txtTplT2 = null!;
        private TextBox _txtTplT3 = null!;
        private TextBox _txtTplT4 = null!;

        private NumericUpDown _numRegion = null!;
        private ComboBox _cmbRevenueSide = null!;
        private ComboBox _cmbCostSide = null!;

        private Button _btnRun = null!;
        private Button _btnDetails = null!;
        private Button _btnCopyBom = null!;
        private Button _btnGetPrices = null!;
        private Button _btnExportBom = null!;
        private Button _btnExportPlan = null!;

        private Label _lblStatus = null!;
        private DataGridView _grid = null!;
        private readonly BindingSource _bs = new BindingSource();

        private CancellationTokenSource? _cts;

        // Данные
        private readonly Dictionary<int, ProductionPlanResult> _plans = new();
        private readonly List<PlanRow> _rows = new();

        // Сортировка
        private string? _sortProperty;
        private bool _sortAsc = true;

        private enum PriceSide { Sell, Buy }

        // --- рейтинг и средний объём из кеша ---
        private sealed class HistoryStats
        {
            private readonly Dictionary<int, (double rating, double avg)> _byType = new();
            public HistoryStats()
            {
                try
                {
                    var cache = new BinaryCachingService();
                    if (cache.TryLoad<OrderHistoryMonthList>(Paths.OrderHistoryMonthPath, out var data, out var _)
                        && data != null)
                    {
                        foreach (var i in data.List)
                            _byType[i.TypeId] = (i.Rating, i.AverageVolume);
                    }
                }
                catch { /* тихо игнорируем: рейтинга может не быть */ }
            }
            public double TryRatingBn(int typeId) => _byType.TryGetValue(typeId, out var t) ? t.rating : 0.0;
            public double TryAvgVol(int typeId) => _byType.TryGetValue(typeId, out var t) ? t.avg : 0.0;
        }

        private sealed class PlanRow
        {
            private readonly ISdePiRepository? _repo;
            public PlanRow(ProductionPlanResult plan, ISdePiRepository? repo)
            {
                _repo = repo;
                ProductTypeId = plan.ProductTypeId;
                ProductName = repo?.GetType(plan.ProductTypeId)?.TypeName ?? plan.ProductTypeId.ToString();
                OutputPerDay = plan.OutputPerDay;
                PlanetsUsed = plan.PlanetsUsed;
                Note = plan.Note ?? "";

                // Уровень PI (из дерева рецепта)
                Tier = CalcTier(plan.Recipe);

                // Объём из SDE: колонка volume в invTypes — строка, парсим InvariantCulture.
                var volStr = _repo?.GetType(plan.ProductTypeId)?.Volume;
                if (!decimal.TryParse(volStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var volDec))
                    volDec = 0m;
                ProductVolumeM3 = volDec;
                VolumePerMonthM3 = OutputPerDay * 30m * ProductVolumeM3;
            }

            private static int CalcTier(RecipeNode? n)
            {
                if (n == null) return 0;
                if (n.Inputs == null || n.Inputs.Count == 0) return 0;
                int maxChild = 0;
                foreach (var i in n.Inputs)
                    maxChild = Math.Max(maxChild, CalcTier(i.Node));
                return maxChild + 1;
            }

            // Идентификация/визуализация
            public int ProductTypeId { get; }
            public string ProductName { get; }
            public int Tier { get; }                   // Уровень PI

            // Производство
            public decimal OutputPerDay { get; set; }
            public int PlanetsUsed { get; set; }
            public string Note { get; set; }

            // Цены/итоги (день)
            public decimal RevenuePerDay { get; set; }
            public decimal CostsPerDay { get; set; }
            public decimal FinalValuePerDay { get; set; } // не выводим колонкой, оставим для внутреннего кода
            public decimal ProfitBuyPerDay { get; set; }

            // Месяц
            public decimal ProfitBuyPerMonth { get; set; }
            public decimal ProductVolumeM3 { get; }       // м³ за 1 ед.
            public decimal VolumePerMonthM3 { get; set; } // Output/day * 30 * volume

            // Рейтинг (OrderHistoryMonth)
            public double RatingBn { get; set; }

            public void ApplyPnl(
                ProductionPlanResult plan,
                Dictionary<int, (double bestBuy, double bestSell)> px,
                PriceSide revenueSide,
                PriceSide costSide)
            {
                var finalPerHour = (plan.Recipe != null && plan.Recipe.CycleTimeSeconds > 0)
                    ? (3600m / plan.Recipe.CycleTimeSeconds) * plan.Recipe.OutputQty
                    : 0m;
                var outPerHour = plan.OutputPerDay / 24m;
                var scale = (finalPerHour > 0m) ? (outPerHour / finalPerHour) : 0m;

                decimal GetPrice(int typeId, PriceSide side)
                {
                    if (!px.TryGetValue(typeId, out var t)) return 0m;
                    return side == PriceSide.Sell ? (decimal)t.bestSell : (decimal)t.bestBuy;
                }

                // Финальная стоимость (продажа собственного продукта)
                var pProd = GetPrice(plan.ProductTypeId, revenueSide);
                RevenuePerDay = pProd * plan.OutputPerDay;
                FinalValuePerDay = RevenuePerDay;

                // Себестоимость при закупке входов
                decimal costs = 0m;
                foreach (var m in plan.RequiredInputs)
                {
                    var p = GetPrice(m.TypeId, costSide);
                    costs += m.QuantityPerHour * scale * 24m * p;
                }
                CostsPerDay = costs;

                // Прибыль «если покупаем входы»
                ProfitBuyPerDay = RevenuePerDay - CostsPerDay;
                ProfitBuyPerMonth = ProfitBuyPerDay * 30m;

                // Объём/месяц пересчитывать не требуется (OutputPerDay не меняем)
                VolumePerMonthM3 = OutputPerDay * 30m * ProductVolumeM3;
            }
        }

        public PlanetaryAnalysisForm(IProductionPlanner planner, ISdePiRepository? repo = null, IPriceProvider? prices = null)
        {
            _planner = planner;
            _repo = repo;
            _prices = prices;
            BuildUi();
        }

        private Label MkLabel(string text) => new Label { Text = text, AutoSize = true, Padding = new Padding(8, 8, 4, 0) };

        private void BuildUi()
        {
            Text = "Планетарная промышленность — Аналитика";
            Width = 1400;
            Height = 880;
            StartPosition = FormStartPosition.CenterParent;

            // === Grid ===
            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                ColumnHeadersVisible = true,
                RowHeadersVisible = false,
                BorderStyle = BorderStyle.FixedSingle,
                EnableHeadersVisualStyles = false,
                BackgroundColor = System.Drawing.SystemColors.Window
            };
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;

            // Колонки (Programmatic — сортируем сами)
            // УДАЛЕНО: ProductTypeId
            AddCol("Продукт", "ProductName");
            AddCol("Уровень", "Tier");
            AddCol("Выход / день", "OutputPerDay", "N2");
            AddCol("Планет", "PlanetsUsed");
            // УДАЛЕНО: Final / Day
            AddCol("Выручка / день", "RevenuePerDay", "N2");
            AddCol("Затраты / день", "CostsPerDay", "N2");
            AddCol("Профит (закупка) / день", "ProfitBuyPerDay", "N2");
            AddCol("Профит (закупка) / месяц", "ProfitBuyPerMonth", "N2");
            AddCol("Объём / месяц (м³)", "VolumePerMonthM3", "N2");
            AddCol("Rating", "RatingBn", "N3");
            AddFillCol("Примечание", "Note");

            _grid.DataSource = _bs;
            _grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;

            Controls.Add(_grid);
            _grid.SendToBack();

            // === Панель кнопок ===
            var runPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 44,
                Padding = new Padding(6, 0, 6, 0)
            };
            Controls.Add(runPanel);

            _btnRun = new Button { Text = "План", Width = 90, Height = 28, Margin = new Padding(10, 3, 0, 0) };
            runPanel.Controls.Add(_btnRun);

            _btnDetails = new Button { Text = "Детали…", Width = 90, Height = 28, Enabled = false, Margin = new Padding(6, 3, 0, 0) };
            runPanel.Controls.Add(_btnDetails);

            _btnCopyBom = new Button { Text = "Копировать BOM", Width = 130, Height = 28, Enabled = false, Margin = new Padding(6, 3, 0, 0) };
            runPanel.Controls.Add(_btnCopyBom);

            _btnGetPrices = new Button { Text = "Цены", Width = 90, Height = 28, Enabled = _prices != null, Margin = new Padding(6, 3, 0, 0) };
            runPanel.Controls.Add(_btnGetPrices);

            _btnExportBom = new Button { Text = "Экспорт BOM CSV", Width = 150, Height = 28, Enabled = false, Margin = new Padding(6, 3, 0, 0) };
            runPanel.Controls.Add(_btnExportBom);

            _btnExportPlan = new Button { Text = "Экспорт плана CSV", Width = 150, Height = 28, Enabled = false, Margin = new Padding(6, 3, 0, 0) };
            runPanel.Controls.Add(_btnExportPlan);

            _lblStatus = new Label { AutoSize = true, Padding = new Padding(12, 8, 0, 0) };
            runPanel.Controls.Add(_lblStatus);

            // === Панель параметров ===
            var panelTop = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                Height = 150,
                Padding = new Padding(6, 6, 6, 6),
                AutoScroll = true,
                WrapContents = true
            };
            Controls.Add(panelTop);

            panelTop.Controls.Add(MkLabel("Product TypeID (опционально, пусто = все):"));
            _txtTypeId = new TextBox { Width = 120, Text = "" };
            panelTop.Controls.Add(_txtTypeId);

            panelTop.Controls.Add(MkLabel("Планет:"));
            _numPlanets = new NumericUpDown { Minimum = 1, Maximum = 50, Value = 6, Width = 70 };
            panelTop.Controls.Add(_numPlanets);

            panelTop.Controls.Add(MkLabel("CC уровень:"));
            _numCc = new NumericUpDown { Minimum = 1, Maximum = 5, Value = 5, Width = 70 };
            panelTop.Controls.Add(_numCc);

            panelTop.Controls.Add(MkLabel("Режим:"));
            _cmbMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
            _cmbMode.Items.AddRange(new object[] { PiMode.Full, PiMode.TradeP0, PiMode.TradeP1 });
            _cmbMode.SelectedIndex = 0;
            panelTop.Controls.Add(_cmbMode);

            panelTop.Controls.Add(MkLabel("Стартовый Tier:"));
            _numStartTier = new NumericUpDown { Minimum = 0, Maximum = 4, Value = 2, Width = 70 };
            panelTop.Controls.Add(_numStartTier);

            panelTop.Controls.Add(MkLabel("Штраф CC %:"));
            _numPenalty = new NumericUpDown { Minimum = 0, Maximum = 50, DecimalPlaces = 1, Increment = 0.5M, Value = 3, Width = 70 };
            panelTop.Controls.Add(_numPenalty);

            _chkUseExtractionTpl = new CheckBox { Text = "Шаблон добычи", Checked = false, AutoSize = true, Padding = new Padding(12, 6, 0, 0) };
            panelTop.Controls.Add(_chkUseExtractionTpl);
            panelTop.Controls.Add(MkLabel("Extraction tpl:"));
            _txtExtractionTpl = new TextBox { Width = 160, Text = "Extraction_Default" };
            panelTop.Controls.Add(_txtExtractionTpl);

            _chkCutoffOverride = new CheckBox { Text = "Порог покупок (tier)", Checked = false, AutoSize = true, Padding = new Padding(12, 6, 0, 0) };
            panelTop.Controls.Add(_chkCutoffOverride);
            _numCutoff = new NumericUpDown { Minimum = 0, Maximum = 4, Value = 1, Width = 60, Enabled = false };
            panelTop.Controls.Add(_numCutoff);
            _chkCutoffOverride.CheckedChanged += (_, __) => _numCutoff.Enabled = _chkCutoffOverride.Checked;

            _chkUseFactoryTpls = new CheckBox { Text = "Шаблоны фабрик (T2/T3/T4)", Checked = false, AutoSize = true, Padding = new Padding(16, 6, 0, 0) };
            panelTop.Controls.Add(_chkUseFactoryTpls);
            panelTop.Controls.Add(MkLabel("T2 tpl:"));
            _txtTplT2 = new TextBox { Width = 160, Text = "Factory_Advanced_Default" };
            panelTop.Controls.Add(_txtTplT2);
            panelTop.Controls.Add(MkLabel("T3 tpl:"));
            _txtTplT3 = new TextBox { Width = 160, Text = "Factory_Advanced_Default" };
            panelTop.Controls.Add(_txtTplT3);
            panelTop.Controls.Add(MkLabel("T4 tpl:"));
            _txtTplT4 = new TextBox { Width = 160, Text = "Factory_HighTech_Default" };
            panelTop.Controls.Add(_txtTplT4);

            panelTop.Controls.Add(MkLabel("RegionId:"));
            _numRegion = new NumericUpDown { Minimum = 10000001, Maximum = 11000000, Value = 10000002, Width = 90 };
            panelTop.Controls.Add(_numRegion);

            panelTop.Controls.Add(MkLabel("Цена выручки:"));
            _cmbRevenueSide = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            _cmbRevenueSide.Items.AddRange(new object[] { "Sell", "Buy" });
            _cmbRevenueSide.SelectedIndex = 0;
            panelTop.Controls.Add(_cmbRevenueSide);

            panelTop.Controls.Add(MkLabel("Цена затрат:"));
            _cmbCostSide = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 80 };
            _cmbCostSide.Items.AddRange(new object[] { "Buy", "Sell" });
            _cmbCostSide.SelectedIndex = 0;
            panelTop.Controls.Add(_cmbCostSide);

            // events
            _btnRun.Click += async (_, __) => await RunPlanningAsync();
            _btnDetails.Click += (_, __) => OpenSelectedDetails();
            _btnCopyBom.Click += (_, __) => CopySelectedBomToClipboard();
            _btnGetPrices.Click += async (_, __) => await GetPricesForAllAsync();
            _btnExportBom.Click += (_, __) => ExportSelectedBom();
            _btnExportPlan.Click += (_, __) => ExportSelectedPlan();
            _grid.CellDoubleClick += (_, __) => OpenSelectedDetails();
            _grid.SelectionChanged += (_, __) => UpdateButtonsState();
            FormClosing += (_, __) => _cts?.Cancel();
        }

        private void AddCol(string header, string prop, string? format = null)
        {
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                DataPropertyName = prop,
                SortMode = DataGridViewColumnSortMode.Programmatic
            };
            if (!string.IsNullOrEmpty(format))
                col.DefaultCellStyle.Format = format;
            _grid.Columns.Add(col);
        }
        private void AddFillCol(string header, string prop)
        {
            var col = new DataGridViewTextBoxColumn
            {
                HeaderText = header,
                DataPropertyName = prop,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.Programmatic
            };
            _grid.Columns.Add(col);
        }

        private PlannerSettings ReadSettings()
        {
            // собираем значения заранее (избегаем присваиваний в init-only)
            var startTier = (int)_numStartTier.Value;
            var ccPenalty = (double)_numPenalty.Value / 100.0;
            var useExtraction = _chkUseExtractionTpl.Checked;
            var extractionTpl = string.IsNullOrWhiteSpace(_txtExtractionTpl.Text) ? null : _txtExtractionTpl.Text.Trim();

            var useFactoryTpls = _chkUseFactoryTpls.Checked;
            Dictionary<int, string>? tierTpls = null;
            if (useFactoryTpls)
            {
                tierTpls = new Dictionary<int, string>();
                if (!string.IsNullOrWhiteSpace(_txtTplT2.Text)) tierTpls[2] = _txtTplT2.Text.Trim();
                if (!string.IsNullOrWhiteSpace(_txtTplT3.Text)) tierTpls[3] = _txtTplT3.Text.Trim();
                if (!string.IsNullOrWhiteSpace(_txtTplT4.Text)) tierTpls[4] = _txtTplT4.Text.Trim();
                if (tierTpls.Count == 0) tierTpls = null;
            }

            int? purchaseCutoff = _chkCutoffOverride.Checked ? (int?)_numCutoff.Value : null;

            return new PlannerSettings
            {
                StartProductionTier = startTier,
                CcPenalty = ccPenalty,
                UseExtractionTemplate = useExtraction,
                ExtractionTemplateName = extractionTpl,
                UseFactoryTemplates = useFactoryTpls,
                TierFactoryTemplates = tierTpls,
                PurchaseCutoffTier = purchaseCutoff
            };
        }

        private async Task RunPlanningAsync()
        {
            SetRunEnabled(false);
            _lblStatus.Text = "Расчёт…";
            _plans.Clear();
            _rows.Clear();
            _bs.DataSource = null;
            ClearSortGlyphs();

            _cts?.Cancel();
            _cts = new CancellationTokenSource();

            try
            {
                var planets = (int)_numPlanets.Value;
                var ccLevel = (int)_numCc.Value;
                var mode = (PiMode)_cmbMode.SelectedItem!;
                var settings = ReadSettings();

                // Что планируем: один продукт или все PI
                List<int> productIds;
                string txt = _txtTypeId.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(txt))
                {
                    if (_repo == null)
                        throw new InvalidOperationException("Нужен репозиторий, чтобы перечислить все PI продукты.");
                    productIds = _repo.GetAllPiOutputTypeIds().OrderBy(x => x).ToList();
                }
                else
                {
                    if (!int.TryParse(txt, out var single))
                        throw new InvalidOperationException("Некорректный TypeID");
                    productIds = new List<int> { single };
                }

                int done = 0;
                foreach (var pid in productIds)
                {
                    _cts.Token.ThrowIfCancellationRequested();
                    var plan = await _planner.PlanAsync(pid, planets, ccLevel, mode, 0m, _cts.Token, settings);
                    _plans[pid] = plan;

                    var row = new PlanRow(plan, _repo)
                    {
                        RatingBn = _hist.TryRatingBn(pid)
                    };
                    _rows.Add(row);

                    done++;
                    if (done % 10 == 0 || done == productIds.Count)
                        _lblStatus.Text = $"Расчёт… {done}/{productIds.Count}";
                }

                _bs.DataSource = _rows;
                _bs.ResetBindings(false);
                UpdateButtonsState();

                _lblStatus.Text = $"OK — просчитано: {productIds.Count}";
            }
            catch (OperationCanceledException)
            {
                _lblStatus.Text = "Отменено.";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "ОШИБКА: " + ex.Message;
                MessageBox.Show(ex.ToString(), "Planning error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetRunEnabled(true);
            }
        }

        private async Task GetPricesForAllAsync()
        {
            if (_plans.Count == 0 || _prices == null) return;

            try
            {
                _btnGetPrices.Enabled = false;
                _lblStatus.Text = "Загрузка цен…";

                // typeIds для одного запроса
                var ids = new HashSet<int>();
                foreach (var p in _plans.Values)
                {
                    ids.Add(p.ProductTypeId);
                    foreach (var m in p.RequiredInputs) ids.Add(m.TypeId);
                }

                var regionId = (int)_numRegion.Value;
                var px = await _prices.GetBestPricesAsync(ids, regionId, CancellationToken.None);

                var revenueSide = _cmbRevenueSide.SelectedItem?.ToString() == "Buy" ? PriceSide.Buy : PriceSide.Sell;
                var costSide = _cmbCostSide.SelectedItem?.ToString() == "Sell" ? PriceSide.Sell : PriceSide.Buy;

                foreach (var row in _rows)
                {
                    if (_plans.TryGetValue(row.ProductTypeId, out var plan))
                        row.ApplyPnl(plan, px, revenueSide, costSide);
                }

                _bs.ResetBindings(false);
                _lblStatus.Text = "Цены применены.";
            }
            catch (Exception ex)
            {
                _lblStatus.Text = "ОШИБКА(цены): " + ex.Message;
                MessageBox.Show(ex.ToString(), "Price error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _btnGetPrices.Enabled = true;
            }
        }

        // ===== Helpers for selected row =====
        private PlanRow? SelectedRow =>
            _grid?.CurrentRow?.DataBoundItem as PlanRow;

        private ProductionPlanResult? SelectedPlan =>
            SelectedRow != null && _plans.TryGetValue(SelectedRow.ProductTypeId, out var p) ? p : null;

        private void UpdateButtonsState()
        {
            bool hasSel = SelectedRow != null && SelectedPlan != null;
            _btnDetails.Enabled = hasSel;
            _btnCopyBom.Enabled = hasSel && SelectedPlan!.RequiredInputs.Count > 0;
            _btnExportBom.Enabled = hasSel && SelectedPlan!.RequiredInputs.Count > 0;
            _btnExportPlan.Enabled = hasSel && SelectedPlan!.Planets.Count > 0;
            _btnGetPrices.Enabled = _prices != null && _plans.Count > 0;
        }

        private void SetRunEnabled(bool enabled)
        {
            _btnRun.Enabled = enabled;
            if (!enabled)
            {
                _btnDetails.Enabled = false;
                _btnCopyBom.Enabled = false;
                _btnExportBom.Enabled = false;
                _btnExportPlan.Enabled = false;
                _btnGetPrices.Enabled = false;
            }
        }

        private void OpenSelectedDetails()
        {
            var plan = SelectedPlan;
            if (plan == null) return;
            using var dlg = new PiPlanDetailsForm(plan, _repo);
            dlg.ShowDialog(this);
        }

        private void CopySelectedBomToClipboard()
        {
            var plan = SelectedPlan;
            if (plan == null) return;
            var lines = new List<string> { "TypeId,Tier,QtyPerHour" };
            lines.AddRange(plan.RequiredInputs
                .OrderBy(x => x.Tier).ThenBy(x => x.TypeId)
                .Select(x => $"{x.TypeId},{x.Tier},{x.QuantityPerHour:0.####}"));
            Clipboard.SetText(string.Join(Environment.NewLine, lines));
            _lblStatus.Text = $"BOM скопирован ({plan.RequiredInputs.Count}).";
        }

        private void ExportSelectedBom()
        {
            var plan = SelectedPlan;
            if (plan == null) return;

            using var sfd = new SaveFileDialog { Filter = "CSV files|*.csv", FileName = $"PI_BOM_{plan.ProductTypeId}.csv" };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            var lines = new List<string> { "TypeId,Name,Tier,QtyPerHour" };
            foreach (var m in plan.RequiredInputs.OrderBy(x => x.Tier).ThenBy(x => x.TypeId))
            {
                var name = _repo?.GetType(m.TypeId)?.TypeName ?? "";
                lines.Add($"{m.TypeId},{Escape(name)},{m.Tier},{m.QuantityPerHour:0.####}");
            }
            File.WriteAllLines(sfd.FileName, lines);
            _lblStatus.Text = $"BOM экспортирован: {sfd.FileName}";
        }

        private void ExportSelectedPlan()
        {
            var plan = SelectedPlan;
            if (plan == null) return;

            using var sfd = new SaveFileDialog { Filter = "CSV files|*.csv", FileName = $"PI_Plan_{plan.ProductTypeId}.csv" };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            var lines = new List<string> { "Role,ProductTypeId,ProductName,Facilities,OutPerHour,InPerHour" };
            foreach (var p in plan.Planets)
            {
                var name = p.ProductTypeId.HasValue
                    ? (_repo?.GetType(p.ProductTypeId.Value)?.TypeName ?? "")
                    : "";
                var fac = string.Join(";", p.Facilities.Select(kv => $"{kv.Key}:{kv.Value}"));
                var outTxt = string.Join(";", p.OutputPerHour.Select(kv => $"{kv.Key}:{kv.Value:0.###}"));
                var inTxt = string.Join(";", p.InputPerHour.Select(kv => $"{kv.Key}:{kv.Value:0.###}"));
                lines.Add($"{p.Role},{p.ProductTypeId},{Escape(name)},{Escape(fac)},{Escape(outTxt)},{Escape(inTxt)}");
            }
            File.WriteAllLines(sfd.FileName, lines);
            _lblStatus.Text = $"План экспортирован: {sfd.FileName}";
        }

        // ===== Сортировка =====
        private void Grid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.ColumnIndex >= _grid.Columns.Count) return;

            var col = _grid.Columns[e.ColumnIndex];
            var prop = col.DataPropertyName;
            if (string.IsNullOrEmpty(prop)) return;

            // переключение направления
            if (_sortProperty == prop) _sortAsc = !_sortAsc;
            else { _sortProperty = prop; _sortAsc = true; }

            ApplySorting(prop, _sortAsc);

            // показать глиф
            ClearSortGlyphs();
            col.HeaderCell.SortGlyphDirection = _sortAsc ? SortOrder.Ascending : SortOrder.Descending;
        }

        private void ApplySorting(string prop, bool asc)
        {
            if (_rows.Count == 0) return;

            Func<PlanRow, IComparable?> key = prop switch
            {
                "ProductName" => r => r.ProductName,
                "Tier" => r => r.Tier,
                "OutputPerDay" => r => r.OutputPerDay,
                "PlanetsUsed" => r => r.PlanetsUsed,
                "RevenuePerDay" => r => r.RevenuePerDay,
                "CostsPerDay" => r => r.CostsPerDay,
                "ProfitBuyPerDay" => r => r.ProfitBuyPerDay,
                "ProfitBuyPerMonth" => r => r.ProfitBuyPerMonth,
                "VolumePerMonthM3" => r => r.VolumePerMonthM3,
                "RatingBn" => r => r.RatingBn,
                "Note" => r => r.Note,
                _ => r => r.ProductName
            };

            IEnumerable<PlanRow> sorted = asc ? _rows.OrderBy(key) : _rows.OrderByDescending(key);
            var newList = sorted.ToList();

            _rows.Clear();
            _rows.AddRange(newList);
            _bs.DataSource = null;
            _bs.DataSource = _rows;
            _bs.ResetBindings(false);
        }

        private void ClearSortGlyphs()
        {
            foreach (DataGridViewColumn c in _grid.Columns)
                c.HeaderCell.SortGlyphDirection = SortOrder.None;
        }

        private static string Escape(string s)
        {
            if (s == null) return "";
            if (s.Contains(",") || s.Contains("\""))
                return "\"" + s.Replace("\"", "\"\"") + "\"";
            return s;
        }
    }
}
