using System;
using System.Linq;
using System.Collections.Generic;
using System.Windows.Forms;

namespace TestForm.Infrastructure
{
    public sealed class PiPlanDetailsForm : Form
    {
        private readonly ProductionPlanResult _plan;
        private readonly ISdePiRepository? _repo;

        public PiPlanDetailsForm(ProductionPlanResult plan, ISdePiRepository? repo = null)
        {
            _plan = plan;
            _repo = repo;
            BuildUi();
        }

        private void BuildUi()
        {
            Text = $"PI Plan Details — Product {_repo?.GetType(_plan.ProductTypeId)?.TypeName ?? _plan.ProductTypeId.ToString()}";
            Width = 1100;
            Height = 780;
            StartPosition = FormStartPosition.CenterParent;

            var tabs = new TabControl { Dock = DockStyle.Fill };
            Controls.Add(tabs);

            // TAB 1: BOM (RequiredInputs)
            var tabBom = new TabPage("Inputs (BOM)");
            tabs.TabPages.Add(tabBom);

            var gridBom = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false
            };
            tabBom.Controls.Add(gridBom);

            gridBom.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TypeID", DataPropertyName = "TypeId", Width = 100 });
            gridBom.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Name", DataPropertyName = "Name", Width = 260 });
            gridBom.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Tier", DataPropertyName = "Tier", Width = 60 });
            gridBom.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Qty/hour", DataPropertyName = "QuantityPerHour", Width = 120, DefaultCellStyle = { Format = "N4" } });

            var bomRows = _plan.RequiredInputs
                .OrderBy(x => x.Tier).ThenBy(x => x.TypeId)
                .Select(x => new
                {
                    x.TypeId,
                    Name = _repo?.GetType(x.TypeId)?.TypeName ?? "",
                    x.Tier,
                    x.QuantityPerHour
                })
                .ToList();
            gridBom.DataSource = bomRows;

            // ===== агрегаты по планетам для аннотации в Recipe =====
            var perTypeAgg = _plan.Planets
                .GroupBy(p => p.ProductTypeId ?? -1)
                .ToDictionary(g => g.Key, g =>
                {
                    int planets = g.Count();
                    // предполагаем одинаковый набор фасилити для одного typeId — берём первый
                    var facs = g.First().Facilities;
                    string facText = string.Join(", ", facs.Select(kv => $"{kv.Key}:{kv.Value}"));
                    return (planets, facText);
                });

            // TAB 2: Recipe
            var tabTree = new TabPage("Recipe");
            tabs.TabPages.Add(tabTree);

            var tree = new TreeView { Dock = DockStyle.Fill };
            tabTree.Controls.Add(tree);

            void AddNode(TreeNode? parent, RecipeNode n)
            {
                var outName = _repo?.GetType(n.OutputTypeId)?.TypeName ?? n.OutputTypeId.ToString();

                perTypeAgg.TryGetValue(n.OutputTypeId, out var agg);
                var facilityText = agg.facText ?? "-";
                var planetsText = agg.planets > 0 ? $"planets:{agg.planets}, " : "";

                var nodeText = $"{outName} [{n.OutputTypeId}] (out {n.OutputQty} / {n.CycleTimeSeconds}s, {planetsText}facilities {facilityText})";
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

            // TAB 3: Planets
            var tabPlanets = new TabPage("Planets");
            tabs.TabPages.Add(tabPlanets);

            var gridPl = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false
            };
            tabPlanets.Controls.Add(gridPl);

            gridPl.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Role", DataPropertyName = "Role", Width = 180 });
            gridPl.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ProductTypeId", DataPropertyName = "ProductTypeId", Width = 120 });
            gridPl.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ProductName", DataPropertyName = "ProductName", Width = 240 });
            gridPl.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Facilities", DataPropertyName = "FacilitiesText", Width = 300 });
            gridPl.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Out/h", DataPropertyName = "OutText", Width = 150 });
            gridPl.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "In/h", DataPropertyName = "InText", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });

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

            // Footer — summary
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
    }
}
