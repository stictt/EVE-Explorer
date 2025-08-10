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
            public double Sum => Qty * UnitPrice;
        }

        private sealed class Header
        {
            public int ProductTypeID { get; set; }
            public string Name { get; set; } = "";
            public double UnitsPerCycle { get; set; }
            public double CycleSeconds { get; set; }
        }

        private readonly BindingList<LineVM> _inputs = new();
        private readonly BindingList<LineVM> _outputs = new();

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
            Text = $"{head.Name} [{head.ProductTypeID}] — детали";
            lblHeader.Text = $"шт/цикл: {head.UnitsPerCycle:N0} • сек/цикл: {head.CycleSeconds:N0}";

            gridIn.DataSource = _inputs;
            gridOut.DataSource = _outputs;
            BuildColumns(gridIn);
            BuildColumns(gridOut);

            RecalcTotalsAndText(head);
        }
        private void SafeSetSplitterDistance()
        {
            // если контрол ещё не отрисован – смысла нет
            if (!splitBottom.IsHandleCreated) return;

            int w = splitBottom.ClientSize.Width;
            if (w <= 0) return;

            // возьмём реальные минимумы
            int p1 = Math.Max(120, splitBottom.Panel1MinSize);
            int p2 = Math.Max(120, splitBottom.Panel2MinSize);

            // допустимый диапазон
            int min = p1;
            int max = Math.Max(p1, w - p2);

            // желаем пополам
            int target = w / 2;
            if (target < min) target = min;
            if (target > max) target = max;

            try
            {
                splitBottom.SplitterDistance = target;
            }
            catch
            {
                // на крайний случай: временно ослабим минимумы
                splitBottom.Panel1MinSize = 0;
                splitBottom.Panel2MinSize = 0;
                splitBottom.SplitterDistance = w / 2;
            }
        }
        // MoonItemDetailsForm.cs  (добавьте внутрь класса)
        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);
            SafeSetSplitterDistance();
            // на всякий случай подстрахуемся при ресайзе
            this.Resize += (_, __) => SafeSetSplitterDistance();
        }
        // ========= helpers =========

        private void TryApplyDark()
        {
            try { UiStyle.ApplyDark(this); } catch { }

            // чуть контраста заголовкам
            lblHeader.ForeColor = Color.Gainsboro;
            lblInputsTotal.ForeColor = Color.Gainsboro;
            lblOutputsTotal.ForeColor = Color.Gainsboro;

            foreach (var g in new[] { gridIn, gridOut })
            {
                g.EnableHeadersVisualStyles = false;
                g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
                g.ColumnHeadersDefaultCellStyle.ForeColor = Color.Gainsboro;
            }
        }

        private static Header ExtractHeader(object obj)
        {
            return new Header
            {
                ProductTypeID = (int)(GetProp(obj, "ProductTypeID") ?? 0),
                Name = (string)(GetProp(obj, "Name") ?? ""),
                UnitsPerCycle = ToDouble(GetProp(obj, "UnitsPerCycle")),
                CycleSeconds = ToDouble(GetProp(obj, "CycleSecondsEff"))
            };
        }

        private static object? GetProp(object obj, string name)
            => obj?.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.Instance)?.GetValue(obj);

        private static double ToDouble(object? v)
        {
            if (v == null) return 0;
            if (v is double d) return d;
            if (v is float f) return f;
            if (v is decimal m) return (double)m;
            if (v is int i) return i;
            if (v is long l) return l;
            if (double.TryParse(Convert.ToString(v, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var x))
                return x;
            return 0;
        }

        /// <summary>
        /// Достаёт из obj (IEnumerable) список строк (TypeID, Name, Quantity, UnitPrice[, Sum]).
        /// Подходит для анонимных типов из MoonIndustryForm.
        /// </summary>
        private static List<LineVM> ExtractLines(object? obj)
        {
            var list = new List<LineVM>();
            if (obj is not IEnumerable en) return list;

            foreach (var it in en)
            {
                if (it == null) continue;

                int typeId = 0;
                string name = "";
                double qty = 0, unit = 0, sum = double.NaN;

                var t = it.GetType();
                var pType = t.GetProperty("TypeID");
                var pName = t.GetProperty("Name");
                var pQty = t.GetProperty("Quantity") ?? t.GetProperty("Qty");
                var pUnit = t.GetProperty("UnitPrice");
                var pSum = t.GetProperty("Sum");

                if (pType != null) typeId = Convert.ToInt32(pType.GetValue(it) ?? 0);
                if (pName != null) name = Convert.ToString(pName.GetValue(it) ?? "") ?? "";
                if (pQty != null) qty = ToDouble(pQty.GetValue(it));
                if (pUnit != null) unit = ToDouble(pUnit.GetValue(it));
                if (pSum != null) sum = ToDouble(pSum.GetValue(it));

                if (double.IsNaN(sum)) sum = qty * unit;
                if (string.IsNullOrWhiteSpace(name)) name = typeId.ToString();

                list.Add(new LineVM { TypeID = typeId, Name = name, Qty = qty, UnitPrice = unit });
            }
            return list;
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
                HeaderText = "Name",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Qty",
                HeaderText = "Qty",
                Width = 110,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" },
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "UnitPrice",
                HeaderText = "UnitPrice",
                Width = 120,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" },
                SortMode = DataGridViewColumnSortMode.Programmatic
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Sum",
                HeaderText = "Sum",
                Width = 140,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" },
                SortMode = DataGridViewColumnSortMode.Programmatic
            });

            g.ColumnHeaderMouseClick += (s, e) =>
            {
                var col = g.Columns[e.ColumnIndex];
                var prop = col.DataPropertyName;
                if (string.IsNullOrEmpty(prop)) return;

                var list = (BindingList<LineVM>)g.DataSource!;
                IEnumerable<LineVM> seq = list;

                // простая смена направления при повторном клике
                bool asc = col.HeaderCell.SortGlyphDirection != SortOrder.Ascending;
                seq = asc ? list.OrderBy(x => GetPropValue(x, prop))
                          : list.OrderByDescending(x => GetPropValue(x, prop));

                g.DataSource = new BindingList<LineVM>(seq.ToList());
                foreach (DataGridViewColumn c in g.Columns) c.HeaderCell.SortGlyphDirection = SortOrder.None;
                col.HeaderCell.SortGlyphDirection = asc ? SortOrder.Ascending : SortOrder.Descending;
            };
        }

        private static object? GetPropValue(object obj, string prop)
            => obj.GetType().GetProperty(prop)?.GetValue(obj);

        private void RecalcTotalsAndText(Header head)
        {
            double inQty = _inputs.Sum(x => x.Qty);
            double inSum = _inputs.Sum(x => x.Sum);
            lblInputsTotal.Text = $"ИТОГО входы: кол-во/цикл = {inQty:N0} • сумма = {inSum:N0}";

            double outQty = _outputs.Sum(x => x.Qty);
            double outSum = _outputs.Sum(x => x.Sum);
            lblOutputsTotal.Text = $"ИТОГО выходы: кол-во/цикл = {outQty:N0} • сумма = {outSum:N0}";

            // левое текстовое — базовые входы "Name<TAB>QtyInt"
            txtLeft.Clear();
            foreach (var i in _inputs)
                txtLeft.AppendText($"{i.Name}\t{Math.Round(i.Qty):N0}{Environment.NewLine}");

            // правое — выходы (по умолчанию их обычно один), но выводим все на всякий случай
            txtRight.Clear();
            foreach (var o in _outputs)
                txtRight.AppendText($"{o.Name}\t{Math.Round(o.Qty):N0}{Environment.NewLine}");
        }
    }
}




namespace TestForm
{
    partial class MoonItemDetailsForm
    {
        private System.ComponentModel.IContainer components = null;

        private DataGridView gridIn;
        private DataGridView gridOut;
        private Label lblHeader;
        private Label lblInputsTotal;
        private Label lblOutputsTotal;
        private SplitContainer splitBottom;
        private TextBox txtLeft;
        private TextBox txtRight;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            lblHeader = new Label();
            gridIn = new DataGridView();
            lblInputsTotal = new Label();
            gridOut = new DataGridView();
            lblOutputsTotal = new Label();
            splitBottom = new SplitContainer();
            txtLeft = new TextBox();
            txtRight = new TextBox();

            // ---- Form ----
            this.SuspendLayout();
            this.AutoScaleMode = AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1000, 720);
            this.MinimumSize = new System.Drawing.Size(900, 600);
            this.StartPosition = FormStartPosition.CenterParent;
            this.Text = "Детали — лунные/реакции";

            // ---- lblHeader ----
            lblHeader.Dock = DockStyle.Top;
            lblHeader.AutoSize = false;
            lblHeader.Height = 26;
            lblHeader.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblHeader.Padding = new Padding(8, 0, 0, 0);
            lblHeader.Text = "шт/цикл: — • сек/цикл: —";

            // ---- gridIn ----
            gridIn.Dock = DockStyle.Top;
            gridIn.Height = 210;
            gridIn.AllowUserToAddRows = false;
            gridIn.AllowUserToDeleteRows = false;
            gridIn.AllowUserToResizeRows = false;
            gridIn.RowHeadersVisible = false;
            gridIn.MultiSelect = false;
            gridIn.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridIn.AutoGenerateColumns = false;
            gridIn.ReadOnly = true;

            // ---- lblInputsTotal ----
            lblInputsTotal.Dock = DockStyle.Top;
            lblInputsTotal.AutoSize = false;
            lblInputsTotal.Height = 22;
            lblInputsTotal.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblInputsTotal.Padding = new Padding(8, 0, 0, 0);
            lblInputsTotal.Text = "ИТОГО входы: —";

            // ---- gridOut ----
            gridOut.Dock = DockStyle.Top;
            gridOut.Height = 210;
            gridOut.AllowUserToAddRows = false;
            gridOut.AllowUserToDeleteRows = false;
            gridOut.AllowUserToResizeRows = false;
            gridOut.RowHeadersVisible = false;
            gridOut.MultiSelect = false;
            gridOut.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            gridOut.AutoGenerateColumns = false;
            gridOut.ReadOnly = true;

            // ---- lblOutputsTotal ----
            lblOutputsTotal.Dock = DockStyle.Top;
            lblOutputsTotal.AutoSize = false;
            lblOutputsTotal.Height = 22;
            lblOutputsTotal.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            lblOutputsTotal.Padding = new Padding(8, 0, 0, 0);
            lblOutputsTotal.Text = "ИТОГО выходы: —";

            // ---- splitBottom ----
            splitBottom.Dock = DockStyle.Fill;
            splitBottom.Orientation = Orientation.Vertical;
            splitBottom.SplitterWidth = 6;
            splitBottom.FixedPanel = FixedPanel.None;
            splitBottom.Panel1MinSize = 120;
            splitBottom.Panel2MinSize = 120;

            // ---- txtLeft ----
            txtLeft.Dock = DockStyle.Fill;
            txtLeft.Multiline = true;
            txtLeft.ScrollBars = ScrollBars.Both;
            txtLeft.AcceptsTab = true;
            txtLeft.WordWrap = false;

            // ---- txtRight ----
            txtRight.Dock = DockStyle.Fill;
            txtRight.Multiline = true;
            txtRight.ScrollBars = ScrollBars.Both;
            txtRight.AcceptsTab = true;
            txtRight.WordWrap = false;

            // add textboxes to split panels
            splitBottom.Panel1.Controls.Add(txtLeft);
            splitBottom.Panel2.Controls.Add(txtRight);

            // order of adding matters (top-docked blocks first)
            this.Controls.Add(splitBottom);
            this.Controls.Add(lblOutputsTotal);
            this.Controls.Add(gridOut);
            this.Controls.Add(lblInputsTotal);
            this.Controls.Add(gridIn);
            this.Controls.Add(lblHeader);

            this.ResumeLayout(false);
        }
    }
}
