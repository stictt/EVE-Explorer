using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Domain.Services;
using Domain.Infrastructure;
using Domain.Models.ResourceDTO;
using TestForm;
using Microsoft.VisualBasic.Logging;
using Loader.Infrastructure;

namespace OreTools
{
    public partial class OreDetailsForm : Form
    {
        private readonly SdeAggregateDTO _sde;
        private readonly IPriceProvider _priceProvider;
        private readonly int _regionId;

        private readonly int _typeId;
        private readonly string _oreName;
        private readonly double _unitsPerHourRaw;
        private readonly double _refineYield;
        private readonly bool _preferMaxUnitsPerCompressed;
        private readonly bool _formatMillions;

        private CancellationTokenSource? _cts;
        private Dictionary<int, (double buy, double sell)> _prices = new();

        private DataGridView dgvOre = new();
        private DataGridView dgvMins = new();
        private CheckBox chkUseCompressed = new();
        private Label lblTitle = new();
        private Panel busy = new();
        private TextBox txtOre = new();
        private TextBox txtMins = new();

        private sealed class OreLine
        {
            public int TypeID { get; set; }
            public string Name { get; set; } = "";
            public double UnitsPerHour { get; set; }
            public double BuyIskPerHour { get; set; }
            public double SellIskPerHour { get; set; }
            public string ModeText { get; set; } = "";
        }

        private sealed class MineralLine
        {
            public int TypeID { get; set; }
            public string Name { get; set; } = "";
            public double UnitsPerHour { get; set; }
            public double BuyIskPerHour { get; set; }
            public double SellIskPerHour { get; set; }
        }

        private static T? GetProp<T>(object src, string name)
        {
            var pi = src.GetType().GetProperty(name);
            if (pi == null) return default;
            var val = pi.GetValue(src);
            if (val is T tv) return tv;
            if (val == null) return default;
            return (T)Convert.ChangeType(val, typeof(T), CultureInfo.InvariantCulture);
        }

        // конструктор из строки главной таблицы
        public OreDetailsForm(
            SdeAggregateDTO sde,
            IPriceProvider priceProvider,
            int regionId,
            double refineYield,
            object row)
            : this(
                sde,
                priceProvider,
                regionId,
                GetProp<int>(row, "TypeID"),
                GetProp<string>(row, "Name") ?? "",
                Convert.ToDouble(GetProp<object>(row, "UnitsPerHour") ?? 0d, CultureInfo.InvariantCulture),
                refineYield,
                true,
                false)
        { }

        // основной конструктор
        public OreDetailsForm(
            SdeAggregateDTO sde,
            IPriceProvider priceProvider,
            int regionId,
            int typeId,
            string oreName,
            double unitsPerHour,
            double refineYield,
            bool preferMaxUnitsPerCompressed,
            bool formatMillions)
        {
            _sde = sde;
            _priceProvider = priceProvider;
            _regionId = regionId;

            _typeId = typeId;
            _oreName = oreName;
            _unitsPerHourRaw = unitsPerHour;
            _refineYield = refineYield;
            _preferMaxUnitsPerCompressed = preferMaxUnitsPerCompressed;
            _formatMillions = formatMillions;

            BuildUi();
            TryApplyDark();

            Shown += async (_, __) => await RefreshUiAsync();

            dgvOre.CellDoubleClick += (_, __) => TryOpenOrdersFromTop();
            dgvMins.CellDoubleClick += (_, __) => TryOpenOrdersFromMins();
        }

        private void TryOpenOrdersFromTop()
        {
            if (dgvOre.CurrentRow?.DataBoundItem is OreLine top && top.TypeID > 0)
            {
                using var f = new MarketOrdersForm(top.TypeID, top.Name, _regionId, null);
                f.ShowDialog(this);
            }
        }

        private void TryOpenOrdersFromMins()
        {
            if (dgvMins.CurrentRow?.DataBoundItem is MineralLine mr && mr.TypeID > 0)
            {
                using var f = new MarketOrdersForm(mr.TypeID, mr.Name, _regionId, null);
                f.ShowDialog(this);
            }
        }

        private void TryApplyDark()
        {
            try { UiStyle.ApplyDark(this); } catch { }
        }

        private void BuildUi()
        {
            Text = "Детали руды";
            Width = 1080;
            Height = 720;
            MinimumSize = new Size(900, 600);

            lblTitle.AutoSize = true;
            lblTitle.Font = new Font(Font, FontStyle.Bold);
            lblTitle.Text = $"{_oreName} [{_typeId}] — Руда";
            lblTitle.Location = new Point(10, 10);

            chkUseCompressed.Text = "Использовать сжатую (если доступно)";
            chkUseCompressed.AutoSize = true;
            chkUseCompressed.Location = new Point(10, 40);
            chkUseCompressed.CheckedChanged += async (_, __) => await RefreshUiAsync();

            dgvOre = CreateGrid();
            dgvOre.Top = 70;
            dgvOre.Left = 10;
            dgvOre.Width = ClientSize.Width - 20;
            dgvOre.Height = (int)(ClientSize.Height * 0.30);
            dgvOre.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            AddOreColumns(dgvOre);

            dgvMins = CreateGrid();
            dgvMins.Top = dgvOre.Bottom + 10;
            dgvMins.Left = 10;
            dgvMins.Width = ClientSize.Width - 20;
            dgvMins.Height = (int)(ClientSize.Height * 0.30);
            dgvMins.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
            AddMineralColumns(dgvMins);

            dgvOre.CellFormatting += OnGridCellFormatting;
            dgvMins.CellFormatting += OnGridCellFormatting;

            txtOre = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Left = 10,
                Top = dgvMins.Bottom + 10,
                Width = (ClientSize.Width - 30) / 2,
                Height = ClientSize.Height - (dgvMins.Bottom + 20),
                Anchor = AnchorStyles.Left | AnchorStyles.Bottom | AnchorStyles.Top
            };
            txtMins = new TextBox
            {
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Left = txtOre.Right + 10,
                Top = dgvMins.Bottom + 10,
                Width = (ClientSize.Width - 30) / 2,
                Height = ClientSize.Height - (dgvMins.Bottom + 20),
                Anchor = AnchorStyles.Right | AnchorStyles.Bottom | AnchorStyles.Top
            };

            busy = new Panel
            {
                BackColor = Color.FromArgb(160, 0, 0, 0),
                Visible = false,
                Left = 0,
                Top = 0,
                Width = ClientSize.Width,
                Height = ClientSize.Height,
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
            };
            var busyLabel = new Label
            {
                AutoSize = true,
                ForeColor = Color.White,
                Font = new Font(FontFamily.GenericSansSerif, 12, FontStyle.Bold),
                Text = "Загружаю цены...",
                BackColor = Color.Transparent
            };
            busy.Controls.Add(busyLabel);
            busyLabel.Location = new Point((busy.Width - busyLabel.Width) / 2, 8);

            Controls.Add(lblTitle);
            Controls.Add(chkUseCompressed);
            Controls.Add(dgvOre);
            Controls.Add(dgvMins);
            Controls.Add(txtOre);
            Controls.Add(txtMins);
            Controls.Add(busy);

            Resize += (_, __) =>
            {
                busy.Width = ClientSize.Width;
                busy.Height = ClientSize.Height;
            };
        }

        private static DataGridView CreateGrid() => new()
        {
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            MultiSelect = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            RowHeadersVisible = false,
            AutoGenerateColumns = false
        };

        private static DataGridViewCellStyle RightAlign()
            => new() { Alignment = DataGridViewContentAlignment.MiddleRight };

        private void AddOreColumns(DataGridView g)
        {
            g.Columns.Clear();
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(OreLine.TypeID), DataPropertyName = nameof(OreLine.TypeID), HeaderText = "TypeID", FillWeight = 10 });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(OreLine.Name), DataPropertyName = nameof(OreLine.Name), HeaderText = "Наименование", FillWeight = 35 });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(OreLine.UnitsPerHour), DataPropertyName = nameof(OreLine.UnitsPerHour), HeaderText = "Кол-во/час", FillWeight = 15, DefaultCellStyle = RightAlign() });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(OreLine.BuyIskPerHour), DataPropertyName = nameof(OreLine.BuyIskPerHour), HeaderText = "BUY ISK/час", FillWeight = 20, DefaultCellStyle = RightAlign() });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(OreLine.SellIskPerHour), DataPropertyName = nameof(OreLine.SellIskPerHour), HeaderText = "SELL ISK/час", FillWeight = 20, DefaultCellStyle = RightAlign() });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(OreLine.ModeText), DataPropertyName = nameof(OreLine.ModeText), HeaderText = "Режим", FillWeight = 12 });
        }

        private void AddMineralColumns(DataGridView g)
        {
            g.Columns.Clear();
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(MineralLine.TypeID), DataPropertyName = nameof(MineralLine.TypeID), HeaderText = "TypeID", FillWeight = 10 });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(MineralLine.Name), DataPropertyName = nameof(MineralLine.Name), HeaderText = "Минерал", FillWeight = 35 });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(MineralLine.UnitsPerHour), DataPropertyName = nameof(MineralLine.UnitsPerHour), HeaderText = "Кол-во/час", FillWeight = 15, DefaultCellStyle = RightAlign() });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(MineralLine.BuyIskPerHour), DataPropertyName = nameof(MineralLine.BuyIskPerHour), HeaderText = "BUY ISK/час", FillWeight = 20, DefaultCellStyle = RightAlign() });
            g.Columns.Add(new DataGridViewTextBoxColumn { Name = nameof(MineralLine.SellIskPerHour), DataPropertyName = nameof(MineralLine.SellIskPerHour), HeaderText = "SELL ISK/час", FillWeight = 20, DefaultCellStyle = RightAlign() });
        }

        private void OnGridCellFormatting(object? sender, DataGridViewCellFormattingEventArgs e)
        {
            if (sender is not DataGridView g) return;
            var col = g.Columns[e.ColumnIndex].Name;

            bool isUnits = col == nameof(OreLine.UnitsPerHour) || col == nameof(MineralLine.UnitsPerHour);
            bool isIsk = col == nameof(OreLine.BuyIskPerHour) || col == nameof(OreLine.SellIskPerHour) ||
                         col == nameof(MineralLine.BuyIskPerHour) || col == nameof(MineralLine.SellIskPerHour);

            if (isUnits && e.Value is double du)
            {
                e.Value = du.ToString("N0");
                e.FormattingApplied = true;
            }
            else if (isIsk && e.Value is double dv)
            {
                double v = _formatMillions ? (dv / 1_000_000.0) : dv;
                e.Value = v.ToString("N0");
                e.FormattingApplied = true;
            }
        }

        private async Task RefreshUiAsync()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            busy.Visible = true;
            try
            {
                var (viewTypeId, viewName, unitsPerHourView, modeText, unitsPerCompressed) = ResolveOreView();

                var need = new HashSet<int> { viewTypeId };
                if (_sde.Reprocessing.TryGetValue(_typeId, out var plan))
                    foreach (var m in plan.Outputs) need.Add(m.TypeID);

                _prices = await _priceProvider.GetBestPricesAsync(need, _regionId, ct);

                var oreRows = new List<OreLine>
                {
                    new OreLine
                    {
                        TypeID = viewTypeId,
                        Name = viewName,
                        UnitsPerHour = unitsPerHourView,
                        BuyIskPerHour = unitsPerHourView * GetBuy(viewTypeId),
                        SellIskPerHour = unitsPerHourView * GetSell(viewTypeId),
                        ModeText = modeText
                    }
                };
                AddTotalsRowForOre(oreRows);
                dgvOre.DataSource = oreRows;

                var mins = new List<MineralLine>();
                if (_sde.Reprocessing.TryGetValue(_typeId, out var reproc))
                {
                    var portion = Math.Max(1, reproc.PortionSize);
                    foreach (var o in reproc.Outputs)
                    {
                        var perUnit = (o.Quantity / (double)portion) * _refineYield;
                        var perHour = perUnit * _unitsPerHourRaw;
                        mins.Add(new MineralLine
                        {
                            TypeID = o.TypeID,
                            Name = o.Name,
                            UnitsPerHour = perHour,
                            BuyIskPerHour = perHour * GetBuy(o.TypeID),
                            SellIskPerHour = perHour * GetSell(o.TypeID)
                        });
                    }
                }
                AddTotalsRowForMinerals(mins);
                dgvMins.DataSource = mins;

                txtOre.Text = $"{viewName}\t{Math.Round(unitsPerHourView)}{(unitsPerCompressed > 1 ? $"\t(1 сжатая = {unitsPerCompressed} руды)" : "")}";
                txtMins.Text = string.Join(Environment.NewLine, mins.Where(m => m.TypeID != -1)
                    .Select(m => $"{m.Name}\t{Math.Round(m.UnitsPerHour)}"));
            }
            finally
            {
                busy.Visible = false;
            }
        }

        private (int viewTypeId, string viewName, double unitsPerHour, string modeText, int unitsPerCompressed) ResolveOreView()
        {
            var baseItem = _sde.Items[_typeId];
            var units = _unitsPerHourRaw;
            bool isIce = baseItem.Kind == Kind.IceOre;

            if (!_sde.Compression.TryGetValue(_typeId, out var options) || options.Count == 0 || !chkUseCompressed.Checked)
                return (_typeId, baseItem.Name, units, "Обычная", 1);

            double MapUpc(int outTypeId, double fallback)
            {
                if (isIce) return 1;
                int compPortion = 0;
                if (_sde.Reprocessing.TryGetValue(outTypeId, out var compPlan))
                    compPortion = Math.Max(1, compPlan.PortionSize);

                if (compPortion == 100) return 1;   // Compressed ... -> 1:1
                if (compPortion == 1) return 100;   // Batch ...      -> 100:1
                return fallback > 0 ? fallback : 1;
            }

            var scored = options
                .Select(o =>
                {
                    int compPortion = _sde.Reprocessing.TryGetValue(o.OutTypeID, out var cp) ? Math.Max(1, cp.PortionSize) : 0;
                    return new
                    {
                        Opt = o,
                        Upc = MapUpc(o.OutTypeID, o.UnitsPerCompressed),
                        Portion = compPortion
                    };
                })
                .ToList();

            // приоритет: portion=100, иначе — максимальный UPC
            var chosen = scored.FirstOrDefault(x => x.Portion == 100)
                      ?? scored.OrderByDescending(x => x.Upc).First();

            var compressedUnitsPerHour = units / Math.Max(1.0, chosen.Upc);
            return (chosen.Opt.OutTypeID, chosen.Opt.OutName, compressedUnitsPerHour,
                    $"Сжатая (1 = {chosen.Upc})", (int)Math.Round(chosen.Upc));
        }

        private double GetBuy(int typeId) => _prices.TryGetValue(typeId, out var p) ? p.buy : 0.0;
        private double GetSell(int typeId) => _prices.TryGetValue(typeId, out var p) ? p.sell : 0.0;

        private void AddTotalsRowForOre(List<OreLine> rows)
        {
            rows.RemoveAll(r => r.TypeID == -1);

            var sumUnits = rows.Sum(r => r.UnitsPerHour);
            var sumBuy = rows.Sum(r => r.BuyIskPerHour);
            var sumSell = rows.Sum(r => r.SellIskPerHour);

            rows.Add(new OreLine
            {
                TypeID = -1,
                Name = "ИТОГО",
                UnitsPerHour = sumUnits,
                BuyIskPerHour = sumBuy,
                SellIskPerHour = sumSell,
                ModeText = ""
            });

            dgvOre.DataBindingComplete += (_, __) => StyleTotalsRow(dgvOre);
        }

        private void AddTotalsRowForMinerals(List<MineralLine> rows)
        {
            rows.RemoveAll(r => r.TypeID == -1);

            var sumUnits = rows.Sum(r => r.UnitsPerHour);
            var sumBuy = rows.Sum(r => r.BuyIskPerHour);
            var sumSell = rows.Sum(r => r.SellIskPerHour);

            rows.Add(new MineralLine
            {
                TypeID = -1,
                Name = "ИТОГО",
                UnitsPerHour = sumUnits,
                BuyIskPerHour = sumBuy,
                SellIskPerHour = sumSell
            });

            dgvMins.DataBindingComplete += (_, __) => StyleTotalsRow(dgvMins);
        }

        private void StyleTotalsRow(DataGridView grid)
        {
            try
            {
                var bold = new Font(grid.Font, FontStyle.Bold);
                var back = Color.FromArgb(40, 46, 56);

                foreach (DataGridViewRow r in grid.Rows)
                {
                    bool isTotal = (r.DataBoundItem is OreLine o && o.TypeID == -1)
                                   || (r.DataBoundItem is MineralLine m && m.TypeID == -1);
                    if (!isTotal) continue;

                    r.ReadOnly = true;
                    r.DefaultCellStyle.Font = bold;
                    r.DefaultCellStyle.BackColor = back;
                    r.DefaultCellStyle.SelectionBackColor = back;
                    r.DefaultCellStyle.SelectionForeColor = grid.DefaultCellStyle.ForeColor;

                    foreach (DataGridViewColumn c in grid.Columns)
                        c.SortMode = DataGridViewColumnSortMode.NotSortable;
                    break;
                }
            }
            catch { }
        }
    }
}
