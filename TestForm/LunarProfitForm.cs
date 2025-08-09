using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Domain.Services;            // SdeAggregateDTO, Kind, BlueprintRecipeDTO
using Domain.Models.ResourceDTO;  // ItemExtDTO
using Domain.Analytics;           // LunarAnalyzer и DTO результатов
using Loader.Services;
using TestForm;            // твой кеш если понадобится (не обяз.)

namespace OreTools
{
    public sealed class LunarProfitForm : Form
    {
        // --------- зависимости ---------
        private readonly SdeAggregateDTO _sde;
        private readonly IPriceProvider _priceProvider;
        private readonly IAvgPriceProvider? _avgProvider;
        private readonly int _regionId;

        // аналитик
        private LunarAnalyzer _analyzer;

        // кэш цен последнего запроса
        private readonly Dictionary<int, (double buy, double sell)> _px = new();

        // --------- UI ---------
        private readonly DataGridView grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AllowUserToAddRows = false, AllowUserToDeleteRows = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect };
        private readonly ComboBox cmbMode = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
        private readonly CheckBox chkViaOre = new CheckBox { Text = "Стоимость через руду (ViaOre)" };
        private readonly NumericUpDown numRefine = new NumericUpDown { Minimum = 0, Maximum = 100, DecimalPlaces = 0, Value = 100, Width = 70 };
        private readonly NumericUpDown numRxME = new NumericUpDown { Minimum = 0, Maximum = 100, DecimalPlaces = 0, Value = 0, Width = 60 };
        private readonly NumericUpDown numRxTE = new NumericUpDown { Minimum = 0, Maximum = 100, DecimalPlaces = 0, Value = 0, Width = 60 };
        private readonly NumericUpDown numBpME = new NumericUpDown { Minimum = 0, Maximum = 100, DecimalPlaces = 0, Value = 0, Width = 60 };
        private readonly NumericUpDown numBpTE = new NumericUpDown { Minimum = 0, Maximum = 100, DecimalPlaces = 0, Value = 0, Width = 60 };
        private readonly ComboBox cmbMaterialFilter = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240 };

        private readonly Button btnRefresh = new Button { Text = "Обновить", AutoSize = true };
        private readonly ProgressBar progress = new ProgressBar { Width = 160, Style = ProgressBarStyle.Continuous };
        private readonly Label lblStatus = new Label { AutoSize = true, Text = "Готово" };

        private CancellationTokenSource? _cts;
        private bool _sortAsc = false;
        private string _lastSortCol = "";

        public LunarProfitForm(SdeAggregateDTO sde,
                               IPriceProvider priceProvider,
                               IAvgPriceProvider? avgProvider = null,
                               int regionId = 10000002) // Jita
        {
            _sde = sde;
            _priceProvider = priceProvider;
            _avgProvider = avgProvider;
            _regionId = regionId;

            Text = "Lunar Profit — Ore / Reactions / Blueprints";
            Width = 1200;
            Height = 720;
            StartPosition = FormStartPosition.CenterParent;

            // analyzer + делегаты цен
            _analyzer = new LunarAnalyzer(_sde,
                avg30dGetter: (id) => _avgProvider?.TryGetAvgPrice(id),
                nowGetter: (id) => _px.TryGetValue(id, out var p) ? p : (0, 0));

            BuildUi();
            TryApplyDark();
            InitData();
        }

        private void TryApplyDark()
        {
            try { UiStyle.ApplyDark(this); } catch { }
        }

        private void BuildUi()
        {
            var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8), WrapContents = true };

            cmbMode.Items.AddRange(new object[]
            {
                "Ore ➜ Moon materials",
                "Terminal reactions (activity=11)",
                "Blueprints requiring reactions"
            });
            cmbMode.SelectedIndex = 0;

            var pnlRefine = Wrap("Refine %", numRefine);
            var pnlRxME = Wrap("Rx ME %", numRxME);
            var pnlRxTE = Wrap("Rx TE %", numRxTE);
            var pnlBpME = Wrap("BP ME %", numBpME);
            var pnlBpTE = Wrap("BP TE %", numBpTE);
            var pnlMatFilter = Wrap("Фильтр по материалу", cmbMaterialFilter);

            btnRefresh.Click += async (_, __) => await RefreshAsync();
            grid.CellDoubleClick += Grid_CellDoubleClick;
            grid.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;

            top.Controls.Add(new Label { Text = "Режим:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) });
            top.Controls.Add(cmbMode);
            top.Controls.Add(chkViaOre);
            top.Controls.Add(pnlRefine);
            top.Controls.Add(pnlRxME);
            top.Controls.Add(pnlRxTE);
            top.Controls.Add(pnlBpME);
            top.Controls.Add(pnlBpTE);
            top.Controls.Add(pnlMatFilter);
            top.Controls.Add(btnRefresh);
            top.Controls.Add(progress);
            top.Controls.Add(lblStatus);

            Controls.Add(grid);
            Controls.Add(top);
        }

        private Panel Wrap(string caption, Control inner)
        {
            var p = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.LeftToRight, Margin = new Padding(8, 4, 8, 4) };
            p.Controls.Add(new Label { Text = caption + ":", AutoSize = true, Padding = new Padding(0, 8, 4, 0) });
            p.Controls.Add(inner);
            return p;
        }

        private void InitData()
        {
            // заполняем фильтр по луным материалам (и "Все")
            cmbMaterialFilter.Items.Clear();
            cmbMaterialFilter.Items.Add("(Все материалы)");

            // moon materials = продукты реакций или материалы реакций
            var moonMats = new HashSet<int>(
                _sde.Recipes.Where(r => r.ActivityID == 11)
                            .SelectMany(r => r.Materials.Select(m => m.TypeID)
                                             .Concat(r.Products.Select(p => p.TypeID))));

            var names = moonMats.Select(id => (_sde.Items.TryGetValue(id, out var it) ? it.Name : id.ToString(), id))
                                .OrderBy(x => x.Item1, StringComparer.CurrentCultureIgnoreCase)
                                .ToList();

            foreach (var (nm, id) in names)
                cmbMaterialFilter.Items.Add(new ComboItem(nm, id));

            cmbMaterialFilter.SelectedIndex = 0;

            // первоначальная отрисовка
            _ = RefreshAsync();
        }

        // ===================== событийка =====================

        private async void Grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;

            // простая деталька: для реакций/чертежей покажем список базовых входов (для копирования)
            // для руды — распишем выход рефайна
            try
            {
                var mode = cmbMode.SelectedIndex;
                if (mode == 0)
                {
                    if (grid.Rows[e.RowIndex].DataBoundItem is OreRow ore)
                    {
                        var res = _analyzer.EvaluateOreRefine(ore.TypeID);
                        var lines = res.Outputs.Select(o => $"{o.name}\t{o.qtyPerUnit:N4}");
                        ShowBigText($"Рефайн: {res.OreName}", string.Join(Environment.NewLine, lines));
                    }
                }
                else if (mode == 1)
                {
                    if (grid.Rows[e.RowIndex].DataBoundItem is ReactionRow rx)
                    {
                        var eff = GetEff();
                        var res = _analyzer.EvaluateReaction(rx.ProductTypeID, chkViaOre.Checked ? LunarAnalyzer.InputCostMode.ViaOre : LunarAnalyzer.InputCostMode.MarketMaterials, eff);
                        var txt = _analyzer.MakeClipboardText(res.BaseInputs);
                        ShowBigText($"Базовые входы (реакция): {rx.Name}", txt);
                    }
                }
                else
                {
                    if (grid.Rows[e.RowIndex].DataBoundItem is BlueprintRow bp)
                    {
                        var eff = GetEff();
                        var res = _analyzer.EvaluateBlueprint(bp.ProductTypeID, chkViaOre.Checked ? LunarAnalyzer.InputCostMode.ViaOre : LunarAnalyzer.InputCostMode.MarketMaterials, eff);
                        var txt = _analyzer.MakeClipboardText(res.BaseInputs);
                        ShowBigText($"Базовые входы (чертёж): {bp.Name}", txt);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ShowBigText(string title, string text)
        {
            using var dlg = new Form
            {
                Text = title,
                StartPosition = FormStartPosition.CenterParent,
                Width = 800,
                Height = 600
            };
            TryApplyDarkTo(dlg);

            var tb = new TextBox { Multiline = true, Dock = DockStyle.Fill, ScrollBars = ScrollBars.Both, ReadOnly = true, WordWrap = false, Text = text };
            dlg.Controls.Add(tb);
            dlg.ShowDialog(this);
        }

        private void TryApplyDarkTo(Form f)
        {
            try { UiStyle.ApplyDark(f); } catch { }
        }

        private void Grid_ColumnHeaderMouseClick(object? sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0) return;
            var name = grid.Columns[e.ColumnIndex].DataPropertyName ?? grid.Columns[e.ColumnIndex].Name;
            if (string.IsNullOrEmpty(name)) name = grid.Columns[e.ColumnIndex].HeaderText;

            _sortAsc = (_lastSortCol == name) ? !_sortAsc : false;
            _lastSortCol = name;

            // Перебиндим отсортированный список
            if (grid.DataSource is BindingList<OreRow> ores)
                grid.DataSource = new BindingList<OreRow>(SortList(ores.ToList(), name, _sortAsc));
            else if (grid.DataSource is BindingList<ReactionRow> rxs)
                grid.DataSource = new BindingList<ReactionRow>(SortList(rxs.ToList(), name, _sortAsc));
            else if (grid.DataSource is BindingList<BlueprintRow> bps)
                grid.DataSource = new BindingList<BlueprintRow>(SortList(bps.ToList(), name, _sortAsc));
        }

        private static List<T> SortList<T>(List<T> list, string prop, bool asc)
        {
            var p = typeof(T).GetProperty(prop);
            if (p == null) return list;
            return asc
                ? list.OrderBy(x => p.GetValue(x, null)).ToList()
                : list.OrderByDescending(x => p.GetValue(x, null)).ToList();
        }

        // ===================== обновление =====================

        private LunarAnalyzer.Eff GetEff() => new LunarAnalyzer.Eff
        {
            ReactionME = (double)numRxME.Value / 100.0,
            ReactionTE = (double)numRxTE.Value / 100.0,
            BpME = (double)numBpME.Value / 100.0,
            BpTE = (double)numBpTE.Value / 100.0
        };

        private async Task RefreshAsync()
        {
            btnRefresh.Enabled = false;
            progress.Style = ProgressBarStyle.Marquee;
            lblStatus.Text = "Сбор цен...";
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                _px.Clear();

                _analyzer.RefineYield = (double)numRefine.Value / 100.0;

                // 1) определить нужные typeId под выбранный режим
                var needed = CollectNeededTypes();

                // 2) подтянуть цены разом
                var prices = await _priceProvider.GetBestPricesAsync(needed, _regionId, ct);
                foreach (var kv in prices) _px[kv.Key] = kv.Value;

                progress.Style = ProgressBarStyle.Continuous;
                progress.Value = 50;
                lblStatus.Text = "Расчёт...";

                // 3) посчитать и отрисовать
                var mode = cmbMode.SelectedIndex;
                if (mode == 0)
                    await BuildOreAsync();
                else if (mode == 1)
                    await BuildReactionsAsync();
                else
                    await BuildBlueprintsAsync();

                lblStatus.Text = $"Готово. Всего типов: {needed.Count:N0}";
                progress.Value = 100;
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "Отменено.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ошибка: " + ex.Message;
            }
            finally
            {
                btnRefresh.Enabled = true;
                progress.Style = ProgressBarStyle.Continuous;
            }
        }

        private HashSet<int> CollectNeededTypes()
        {
            var mode = cmbMode.SelectedIndex;
            var set = new HashSet<int>();

            // Всегда: продукты и материалы реакций — это moon-материалы.
            var rxAll = _sde.Recipes.Where(r => r.ActivityID == 11).ToList();

            if (mode == 0)
            {
                // Ore -> Moon mats: все лунные руды (включая сжатые) + их рефайн-выход
                var ores = _sde.Items.Values.Where(i => i.Published && (i.Kind == Kind.MoonOre));
                foreach (var o in ores)
                {
                    set.Add(o.TypeID);
                    if (_sde.Reprocessing.TryGetValue(o.TypeID, out var plan))
                        foreach (var outp in plan.Outputs) set.Add(outp.TypeID);
                }
            }
            else
            {
                // Reactions / Blueprints: всё, что может пригодиться
                // 1) все moon-materials и реакции над ними
                foreach (var r in rxAll)
                {
                    foreach (var m in r.Materials) set.Add(m.TypeID);
                    foreach (var p in r.Products) set.Add(p.TypeID);
                }

                // 2) все лунные руды (для режима ViaOre)
                if (chkViaOre.Checked)
                {
                    foreach (var it in _sde.Items.Values.Where(i => i.Kind == Kind.MoonOre))
                        set.Add(it.TypeID);
                    // + их выход рефайна, чтобы оценивать «лучшую руду»
                    foreach (var it in _sde.Items.Values.Where(i => i.Kind == Kind.MoonOre))
                        if (_sde.Reprocessing.TryGetValue(it.TypeID, out var plan))
                            foreach (var outp in plan.Outputs) set.Add(outp.TypeID);
                }

                // 3) продукты manufacturing, которые требуют реакций
                if (mode == 2)
                {
                    var rxProducts = new HashSet<int>(rxAll.SelectMany(r => r.Products.Select(p => p.TypeID)));
                    foreach (var r in _sde.Recipes.Where(r => r.ActivityID == 1))
                    {
                        if (r.Materials.Any(m => rxProducts.Contains(m.TypeID)))
                        {
                            foreach (var m in r.Materials) set.Add(m.TypeID);
                            foreach (var p in r.Products) set.Add(p.TypeID);
                        }
                    }
                }
            }

            return set;
        }

        private Task BuildOreAsync()
        {
            // фильтр по материалу (если выбран конкретный)
            int? materialId = (cmbMaterialFilter.SelectedItem as ComboItem)?.Id;

            var rows = new List<OreRow>();
            foreach (var it in _sde.Items.Values.Where(i => i.Published && i.Kind == Kind.MoonOre))
            {
                if (!_sde.Reprocessing.TryGetValue(it.TypeID, out var plan)) continue;

                // если задан фильтр по материалу — пропускаем руды, которые его не дают
                if (materialId.HasValue && !plan.Outputs.Any(o => o.TypeID == materialId.Value))
                    continue;

                var res = _analyzer.EvaluateOreRefine(it.TypeID);
                rows.Add(new OreRow
                {
                    TypeID = res.OreTypeID,
                    Name = res.OreName,
                    IsCompressed = res.IsCompressed,
                    UnitVolume = res.UnitVolume,
                    PortionSize = res.PortionSize,
                    OreBuy = res.OreBuy,
                    OreSell = res.OreSell,
                    RefineSellPerUnit = res.RefineSellPerUnit,
                    MarginPerUnit = res.MarginPerUnit
                });
            }

            var ordered = rows.OrderByDescending(r => r.MarginPerUnit).ToList();
            grid.DataSource = new BindingList<OreRow>(ordered);
            AutoColumns();
            return Task.CompletedTask;
        }

        private Task BuildReactionsAsync()
        {
            var eff = GetEff();
            var mode = chkViaOre.Checked ? LunarAnalyzer.InputCostMode.ViaOre : LunarAnalyzer.InputCostMode.MarketMaterials;

            var list = _analyzer.FindTerminalReactions();
            var rows = new List<ReactionRow>(list.Count);

            foreach (var (pid, name, qty) in list)
            {
                var r = _analyzer.EvaluateReaction(pid, mode, eff);
                rows.Add(new ReactionRow
                {
                    ProductTypeID = pid,
                    Name = name,
                    QtyOut = r.QtyOutPerRun,
                    InputsCost = r.BaseInputsCost,
                    Tax = r.TotalTaxIsk,
                    RevenueBuy = r.RevenueBuy,
                    RevenueSell = r.RevenueSell,
                    ProfitToBuy = r.ProfitToBuy,
                    ProfitToSell = r.ProfitToSell,
                    Time = TimeSpan.FromSeconds(r.TimeSeconds)
                });
            }

            var ordered = rows.OrderByDescending(x => x.ProfitToSell).ToList();
            grid.DataSource = new BindingList<ReactionRow>(ordered);
            AutoColumns();
            return Task.CompletedTask;
        }

        private Task BuildBlueprintsAsync()
        {
            var eff = GetEff();
            var mode = chkViaOre.Checked ? LunarAnalyzer.InputCostMode.ViaOre : LunarAnalyzer.InputCostMode.MarketMaterials;

            var list = _analyzer.FindBlueprintsRequiringReactions();
            var rows = new List<BlueprintRow>(list.Count);

            foreach (var (pid, name, qty) in list)
            {
                var r = _analyzer.EvaluateBlueprint(pid, mode, eff);
                rows.Add(new BlueprintRow
                {
                    ProductTypeID = pid,
                    Name = name,
                    QtyOut = r.QtyOutPerRun,
                    InputsCost = r.BaseInputsCost,
                    Tax = r.TotalTaxIsk,
                    RevenueBuy = r.RevenueBuy,
                    RevenueSell = r.RevenueSell,
                    ProfitToBuy = r.ProfitToBuy,
                    ProfitToSell = r.ProfitToSell,
                    Time = TimeSpan.FromSeconds(r.TimeSeconds)
                });
            }

            var ordered = rows.OrderByDescending(x => x.ProfitToSell).ToList();
            grid.DataSource = new BindingList<BlueprintRow>(ordered);
            AutoColumns();
            return Task.CompletedTask;
        }

        private void AutoColumns()
        {
            grid.AutoGenerateColumns = true;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells;
            grid.Columns.Cast<DataGridViewColumn>().ToList().ForEach(c =>
            {
                c.SortMode = DataGridViewColumnSortMode.Programmatic;
            });

            // красивые форматы чисел
            void fmt(string col, string format) { if (grid.Columns[col] != null) grid.Columns[col].DefaultCellStyle.Format = format; }

            if (grid.DataSource is BindingList<OreRow>)
            {
                fmt(nameof(OreRow.UnitVolume), "N2");
                fmt(nameof(OreRow.OreBuy), "N2");
                fmt(nameof(OreRow.OreSell), "N2");
                fmt(nameof(OreRow.RefineSellPerUnit), "N2");
                fmt(nameof(OreRow.MarginPerUnit), "N2");
            }
            else if (grid.DataSource is BindingList<ReactionRow>)
            {
                fmt(nameof(ReactionRow.QtyOut), "N2");
                fmt(nameof(ReactionRow.InputsCost), "N2");
                fmt(nameof(ReactionRow.Tax), "N2");
                fmt(nameof(ReactionRow.RevenueBuy), "N2");
                fmt(nameof(ReactionRow.RevenueSell), "N2");
                fmt(nameof(ReactionRow.ProfitToBuy), "N2");
                fmt(nameof(ReactionRow.ProfitToSell), "N2");
            }
            else if (grid.DataSource is BindingList<BlueprintRow>)
            {
                fmt(nameof(BlueprintRow.QtyOut), "N2");
                fmt(nameof(BlueprintRow.InputsCost), "N2");
                fmt(nameof(BlueprintRow.Tax), "N2");
                fmt(nameof(BlueprintRow.RevenueBuy), "N2");
                fmt(nameof(BlueprintRow.RevenueSell), "N2");
                fmt(nameof(BlueprintRow.ProfitToBuy), "N2");
                fmt(nameof(BlueprintRow.ProfitToSell), "N2");
            }
        }

        // ===================== модели строк =====================

        private sealed class ComboItem
        {
            public string Name { get; }
            public int Id { get; }
            public ComboItem(string name, int id) { Name = name; Id = id; }
            public override string ToString() => Name;
        }

        private sealed class OreRow
        {
            public int TypeID { get; set; }
            public string Name { get; set; } = "";
            public bool IsCompressed { get; set; }
            public double UnitVolume { get; set; }
            public int PortionSize { get; set; }

            public double OreBuy { get; set; }
            public double OreSell { get; set; }
            public double RefineSellPerUnit { get; set; }
            public double MarginPerUnit { get; set; }
        }

        private sealed class ReactionRow
        {
            public int ProductTypeID { get; set; }
            public string Name { get; set; } = "";
            public double QtyOut { get; set; }
            public double InputsCost { get; set; }
            public double Tax { get; set; }
            public double RevenueBuy { get; set; }
            public double RevenueSell { get; set; }
            public double ProfitToBuy { get; set; }
            public double ProfitToSell { get; set; }
            public TimeSpan Time { get; set; }
        }

        private sealed class BlueprintRow
        {
            public int ProductTypeID { get; set; }
            public string Name { get; set; } = "";
            public double QtyOut { get; set; }
            public double InputsCost { get; set; }
            public double Tax { get; set; }
            public double RevenueBuy { get; set; }
            public double RevenueSell { get; set; }
            public double ProfitToBuy { get; set; }
            public double ProfitToSell { get; set; }
            public TimeSpan Time { get; set; }
        }
    }
}
