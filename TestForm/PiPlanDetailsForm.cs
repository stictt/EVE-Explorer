using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace TestForm.Infrastructure
{
    public sealed class PiPlanDetailsForm : Form
    {
        private readonly ProductionPlanResult _plan;
        private readonly ISdePiRepository? _repo;

        // Period tab controls
        private NumericUpDown _numDays = null!;
        private DataGridView _gridIn = null!;
        private DataGridView _gridOut = null!;
        private TextBox _txtIn = null!;
        private TextBox _txtOut = null!;

        private sealed class BomRow
        {
            public int TypeId { get; set; }
            public string Name { get; set; } = "";
            public int Tier { get; set; }
            public decimal QuantityPerHour { get; set; }
        }
        private sealed class TotalsRow
        {
            public int TypeId { get; set; }
            public string Name { get; set; } = "";
            public decimal QtyPerDay { get; set; }
            public decimal QtyPerPeriod { get; set; }
        }

        public PiPlanDetailsForm(ProductionPlanResult plan, ISdePiRepository? repo = null)
        {
            _plan = plan;
            _repo = repo;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = $"PI Plan Details — Product {_repo?.GetType(_plan.ProductTypeId)?.TypeName ?? _plan.ProductTypeId.ToString()}";
            Width = 1200;
            Height = 820;
            StartPosition = FormStartPosition.CenterParent;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            Controls.Add(tabs);

            // ===== TAB 1: BOM =====
            var tabBom = new TabPage("Inputs (BOM)");
            tabs.TabPages.Add(tabBom);
            var gridBom = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false
            };
            gridBom.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TypeID", DataPropertyName = "TypeId", Width = 90 });
            gridBom.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 260 });
            gridBom.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tier", DataPropertyName = "Tier", Width = 60 });
            gridBom.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Qty/hour",
                DataPropertyName = "QuantityPerHour",
                Width = 120,
                DefaultCellStyle = { Format = "N4" }
            });
            tabBom.Controls.Add(gridBom);

            var bomRows = _plan.RequiredInputs
                .OrderBy(x => x.Tier).ThenBy(x => x.TypeId)
                .Select(x => new BomRow
                {
                    TypeId = x.TypeId,
                    Name = _repo?.GetType(x.TypeId)?.TypeName ?? "",
                    Tier = x.Tier,
                    QuantityPerHour = x.QuantityPerHour
                })
                .ToList();
            gridBom.DataSource = bomRows;

            // ===== TAB 2: Recipe =====
            var tabTree = new TabPage("Recipe");
            tabs.TabPages.Add(tabTree);
            var tree = new TreeView { Dock = DockStyle.Fill };
            tabTree.Controls.Add(tree);

            var perTypeAgg = _plan.Planets
                .GroupBy(p => p.ProductTypeId ?? -1)
                .ToDictionary(g => g.Key, g =>
                {
                    int planets = g.Count();
                    var facs = g.First().Facilities;
                    string facText = string.Join(", ", facs.Select(kv => $"{kv.Key}:{kv.Value}"));
                    return (planets, facText);
                });

            void AddNode(TreeNode? parent, RecipeNode n)
            {
                var outName = _repo?.GetType(n.OutputTypeId)?.TypeName ?? n.OutputTypeId.ToString();
                perTypeAgg.TryGetValue(n.OutputTypeId, out var agg);
                string facTxt = agg.facText ?? "-";
                string planetsTxt = agg.planets > 0 ? $"planets:{agg.planets}, " : "";
                var nodeText = $"{outName} [{n.OutputTypeId}] (out {n.OutputQty} / {n.CycleTimeSeconds}s, {planetsTxt}facilities {facTxt})";
                var tn = parent == null ? tree.Nodes.Add(nodeText) : parent.Nodes.Add(nodeText);

                foreach (var i in n.Inputs)
                {
                    var inName = _repo?.GetType(i.InputTypeId)?.TypeName ?? i.InputTypeId.ToString();
                    var txt = $"{inName} [{i.InputTypeId}] x{i.InputQty}";
                    var child = tn.Nodes.Add(txt);
                    if (i.Node != null) AddNode(child, i.Node);
                }
            }
            if (_plan.Recipe != null) AddNode(null, _plan.Recipe);
            tree.ExpandAll();

            // ===== TAB 3: Planets =====
            var tabPlanets = new TabPage("Planets");
            tabs.TabPages.Add(tabPlanets);
            var gridPl = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false
            };
            gridPl.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Role", DataPropertyName = "Role", Width = 160 });
            gridPl.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ProductTypeId", DataPropertyName = "ProductTypeId", Width = 110 });
            gridPl.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ProductName", DataPropertyName = "ProductName", Width = 240 });
            gridPl.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Facilities", DataPropertyName = "FacilitiesText", Width = 300 });
            gridPl.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Out/h", DataPropertyName = "OutText", Width = 160 });
            gridPl.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "In/h", DataPropertyName = "InText", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            tabPlanets.Controls.Add(gridPl);

            var rows = _plan.Planets.Select(p => new
            {
                p.Role,
                p.ProductTypeId,
                ProductName = p.ProductTypeId.HasValue ? (_repo?.GetType(p.ProductTypeId.Value)?.TypeName ?? "") : "",
                FacilitiesText = string.Join(", ", p.Facilities.Select(kv => $"{kv.Key}:{kv.Value}")),
                OutText = string.Join(", ", p.OutputPerHour.Select(kv =>
                {
                    var nm = _repo?.GetType(kv.Key)?.TypeName ?? kv.Key.ToString();
                    return $"{nm} [{kv.Key}]:{kv.Value:N3}";
                })),
                InText = string.Join(", ", p.InputPerHour.Select(kv =>
                {
                    var nm = _repo?.GetType(kv.Key)?.TypeName ?? kv.Key.ToString();
                    return $"{nm} [{kv.Key}]:{kv.Value:N3}";
                }))
            }).ToList();
            gridPl.DataSource = rows;

            // ===== TAB 4: Period (перелэйаут) =====
            var tabPeriod = new TabPage("Period");
            tabs.TabPages.Add(tabPeriod);

            // Общий TableLayout: 2 колонки, 3 строки (Auto / 55% / 45%)
            var tl = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 3
            };
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tl.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tl.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // панель управления
            tl.RowStyles.Add(new RowStyle(SizeType.Percent, 55));    // две таблицы
            tl.RowStyles.Add(new RowStyle(SizeType.Percent, 45));    // два больших textbox
            tabPeriod.Controls.Add(tl);

            // Row 0: управляющая панель (растягивается на 2 колонки)
            var top = new FlowLayoutPanel { Dock = DockStyle.Fill, Height = 40, Padding = new Padding(8, 8, 8, 0) };
            tl.Controls.Add(top, 0, 0);
            tl.SetColumnSpan(top, 2);
            top.Controls.Add(new Label { Text = "Days:", AutoSize = true, Padding = new Padding(0, 6, 6, 0) });
            _numDays = new NumericUpDown { Minimum = 1, Maximum = 365, Value = 30, Width = 80 };
            top.Controls.Add(_numDays);
            var btnRecalc = new Button { Text = "Recalc", Width = 90, Height = 24, Margin = new Padding(8, 3, 0, 0) };
            top.Controls.Add(btnRecalc);

            // Row 1: две таблицы
            var grpIn = new GroupBox { Text = "Inputs", Dock = DockStyle.Fill };
            var grpOut = new GroupBox { Text = "Outputs", Dock = DockStyle.Fill };
            tl.Controls.Add(grpIn, 0, 1);
            tl.Controls.Add(grpOut, 1, 1);

            _gridIn = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false
            };
            _gridIn.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TypeID", DataPropertyName = "TypeId", Width = 90 });
            _gridIn.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 260 });
            _gridIn.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qty / Day", DataPropertyName = "QtyPerDay", Width = 120, DefaultCellStyle = { Format = "N3" } });
            _gridIn.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Qty / N days",
                DataPropertyName = "QtyPerPeriod",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DefaultCellStyle = { Format = "N3" }
            });
            grpIn.Controls.Add(_gridIn);

            _gridOut = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false
            };
            _gridOut.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TypeID", DataPropertyName = "TypeId", Width = 90 });
            _gridOut.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 260 });
            _gridOut.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qty / Day", DataPropertyName = "QtyPerDay", Width = 120, DefaultCellStyle = { Format = "N3" } });
            _gridOut.Columns.Add(new DataGridViewTextBoxColumn
            {
                HeaderText = "Qty / N days",
                DataPropertyName = "QtyPerPeriod",
                AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                DefaultCellStyle = { Format = "N3" }
            });
            grpOut.Controls.Add(_gridOut);

            // Row 2: два больших текстовых поля + Copy
            var pnlInText = new Panel { Dock = DockStyle.Fill };
            var pnlOutText = new Panel { Dock = DockStyle.Fill };
            tl.Controls.Add(pnlInText, 0, 2);
            tl.Controls.Add(pnlOutText, 1, 2);

            pnlInText.Controls.Add(new Label { Text = "Inputs (text):", Dock = DockStyle.Top, Height = 18 });
            _txtIn = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new System.Drawing.Font("Consolas", 10)
            };
            pnlInText.Controls.Add(_txtIn);
            var btnCopyIn = new Button { Text = "Copy", Dock = DockStyle.Bottom, Height = 26 };
            btnCopyIn.Click += (_, __) => { if (!string.IsNullOrWhiteSpace(_txtIn.Text)) Clipboard.SetText(_txtIn.Text.Trim()); };
            pnlInText.Controls.Add(btnCopyIn);

            pnlOutText.Controls.Add(new Label { Text = "Outputs (text):", Dock = DockStyle.Top, Height = 18 });
            _txtOut = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                WordWrap = false,
                Font = new System.Drawing.Font("Consolas", 10)
            };
            pnlOutText.Controls.Add(_txtOut);
            var btnCopyOut = new Button { Text = "Copy", Dock = DockStyle.Bottom, Height = 26 };
            btnCopyOut.Click += (_, __) => { if (!string.IsNullOrWhiteSpace(_txtOut.Text)) Clipboard.SetText(_txtOut.Text.Trim()); };
            pnlOutText.Controls.Add(btnCopyOut);

            // recalc
            btnRecalc.Click += (_, __) => RecalcPeriod();
            _numDays.ValueChanged += (_, __) => RecalcPeriod();
            Shown += (_, __) => RecalcPeriod();

            // ===== footer =====
            var panelBottom = new Panel { Dock = DockStyle.Bottom, Height = 36 };
            Controls.Add(panelBottom);
            var lblSummary = new Label
            {
                Dock = DockStyle.Fill,
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Text = $"Output/day: {_plan.OutputPerDay:N2} | Planets: {_plan.PlanetsUsed} | Note: {_plan.Note}"
            };
            panelBottom.Controls.Add(lblSummary);
        }

        private void RecalcPeriod()
        {
            int days = (int)_numDays.Value;
            if (days <= 0) days = 1;

            // 1) агрегируем по всем планетам
            var inPerDay = new Dictionary<int, decimal>();
            var outPerDay = new Dictionary<int, decimal>();

            foreach (var p in _plan.Planets)
            {
                foreach (var kv in p.InputPerHour)
                {
                    var perDay = kv.Value * 24m;
                    if (!inPerDay.TryGetValue(kv.Key, out var cur)) cur = 0m;
                    inPerDay[kv.Key] = cur + perDay;
                }
                foreach (var kv in p.OutputPerHour)
                {
                    var perDay = kv.Value * 24m;
                    if (!outPerDay.TryGetValue(kv.Key, out var cur)) cur = 0m;
                    outPerDay[kv.Key] = cur + perDay;
                }
            }

            // 2) страховка: гарантируем наличие непосредственных входов финального узла
            if (_plan.Recipe != null && _plan.Recipe.CycleTimeSeconds > 0)
            {
                var final = _plan.Recipe;
                var cyclesPerHour = 3600m / final.CycleTimeSeconds;
                var perFactoryOut = cyclesPerHour * final.OutputQty;
                var outPerHour = _plan.OutputPerDay / 24m;
                var scale = perFactoryOut > 0m ? (outPerHour / perFactoryOut) : 0m;

                foreach (var inp in final.Inputs)
                {
                    var perHour = cyclesPerHour * inp.InputQty * scale;
                    var perDay = perHour * 24m;
                    if (!inPerDay.ContainsKey(inp.InputTypeId))
                        inPerDay[inp.InputTypeId] = perDay;
                }
            }

            // 3) заполнение таблиц
            var rowsIn = inPerDay
                .OrderBy(kv => _repo?.GetType(kv.Key)?.TypeName ?? kv.Key.ToString())
                .Select(kv => new TotalsRow
                {
                    TypeId = kv.Key,
                    Name = _repo?.GetType(kv.Key)?.TypeName ?? "",
                    QtyPerDay = kv.Value,
                    QtyPerPeriod = kv.Value * days
                })
                .ToList();

            var rowsOut = outPerDay
                .OrderBy(kv => _repo?.GetType(kv.Key)?.TypeName ?? kv.Key.ToString())
                .Select(kv => new TotalsRow
                {
                    TypeId = kv.Key,
                    Name = _repo?.GetType(kv.Key)?.TypeName ?? "",
                    QtyPerDay = kv.Value,
                    QtyPerPeriod = kv.Value * days
                })
                .ToList();

            _gridIn.DataSource = rowsIn;
            _gridOut.DataSource = rowsOut;

            // 4) текст для копирования: "Name<TAB>QtyForNdays"
            static string Fmt(decimal v)
            {
                if (v == Math.Truncate(v)) return ((long)v).ToString("N0", CultureInfo.InvariantCulture);
                return v.ToString("N3", CultureInfo.InvariantCulture);
            }

            _txtIn.Text = string.Join(Environment.NewLine,
                rowsIn.Select(r => $"{(string.IsNullOrWhiteSpace(r.Name) ? r.TypeId.ToString() : r.Name)}\t{Fmt(r.QtyPerPeriod)}"));

            _txtOut.Text = string.Join(Environment.NewLine,
                rowsOut.Select(r => $"{(string.IsNullOrWhiteSpace(r.Name) ? r.TypeId.ToString() : r.Name)}\t{Fmt(r.QtyPerPeriod)}"));
        }
    }
}
