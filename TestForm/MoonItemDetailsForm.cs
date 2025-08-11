using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using System.Collections;

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
            public double UnitPrice { get; set; }
            public double Sum { get { return Qty * UnitPrice; } }
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

        public MoonItemDetailsForm(object payload)
        {
            InitializeComponent();
            TryApplyDark();

            // безопасно распакуем анонимный payload через рефлексию
            var head = ExtractHeader(payload);
            var ins = ExtractLines(GetProp(payload, "Inputs"));
            var outs = ExtractLines(GetProp(payload, "Outputs"));

            foreach (var i in ins) _inputs.Add(i);
            foreach (var o in outs) _outputs.Add(o);

            // шапка/итоги/биндинги
            Text = head.Name + " [" + head.ProductTypeID + "] — детали";
            lblHeader.Text = "шт/цикл: " + head.UnitsPerCycle.ToString("N0") + " • сек/цикл: " + head.CycleSeconds.ToString("N0");

            gridIn.DataSource = _inputs;
            gridOut.DataSource = _outputs;
            BuildColumns(gridIn);
            BuildColumns(gridOut);

            RecalcTotalsAndText(head);

            // адекватная ширина панелей снизу
            this.Shown += (_, __) => SafeSetSplitterDistance();
            splitBottom.SizeChanged += (_, __) => SafeSetSplitterDistance();
        }

        // аккуратно делим нижнюю область 50/50, соблюдая минимумы
        private void SafeSetSplitterDistance()
        {
            if (!splitBottom.IsHandleCreated) return;

            int w = splitBottom.ClientSize.Width;
            if (w <= 0) return;

            int p1 = Math.Max(120, splitBottom.Panel1MinSize);
            int p2 = Math.Max(120, splitBottom.Panel2MinSize);

            int min = p1;
            int max = Math.Max(p1, w - p2);

            int target = w / 2;
            if (target < min) target = min;
            if (target > max) target = max;

            try
            {
                splitBottom.SplitterDistance = target;
            }
            catch
            {
                splitBottom.Panel1MinSize = 0;
                splitBottom.Panel2MinSize = 0;
                splitBottom.SplitterDistance = w / 2;
            }
        }

        private void TryApplyDark()
        {
            try
            {
                // твой хелпер, если есть в проекте
                var t = Type.GetType("UiStyle");
                var m = t?.GetMethod("ApplyDark", BindingFlags.Public | BindingFlags.Static);
                if (m != null) m.Invoke(null, new object[] { this });
            }
            catch { /* необязательно */ }

            // подстройка цветов
            Color caption = Color.Gainsboro;
            lblHeader.ForeColor = caption;
            lblInputsTotal.ForeColor = caption;
            lblOutputsTotal.ForeColor = caption;

            gridIn.EnableHeadersVisualStyles = false;
            gridOut.EnableHeadersVisualStyles = false;
            gridIn.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
            gridOut.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
            gridIn.ColumnHeadersDefaultCellStyle.ForeColor = caption;
            gridOut.ColumnHeadersDefaultCellStyle.ForeColor = caption;
        }

        private void BuildColumns(DataGridView g)
        {
            g.AutoGenerateColumns = false;
            g.Columns.Clear();

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "TypeID",
                HeaderText = "TypeID",
                Width = 80,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Name",
                HeaderText = "Название",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Qty",
                HeaderText = "Кол-во",
                Width = 110,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N3"
                }
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "UnitPrice",
                HeaderText = "Цена/шт",
                Width = 110,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N0"
                }
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Sum",
                HeaderText = "Сумма",
                Width = 120,
                SortMode = DataGridViewColumnSortMode.Programmatic,
                DefaultCellStyle = new DataGridViewCellStyle
                {
                    Alignment = DataGridViewContentAlignment.MiddleRight,
                    Format = "N0"
                }
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
            IEnumerable<LineVM> seq = list;

            bool asc = col.HeaderCell.SortGlyphDirection != SortOrder.Ascending;
            seq = asc ? list.OrderBy(x => GetPropValue(x, prop))
                      : list.OrderByDescending(x => GetPropValue(x, prop));

            g.DataSource = new BindingList<LineVM>(seq.ToList());
            foreach (DataGridViewColumn c in g.Columns) c.HeaderCell.SortGlyphDirection = SortOrder.None;
            col.HeaderCell.SortGlyphDirection = asc ? SortOrder.Ascending : SortOrder.Descending;
        }

        private static object GetPropValue(object obj, string prop)
        {
            var p = obj.GetType().GetProperty(prop, BindingFlags.Public | BindingFlags.Instance);
            return p != null ? (p.GetValue(obj) ?? 0) : 0;
        }

        private void RecalcTotalsAndText(Header head)
        {
            double inSum = _inputs.Sum(x => x.Sum);
            double outSum = _outputs.Sum(x => x.Sum);

            lblInputsTotal.Text = "ИТОГО входы: " + inSum.ToString("N0", CultureInfo.InvariantCulture);
            lblOutputsTotal.Text = "ИТОГО выходы: " + outSum.ToString("N0", CultureInfo.InvariantCulture);

            // текстовые панели – без грубого округления, формат N3
            var left = new System.Text.StringBuilder();
            foreach (var i in _inputs.OrderBy(x => x.Name))
                left.AppendLine(i.Name + "\t" + i.Qty.ToString("N3", CultureInfo.InvariantCulture));
            txtLeft.Text = left.ToString();

            var right = new System.Text.StringBuilder();
            foreach (var o in _outputs.OrderBy(x => x.Name))
                right.AppendLine(o.Name + "\t" + o.Qty.ToString("N3", CultureInfo.InvariantCulture));
            txtRight.Text = right.ToString();
        }

        // ===== распаковка payload (анонимный объект из MoonIndustryForm) =====

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

            // поддерживаем: IEnumerable анонимных объектов с полями TypeID/Name/Quantity/UnitPrice/Sum
            var en = rawList as System.Collections.IEnumerable;
            if (en == null) return list;

            foreach (var it in en)
            {
                list.Add(new LineVM
                {
                    TypeID = (int)ToInt(GetProp(it, "TypeID")),
                    Name = Convert.ToString(GetProp(it, "Name")) ?? "",
                    Qty = ToDouble(GetProp(it, "Quantity")),
                    UnitPrice = ToDouble(GetProp(it, "UnitPrice"))
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

        private DataGridView gridIn;
        private DataGridView gridOut;

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

            gridIn = new DataGridView();
            gridOut = new DataGridView();

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

            // ---- mainSplit (H) ----
            mainSplit.Dock = DockStyle.Fill;
            mainSplit.Orientation = Orientation.Horizontal;
            mainSplit.SplitterWidth = 5;
            mainSplit.Panel1MinSize = 200;
            mainSplit.Panel2MinSize = 200;
            mainSplit.SplitterDistance = 360;

            // ---- splitTop (V) : grids ----
            splitTop.Dock = DockStyle.Fill;
            splitTop.Orientation = Orientation.Vertical;
            splitTop.SplitterWidth = 5;
            splitTop.Panel1MinSize = 220;
            splitTop.Panel2MinSize = 220;
            splitTop.SplitterDistance = this.ClientSize.Width / 2;

            // ---- gridIn ----
            gridIn.Dock = DockStyle.Fill;
            gridIn.ReadOnly = true;
            gridIn.AllowUserToAddRows = false;
            gridIn.AllowUserToDeleteRows = false;
            gridIn.AllowUserToResizeRows = false;
            gridIn.RowHeadersVisible = false;
            gridIn.MultiSelect = false;
            gridIn.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridIn.AutoGenerateColumns = false;

            // ---- gridOut ----
            gridOut.Dock = DockStyle.Fill;
            gridOut.ReadOnly = true;
            gridOut.AllowUserToAddRows = false;
            gridOut.AllowUserToDeleteRows = false;
            gridOut.AllowUserToResizeRows = false;
            gridOut.RowHeadersVisible = false;
            gridOut.MultiSelect = false;
            gridOut.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridOut.AutoGenerateColumns = false;

            splitTop.Panel1.Controls.Add(gridIn);
            splitTop.Panel2.Controls.Add(gridOut);

            // ---- splitBottom (V) : text summaries ----
            splitBottom.Dock = DockStyle.Fill;
            splitBottom.Orientation = Orientation.Vertical;
            splitBottom.SplitterWidth = 5;
            splitBottom.Panel1MinSize = 200;
            splitBottom.Panel2MinSize = 200;
            splitBottom.SplitterDistance = this.ClientSize.Width / 2;

            // ---- Left panel: inputs total + text ----
            var panelLeft = new Panel { Dock = DockStyle.Fill };
            lblInputsTotal.Dock = DockStyle.Top;
            lblInputsTotal.Height = 22;
            lblInputsTotal.TextAlign = ContentAlignment.MiddleLeft;
            lblInputsTotal.Padding = new Padding(6, 0, 6, 0);
            lblInputsTotal.Text = "ИТОГО входы: 0";

            txtLeft.Dock = DockStyle.Fill;
            txtLeft.Multiline = true;
            txtLeft.ReadOnly = true;
            txtLeft.ScrollBars = ScrollBars.Vertical;
            txtLeft.BorderStyle = BorderStyle.FixedSingle;

            panelLeft.Controls.Add(txtLeft);
            panelLeft.Controls.Add(lblInputsTotal);

            // ---- Right panel: outputs total + text ----
            var panelRight = new Panel { Dock = DockStyle.Fill };
            lblOutputsTotal.Dock = DockStyle.Top;
            lblOutputsTotal.Height = 22;
            lblOutputsTotal.TextAlign = ContentAlignment.MiddleLeft;
            lblOutputsTotal.Padding = new Padding(6, 0, 6, 0);
            lblOutputsTotal.Text = "ИТОГО выходы: 0";

            txtRight.Dock = DockStyle.Fill;
            txtRight.Multiline = true;
            txtRight.ReadOnly = true;
            txtRight.ScrollBars = ScrollBars.Vertical;
            txtRight.BorderStyle = BorderStyle.FixedSingle;

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