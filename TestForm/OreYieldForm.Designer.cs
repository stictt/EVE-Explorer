using System.Windows.Forms;
using System.Drawing;

namespace TestForm
{
    partial class OreYieldForm
    {
        private System.ComponentModel.IContainer components = null;
        private NumericUpDown numShips;
        private NumericUpDown numStrips;
        private NumericUpDown numM3PerStrip;
        private NumericUpDown numCycleSec;
        private NumericUpDown numIceCycleSec;
        private NumericUpDown numRefineYield;
        private CheckBox chkIncludeCompressed;
        private Button btnCalc;
        private DataGridView grid;
        private StatusStrip status;
        private ToolStripStatusLabel statusLabel;
        private ToolStripProgressBar progress;
        private Label lblShips;
        private Label lblStrips;
        private Label lblM3Ore;
        private Label lblCycleOre;
        private Label lblCycleIce;
        private Label lblYield;
        private Label lblRegion;
        private ComboBox cmbRegion;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            Text = "Ore Yield (ISK/hour)";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(1200, 660);

            lblShips = new Label { Text = "Кораблей:", AutoSize = true, Location = new Point(12, 14) };
            numShips = new NumericUpDown { Location = new Point(120, 10), Minimum = 1, Maximum = 1000, Value = 1, Width = 80 };

            lblStrips = new Label { Text = "Стрип-майнеров:", AutoSize = true, Location = new Point(210, 14) };
            numStrips = new NumericUpDown { Location = new Point(330, 10), Minimum = 1, Maximum = 8, Value = 2, Width = 60 };

            lblM3Ore = new Label { Text = "м³/цикл (руда) на 1 стрип:", AutoSize = true, Location = new Point(400, 14) };
            numM3PerStrip = new NumericUpDown { Location = new Point(570, 10), Minimum = 1, Maximum = 100000, Value = 4000, Increment = 10, Width = 100 };

            lblCycleOre = new Label { Text = "Время цикла руды (сек):", AutoSize = true, Location = new Point(680, 14) };
            numCycleSec = new NumericUpDown { Location = new Point(850, 10), Minimum = 1, Maximum = 3600, Value = 90, Width = 70 };

            lblCycleIce = new Label { Text = "Время цикла льда (сек):", AutoSize = true, Location = new Point(930, 14) };
            numIceCycleSec = new NumericUpDown { Location = new Point(1095, 10), Minimum = 1, Maximum = 3600, Value = 200, Width = 70 };

            lblYield = new Label { Text = "Коэфф. рефайна (%):", AutoSize = true, Location = new Point(12, 46) };
            numRefineYield = new NumericUpDown { Location = new Point(150, 42), Minimum = 0, Maximum = 100, DecimalPlaces = 0, Value = 100, Width = 80 };
            var tip = new ToolTip(components);
            tip.SetToolTip(numRefineYield, "100 = идеальный рефайн. Поставь своё значение, напр. 73.");

            lblRegion = new Label { Text = "Регион:", AutoSize = true, Location = new Point(240, 46) };
            cmbRegion = new ComboBox { Location = new Point(300, 42), Width = 200, DropDownStyle = ComboBoxStyle.DropDownList };
            cmbRegion.Items.Add("The Forge (10000002)");
            cmbRegion.SelectedIndex = 0;

            chkIncludeCompressed = new CheckBox { Text = "Включать сжатые (руда/лёд)", AutoSize = true, Location = new Point(520, 44) };

            btnCalc = new Button { Text = "Рассчитать", Location = new Point(1095, 42), Width = 120, Height = 26 };
            btnCalc.Click += btnCalc_Click;

            grid = new DataGridView
            {
                Location = new Point(12, 80),
                Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                ReadOnly = true,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                Size = new Size(1200 - 24, 520)
            };

            // Колонки
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TypeID", HeaderText = "TypeID", Width = 80 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "TypeLabel", HeaderText = "Тип", Width = 60 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Name", HeaderText = "Наименование", Width = 220 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UnitVolume", HeaderText = "м³/ед.", Width = 70, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UnitsPerHour", HeaderText = "ед./час", Width = 100, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });

            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RawBuyIskPerHour", HeaderText = "Raw BUY ISK/час", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RawSellIskPerHour", HeaderText = "Raw SELL ISK/час", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RefinedBuyIskPerHour", HeaderText = "Refined BUY ISK/час", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "RefinedSellIskPerHour", HeaderText = "Refined SELL ISK/час", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "Avg30dIskPerHour", HeaderText = "Avg30d ISK/час (сыр.)", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });

            // Компрессия
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompressedTypeID", HeaderText = "CompressedType", Width = 110 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompressedName", HeaderText = "CompressedName", Width = 180 });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "UnitsPerCompressed", HeaderText = "UnitsPerCompressed", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompressedBuyIskPerHour", HeaderText = "CompressedBUY/час", Width = 140, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });
            grid.Columns.Add(new DataGridViewTextBoxColumn { DataPropertyName = "CompressedSellIskPerHour", HeaderText = "CompressedSELL/час", Width = 150, DefaultCellStyle = new DataGridViewCellStyle { Format = "N0" } });

            status = new StatusStrip();
            statusLabel = new ToolStripStatusLabel { Text = "Готово" };
            progress = new ToolStripProgressBar { Minimum = 0, Maximum = 100, Value = 0, Width = 200 };
            status.Items.Add(statusLabel);
            status.Items.Add(new ToolStripStatusLabel { Spring = true });
            status.Items.Add(progress);

            Controls.Add(lblShips);
            Controls.Add(numShips);
            Controls.Add(lblStrips);
            Controls.Add(numStrips);
            Controls.Add(lblM3Ore);
            Controls.Add(numM3PerStrip);
            Controls.Add(lblCycleOre);
            Controls.Add(numCycleSec);
            Controls.Add(lblCycleIce);
            Controls.Add(numIceCycleSec);
            Controls.Add(lblYield);
            Controls.Add(numRefineYield);
            Controls.Add(lblRegion);
            Controls.Add(cmbRegion);
            Controls.Add(chkIncludeCompressed);
            Controls.Add(btnCalc);
            Controls.Add(grid);
            Controls.Add(status);
        }
    }
}
