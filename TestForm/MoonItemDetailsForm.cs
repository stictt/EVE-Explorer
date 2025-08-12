using ANALYTICS;
using Domain.Infrastructure;       // IPriceProvider
using Domain.Models;               // OrderHistoryMonthList
using Loader.Infrastructure;        // BinaryCachingService, Paths
using OreTools;                    // MarketOrdersForm
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TestForm
{
    public partial class MoonItemDetailsForm : Form
    {
        // ---- локальные ВМ ----
        private sealed class LineVM
        {
            public int TypeID { get; set; }
            public string Name { get; set; } = "";
            public double Qty { get; set; }

            public double Rating { get; set; }   // из OrderHistoryMonthList
            public double AvgVol { get; set; }   // из OrderHistoryMonthList

            public double Buy { get; set; }      // цена покупки (BUY)
            public double Sell { get; set; }     // цена продажи (SELL)

            public double SumBuy => Qty * Buy;
            public double SumSell => Qty * Sell;
        }

        private sealed class Header
        {
            public int ProductTypeID { get; set; }
            public string Name { get; set; } = "";
            public double UnitsPerCycle { get; set; }
            public double CycleSeconds { get; set; }
        }

        private readonly BindingList<LineVM> _inputs = new BindingList<LineVM>();
        private readonly BindingList<LineVM> _outputs = new BindingList<LineVM>();

        // ценовой провайдер и регион — форма сама тянет цены
        private readonly IPriceProvider _priceProvider;
        private readonly int _regionId;

        // история (рейтинг/средний объём)
        private readonly Dictionary<int, (double rating, double avgVol)> _histByType;

        public MoonItemDetailsForm(object payload, IPriceProvider priceProvider, int regionId)
        {
            _priceProvider = priceProvider;
            _regionId = regionId;

            _histByType = LoadHistory();

            InitializeComponent();
            TryApplyDark();

            // распакуем payload (чистый BOM/Outputs из MoonIndustryForm)
            var head = ExtractHeader(payload);
            var ins = ExtractLines(GetProp(payload, "Inputs"));
            var outs = ExtractLines(GetProp(payload, "Outputs"));

            foreach (var i in ins) _inputs.Add(Enrich(i));
            foreach (var o in outs) _outputs.Add(Enrich(o));

            // шапка/биндинги/события
            Text = head.Name + " [" + head.ProductTypeID + "] — детали";
            lblHeader.Text = $"шт/цикл: {head.UnitsPerCycle:N0} • сек/цикл: {head.CycleSeconds:N0}";

            gridIn.DataSource = _inputs;
            gridOut.DataSource = _outputs;
            BuildColumns(gridIn);
            BuildColumns(gridOut);

            // двойной клик -> MarketOrdersForm
            gridIn.CellDoubleClick += (_, e) => OpenOrders(gridIn, e.RowIndex);
            gridOut.CellDoubleClick += (_, e) => OpenOrders(gridOut, e.RowIndex);

            // безопасная инициализация сплиттеров
            this.Shown += async (_, __) =>
            {
                BeginInvoke(new Action(SafeInitSplitters));
                await LoadPricesAsync(); // подтянем BUY/SELL асинхронно
            };
            mainSplit.SizeChanged += (_, __) => SafeSetSplitterDistance(mainSplit);
            splitTop.SizeChanged += (_, __) => SafeSetSplitterDistance(splitTop);
            splitBottom.SizeChanged += (_, __) => SafeSetSplitterDistance(splitBottom);

            // первичные футеры/тексты
            UpdateFooters();
            RebuildTextAreas();
        }

        private LineVM Enrich(LineVM x)
        {
            if (_histByType.TryGetValue(x.TypeID, out var h))
            {
                x.Rating = h.rating;
                x.AvgVol = h.avgVol;
            }
            return x;
        }

        private Dictionary<int, (double rating, double avgVol)> LoadHistory()
        {
            var map = new Dictionary<int, (double, double)>();
            try
            {
                var cache = new BinaryCachingService();
                if (cache.TryLoad<OrderHistoryMonthList>(Paths.OrderHistoryMonthPath, out var data, out var _) && data != null)
                {
                    foreach (var i in data.List)
                        map[i.TypeId] = (i.Rating, i.AverageVolume);
                }
            }
            catch { /* без истории ок */ }
            return map;
        }

        private async Task LoadPricesAsync()
        {
            try
            {
                // собрать уникальные typeId
                var ids = new HashSet<int>();
                foreach (var i in _inputs) ids.Add(i.TypeID);
                foreach (var o in _outputs) ids.Add(o.TypeID);

                var prices = await _priceProvider.GetBestPricesAsync(ids, _regionId, CancellationToken.None);

                // проставим цены
                void apply(BindingList<LineVM> list)
                {
                    foreach (var it in list)
                    {
                        if (prices.TryGetValue(it.TypeID, out var p))
                        {
                            it.Buy = p.bestBuy;
                            it.Sell = p.bestSell;
                        }
                    }
                }

                apply(_inputs);
                apply(_outputs);

                gridIn.Refresh();
                gridOut.Refresh();
                UpdateFooters();
                RebuildTextAreas();
            }
            catch (Exception ex)
            {
                // в крайнем случае просто оставим 0, чтобы не падать
                Console.WriteLine(ex);
            }
        }

        private void OpenOrders(DataGridView g, int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= g.Rows.Count) return;

            if (g.Rows[rowIndex].DataBoundItem is not LineVM vm) return;

            using var dlg = new MarketOrdersForm(vm.TypeID, vm.Name, _regionId);
            dlg.StartPosition = FormStartPosition.CenterParent;
            dlg.ShowDialog(this);
        }

        // ===== оформление =====

        private void TryApplyDark()
        {
            try
            {
                var t = Type.GetType("UiStyle");
                var m = t?.GetMethod("ApplyDark", BindingFlags.Public | BindingFlags.Static);
                if (m != null) m.Invoke(null, new object[] { this });
            }
            catch { /* необязательно */ }

            Color caption = Color.Gainsboro;
            lblHeader.ForeColor = caption;
            lblInputsTotal.ForeColor = caption;
            lblOutputsTotal.ForeColor = caption;
            lblInFooter.ForeColor = Color.Gold;
            lblOutFooter.ForeColor = Color.Gold;

            void StyleGrid(DataGridView g)
            {
                g.EnableHeadersVisualStyles = false;
                g.BackgroundColor = Color.FromArgb(37, 37, 38);
                g.GridColor = Color.FromArgb(62, 62, 64);
                g.DefaultCellStyle.BackColor = Color.FromArgb(30, 30, 30);
                g.DefaultCellStyle.ForeColor = Color.Gainsboro;
                g.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(26, 26, 28);
                g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
                g.ColumnHeadersDefaultCellStyle.ForeColor = Color.Gainsboro;
                g.ColumnHeadersDefaultCellStyle.Font = new Font(g.Font, FontStyle.Bold);
                g.RowHeadersVisible = false;
            }

            StyleGrid(gridIn);
            StyleGrid(gridOut);

            txtLeft.BackColor = Color.FromArgb(30, 30, 30);
            txtRight.BackColor = Color.FromArgb(30, 30, 30);
            txtLeft.ForeColor = Color.Gainsboro;
            txtRight.ForeColor = Color.Gainsboro;
        }

        private void BuildColumns(DataGridView g)
        {
            g.AutoGenerateColumns = false;
            g.Columns.Clear();

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(LineVM.TypeID),
                HeaderText = "TypeID",
                Width = 80,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(LineVM.Name),
                HeaderText = "Название",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(LineVM.Qty),
                HeaderText = "Кол-во",
                Width = 110,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N3" }
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(LineVM.Rating),
                HeaderText = "Rating",
                Width = 110,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(LineVM.AvgVol),
                HeaderText = "AvgVol",
                Width = 110,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(LineVM.Buy),
                HeaderText = "BUY",
                Width = 110,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(LineVM.Sell),
                HeaderText = "SELL",
                Width = 110,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });

            g.ColumnHeaderMouseClick -= Grid_ColumnHeaderMouseClick;
            g.ColumnHeaderMouseClick += Grid_ColumnHeaderMouseClick;
        }

        private void Grid_ColumnHeaderMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            var g = (DataGridView)sender;
            if (e.ColumnIndex < 0) return;

            var col = g.Columns[e.ColumnIndex];
            var prop = col.DataPropertyName;
            if (string.IsNullOrEmpty(prop)) return;

            var list = (BindingList<LineVM>)g.DataSource;
            bool asc = col.HeaderCell.SortGlyphDirection != SortOrder.Ascending;
            var sorted = asc
                ? list.OrderBy(x => GetPropValue(x, prop))
                : list.OrderByDescending(x => GetPropValue(x, prop));

            g.DataSource = new BindingList<LineVM>(sorted.ToList());
            foreach (DataGridViewColumn c in g.Columns) c.HeaderCell.SortGlyphDirection = SortOrder.None;
            col.HeaderCell.SortGlyphDirection = asc ? SortOrder.Ascending : SortOrder.Descending;
        }

        private static object GetPropValue(object obj, string prop)
        {
            var p = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            return p != null ? (p.GetValue(obj) ?? 0) : 0;
        }

        // ===== футеры и текстовые зоны =====

        private void UpdateFooters()
        {
            double inBuy = _inputs.Sum(x => x.SumBuy);
            double inSell = _inputs.Sum(x => x.SumSell);
            double outBuy = _outputs.Sum(x => x.SumBuy);
            double outSell = _outputs.Sum(x => x.SumSell);

            lblInFooter.Text = $"Σ BUY {inBuy:N0} | Σ SELL {inSell:N0}";
            lblOutFooter.Text = $"Σ BUY {outBuy:N0} | Σ SELL {outSell:N0}";

            // дубль в нижних панелях (если полезно)
            lblInputsTotal.Text = lblInFooter.Text.Replace("Σ ", "");
            lblOutputsTotal.Text = lblOutFooter.Text.Replace("Σ ", ""); // без «Σ» в нижних
        }

        private void RebuildTextAreas()
        {
            var left = new System.Text.StringBuilder();
            foreach (var i in _inputs.OrderBy(x => x.Name))
                left.AppendLine($"{i.Name}\t{FmtQty(i.Qty)}");
            txtLeft.Text = left.ToString();

            var right = new System.Text.StringBuilder();
            foreach (var o in _outputs.OrderBy(x => x.Name))
                right.AppendLine($"{o.Name}\t{FmtQty(o.Qty)}");
            txtRight.Text = right.ToString();
        }

        private static string FmtQty(double v)
        {
            // компактно: без разделителей тысяч, до 3 знаков после запятой, без лишних нулей
            return v.ToString("0.###", CultureInfo.InvariantCulture);
        }

        // ----- безопасная установка SplitterDistance -----
        private void SafeInitSplitters()
        {
            SafeSetSplitterDistance(mainSplit);
            SafeSetSplitterDistance(splitTop);
            SafeSetSplitterDistance(splitBottom);
        }

        private void SafeSetSplitterDistance(SplitContainer s)
        {
            if (s == null || !s.IsHandleCreated) return;

            int extent = (s.Orientation == Orientation.Vertical) ? s.ClientSize.Width : s.ClientSize.Height;
            if (extent <= 0) return;

            int min1 = Math.Max(0, s.Panel1MinSize);
            int min2 = Math.Max(0, s.Panel2MinSize);

            int target = extent / 2;
            int minAllowed = Math.Min(extent, min1);
            int maxAllowed = Math.Max(0, extent - min2);

            if (target < minAllowed) target = minAllowed;
            if (target > maxAllowed) target = maxAllowed;

            try { s.SplitterDistance = target; }
            catch
            {
                s.Panel1MinSize = 0;
                s.Panel2MinSize = 0;
                int safe = Math.Max(0, extent / 2);
                try { s.SplitterDistance = safe; } catch { /* ignore */ }
            }
        }

        // ===== распаковка payload =====

        private static Header ExtractHeader(object payload)
        {
            return new Header
            {
                ProductTypeID = (int)ToInt(GetProp(payload, "ProductTypeID")),
                Name = Convert.ToString(GetProp(payload, "Name")) ?? "",
                UnitsPerCycle = ToDouble(GetProp(payload, "UnitsPerCycle")),
                CycleSeconds = ToDouble(GetProp(payload, "CycleSecondsEff"))
            };
        }

        private static IEnumerable<LineVM> ExtractLines(object rawList)
        {
            var list = new List<LineVM>();
            if (rawList == null) return list;

            var en = rawList as System.Collections.IEnumerable;
            if (en == null) return list;

            foreach (var it in en)
            {
                list.Add(new LineVM
                {
                    TypeID = (int)ToInt(GetProp(it, "TypeID")),
                    Name = Convert.ToString(GetProp(it, "Name")) ?? "",
                    Qty = ToDouble(GetProp(it, "Quantity"))
                });
            }
            return list;
        }

        private static object GetProp(object obj, string name)
        {
            if (obj == null) return null;
            var p = obj.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
            return p != null ? p.GetValue(obj) : null;
        }

        private static double ToDouble(object v)
        {
            if (v == null) return 0;
            if (v is double d) return d;
            if (v is float f) return f;
            if (v is decimal m) return (double)m;
            if (v is int i) return i;
            if (v is long l) return l;
            double res;
            if (double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out res))
                return res;
            return 0;
        }

        private static long ToInt(object v)
        {
            if (v == null) return 0;
            if (v is int i) return i;
            if (v is long l) return l;
            long res;
            if (long.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Any, CultureInfo.InvariantCulture, out res))
                return res;
            return 0;
        }
    }
}


namespace TestForm
{
    partial class MoonItemDetailsForm
    {
        private IContainer components = null;

        private Label lblHeader;

        private SplitContainer mainSplit;   // горизонтальный: сверху таблицы, снизу текст
        private SplitContainer splitTop;    // вертикальный: gridIn | gridOut
        private SplitContainer splitBottom; // вертикальный: левый текст | правый текст

        private Panel panelInTop;
        private DataGridView gridIn;
        private Panel panelInFooter;
        private Label lblInFooter;

        private Panel panelOutTop;
        private DataGridView gridOut;
        private Panel panelOutFooter;
        private Label lblOutFooter;

        private Label lblInputsTotal;
        private TextBox txtLeft;

        private Label lblOutputsTotal;
        private TextBox txtRight;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new Container();

            // ---- controls ----
            lblHeader = new Label();

            mainSplit = new SplitContainer();
            splitTop = new SplitContainer();
            splitBottom = new SplitContainer();

            panelInTop = new Panel();
            gridIn = new DataGridView();
            panelInFooter = new Panel();
            lblInFooter = new Label();

            panelOutTop = new Panel();
            gridOut = new DataGridView();
            panelOutFooter = new Panel();
            lblOutFooter = new Label();

            lblInputsTotal = new Label();
            txtLeft = new TextBox();

            lblOutputsTotal = new Label();
            txtRight = new TextBox();

            // ---- form ----
            SuspendLayout();
            this.Text = "Moon Item Details";
            this.ClientSize = new Size(1100, 720);
            this.StartPosition = FormStartPosition.CenterParent;
            this.MinimumSize = new Size(900, 600);

            // ---- lblHeader (top caption) ----
            lblHeader.Dock = DockStyle.Top;
            lblHeader.Height = 26;
            lblHeader.TextAlign = ContentAlignment.MiddleLeft;
            lblHeader.Padding = new Padding(8, 0, 8, 0);
            lblHeader.Text = "детали";
            lblHeader.ForeColor = Color.Gainsboro;
            lblHeader.BackColor = Color.FromArgb(32, 32, 34);

            // ---- mainSplit (H) ----
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.Orientation = Orientation.Horizontal;
            mainSplit.SplitterWidth = 5;
            mainSplit.Panel1MinSize = 0;
            mainSplit.Panel2MinSize = 0;

            // ---- splitTop (V) : grids ----
            splitTop.Dock = DockStyle.Fill;
            splitTop.Orientation = Orientation.Vertical;
            splitTop.SplitterWidth = 5;
            splitTop.Panel1MinSize = 0;
            splitTop.Panel2MinSize = 0;

            // ---- left (inputs) panel with footer ----
            panelInTop.Dock = DockStyle.Fill;
            panelInTop.Padding = new Padding(0);
            panelInTop.BackColor = Color.FromArgb(32, 32, 34);

            gridIn.Dock = DockStyle.Fill;
            gridIn.ReadOnly = true;
            gridIn.AllowUserToAddRows = false;
            gridIn.AllowUserToDeleteRows = false;
            gridIn.AllowUserToResizeRows = false;
            gridIn.RowHeadersVisible = false;
            gridIn.MultiSelect = false;
            gridIn.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridIn.AutoGenerateColumns = false;

            panelInFooter.Dock = DockStyle.Bottom;
            panelInFooter.Height = 22;
            panelInFooter.BackColor = Color.FromArgb(28, 28, 30);
            lblInFooter.Dock = DockStyle.Fill;
            lblInFooter.TextAlign = ContentAlignment.MiddleLeft;
            lblInFooter.Padding = new Padding(6, 0, 6, 0);
            lblInFooter.Text = "Σ BUY 0 | Σ SELL 0";
            lblInFooter.ForeColor = Color.Gold;

            panelInFooter.Controls.Add(lblInFooter);
            panelInTop.Controls.Add(gridIn);
            panelInTop.Controls.Add(panelInFooter);

            // ---- right (outputs) panel with footer ----
            panelOutTop.Dock = DockStyle.Fill;
            panelOutTop.Padding = new Padding(0);
            panelOutTop.BackColor = Color.FromArgb(32, 32, 34);

            gridOut.Dock = DockStyle.Fill;
            gridOut.ReadOnly = true;
            gridOut.AllowUserToAddRows = false;
            gridOut.AllowUserToDeleteRows = false;
            gridOut.AllowUserToResizeRows = false;
            gridOut.RowHeadersVisible = false;
            gridOut.MultiSelect = false;
            gridOut.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridOut.AutoGenerateColumns = false;

            panelOutFooter.Dock = DockStyle.Bottom;
            panelOutFooter.Height = 22;
            panelOutFooter.BackColor = Color.FromArgb(28, 28, 30);
            lblOutFooter.Dock = DockStyle.Fill;
            lblOutFooter.TextAlign = ContentAlignment.MiddleLeft;
            lblOutFooter.Padding = new Padding(6, 0, 6, 0);
            lblOutFooter.Text = "Σ BUY 0 | Σ SELL 0";
            lblOutFooter.ForeColor = Color.Gold;

            panelOutFooter.Controls.Add(lblOutFooter);
            panelOutTop.Controls.Add(gridOut);
            panelOutTop.Controls.Add(panelOutFooter);

            splitTop.Panel1.Controls.Add(panelInTop);
            splitTop.Panel2.Controls.Add(panelOutTop);

            // ---- splitBottom (V) : text summaries ----
            splitBottom.Dock = DockStyle.Fill;
            splitBottom.Orientation = Orientation.Vertical;
            splitBottom.SplitterWidth = 5;
            splitBottom.Panel1MinSize = 0;
            splitBottom.Panel2MinSize = 0;

            // ---- Left panel: inputs total + text ----
            var panelLeft = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 34) };
            lblInputsTotal.Dock = DockStyle.Top;
            lblInputsTotal.Height = 22;
            lblInputsTotal.TextAlign = ContentAlignment.MiddleLeft;
            lblInputsTotal.Padding = new Padding(6, 0, 6, 0);
            lblInputsTotal.Text = "ИТОГО входы";
            lblInputsTotal.ForeColor = Color.Gainsboro;

            txtLeft.Dock = DockStyle.Fill;
            txtLeft.Multiline = true;
            txtLeft.ReadOnly = true;
            txtLeft.ScrollBars = ScrollBars.Vertical;
            txtLeft.BorderStyle = BorderStyle.FixedSingle;
            txtLeft.BackColor = Color.FromArgb(30, 30, 30);
            txtLeft.ForeColor = Color.Gainsboro;

            panelLeft.Controls.Add(txtLeft);
            panelLeft.Controls.Add(lblInputsTotal);

            // ---- Right panel: outputs total + text ----
            var panelRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(32, 32, 34) };
            lblOutputsTotal.Dock = DockStyle.Top;
            lblOutputsTotal.Height = 22;
            lblOutputsTotal.TextAlign = ContentAlignment.MiddleLeft;
            lblOutputsTotal.Padding = new Padding(6, 0, 6, 0);
            lblOutputsTotal.Text = "ИТОГО выходы";
            lblOutputsTotal.ForeColor = Color.Gainsboro;

            txtRight.Dock = DockStyle.Fill;
            txtRight.Multiline = true;
            txtRight.ReadOnly = true;
            txtRight.ScrollBars = ScrollBars.Vertical;
            txtRight.BorderStyle = BorderStyle.FixedSingle;
            txtRight.BackColor = Color.FromArgb(30, 30, 30);
            txtRight.ForeColor = Color.Gainsboro;

            panelRight.Controls.Add(txtRight);
            panelRight.Controls.Add(lblOutputsTotal);

            splitBottom.Panel1.Controls.Add(panelLeft);
            splitBottom.Panel2.Controls.Add(panelRight);

            // ---- assemble ----
            mainSplit.Panel1.Controls.Add(splitTop);
            mainSplit.Panel2.Controls.Add(splitBottom);

            this.Controls.Add(mainSplit);
            this.Controls.Add(lblHeader);

            ResumeLayout(false);
        }
    }
}
