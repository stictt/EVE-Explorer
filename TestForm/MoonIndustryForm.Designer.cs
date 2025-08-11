using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace TestForm
{
    partial class MoonIndustryForm
    {
        private IContainer components = null;

        private Panel panelTop;
        private GroupBox gbMode;
        private RadioButton rbModeOre;
        private RadioButton rbModeReactions;
        private RadioButton rbModeBlueprints;

        private GroupBox gbOre;
        private CheckBox chkUseOre;
        private Label lblMinOreRatingBn;
        private NumericUpDown numMinOreRatingBn;
        private Label lblMinOreHint;

        private GroupBox gbRefine;
        private Label lblRefine;
        private NumericUpDown numRefine;

        private GroupBox gbSales;
        private Label lblSalesTax;
        private NumericUpDown numSalesTax;

        private GroupBox gbReact;
        private Label lblReactME;
        private NumericUpDown numReactME;
        private Label lblReactTE;
        private NumericUpDown numReactTE;

        private GroupBox gbBpo;
        private Label lblBpoME;
        private NumericUpDown numBpoME;
        private Label lblBpoTE;
        private NumericUpDown numBpoTE;

        private Button btnCalc;
        private DataGridView grid;
        private ProgressBar progress;
        private StatusStrip statusStrip;
        private ToolStripStatusLabel statusLabel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            components = new Container();

            panelTop = new Panel();
            gbMode = new GroupBox();
            rbModeOre = new RadioButton();
            rbModeReactions = new RadioButton();
            rbModeBlueprints = new RadioButton();

            gbOre = new GroupBox();
            chkUseOre = new CheckBox();
            lblMinOreRatingBn = new Label();
            numMinOreRatingBn = new NumericUpDown();
            lblMinOreHint = new Label();

            gbRefine = new GroupBox();
            lblRefine = new Label();
            numRefine = new NumericUpDown();

            gbSales = new GroupBox();
            lblSalesTax = new Label();
            numSalesTax = new NumericUpDown();

            gbReact = new GroupBox();
            lblReactME = new Label();
            numReactME = new NumericUpDown();
            lblReactTE = new Label();
            numReactTE = new NumericUpDown();

            gbBpo = new GroupBox();
            lblBpoME = new Label();
            numBpoME = new NumericUpDown();
            lblBpoTE = new Label();
            numBpoTE = new NumericUpDown();

            btnCalc = new Button();
            grid = new DataGridView();
            progress = new ProgressBar();
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();

            // ---- form ----
            this.SuspendLayout();
            this.Text = "Moon Industry";
            this.ClientSize = new Size(1280, 800);

            // ---- panelTop ----
            panelTop.Dock = DockStyle.Top;
            panelTop.Height = 160;
            panelTop.Padding = new Padding(6);

            // ---- gbMode ----
            gbMode.Text = "Режим";
            gbMode.Width = 250;
            gbMode.Height = 70;
            gbMode.Left = 8;
            gbMode.Top = 8;

            rbModeOre.Text = "Руда → материалы";
            rbModeOre.Left = 12;
            rbModeOre.Top = 20;
            rbModeOre.AutoSize = true;

            rbModeReactions.Text = "Конечные реакции";
            rbModeReactions.Left = 12;
            rbModeReactions.Top = 40;
            rbModeReactions.AutoSize = true;

            rbModeBlueprints.Text = "Чертежи (BPO/BPC)";
            rbModeBlueprints.Left = 120;
            rbModeBlueprints.Top = 40;
            rbModeBlueprints.AutoSize = true;

            gbMode.Controls.Add(rbModeOre);
            gbMode.Controls.Add(rbModeReactions);
            gbMode.Controls.Add(rbModeBlueprints);

            // ---- gbOre ----
            gbOre.Text = "Руда/ликвидность";
            gbOre.Width = 360;
            gbOre.Height = 70;
            gbOre.Left = gbMode.Right + 10;
            gbOre.Top = 8;

            chkUseOre.Text = "Только ликвидная руда";
            chkUseOre.Left = 12;
            chkUseOre.Top = 20;
            chkUseOre.AutoSize = true;

            lblMinOreRatingBn.Text = "мин. рейтинг (млрд):";
            lblMinOreRatingBn.Left = 12;
            lblMinOreRatingBn.Top = 42;
            lblMinOreRatingBn.AutoSize = true;

            numMinOreRatingBn.Left = 140;
            numMinOreRatingBn.Top = 38;
            numMinOreRatingBn.Width = 80;
            numMinOreRatingBn.DecimalPlaces = 2;
            numMinOreRatingBn.Maximum = 1000000;
            numMinOreRatingBn.Minimum = 0;

            lblMinOreHint.Text = "оценка ликвидности руды";
            lblMinOreHint.Left = 225;
            lblMinOreHint.Top = 42;
            lblMinOreHint.AutoSize = true;

            gbOre.Controls.Add(chkUseOre);
            gbOre.Controls.Add(lblMinOreRatingBn);
            gbOre.Controls.Add(numMinOreRatingBn);
            gbOre.Controls.Add(lblMinOreHint);

            // ---- gbRefine ----
            gbRefine.Text = "Рефайн";
            gbRefine.Width = 160;
            gbRefine.Height = 70;
            gbRefine.Left = gbOre.Right + 10;
            gbRefine.Top = 8;

            lblRefine.Text = "Выход, %:";
            lblRefine.Left = 12;
            lblRefine.Top = 20;
            lblRefine.AutoSize = true;

            numRefine.Left = 80;
            numRefine.Top = 18;
            numRefine.Width = 60;
            numRefine.Maximum = 100;
            numRefine.Minimum = 0;
            numRefine.DecimalPlaces = 2;
            numRefine.Value = 50;

            gbRefine.Controls.Add(lblRefine);
            gbRefine.Controls.Add(numRefine);

            // ---- gbSales ----
            gbSales.Text = "Продажи";
            gbSales.Width = 160;
            gbSales.Height = 70;
            gbSales.Left = gbRefine.Right + 10;
            gbSales.Top = 8;

            lblSalesTax.Text = "Налог, %:";
            lblSalesTax.Left = 12;
            lblSalesTax.Top = 20;
            lblSalesTax.AutoSize = true;

            numSalesTax.Left = 80;
            numSalesTax.Top = 18;
            numSalesTax.Width = 60;
            numSalesTax.Maximum = 100;
            numSalesTax.Minimum = 0;
            numSalesTax.DecimalPlaces = 2;
            numSalesTax.Value = 1;

            gbSales.Controls.Add(lblSalesTax);
            gbSales.Controls.Add(numSalesTax);

            // ---- gbReact ----
            gbReact.Text = "Reactions ME/TE";
            gbReact.Width = 200;
            gbReact.Height = 70;
            gbReact.Left = 8;
            gbReact.Top = gbMode.Bottom + 8;

            lblReactME.Text = "ME, %:";
            lblReactME.Left = 12;
            lblReactME.Top = 22;
            lblReactME.AutoSize = true;

            numReactME.Left = 60;
            numReactME.Top = 20;
            numReactME.Width = 60;
            numReactME.Maximum = 100;
            numReactME.Minimum = 0;
            numReactME.DecimalPlaces = 2;

            lblReactTE.Text = "TE, %:";
            lblReactTE.Left = 12;
            lblReactTE.Top = 42;
            lblReactTE.AutoSize = true;

            numReactTE.Left = 60;
            numReactTE.Top = 40;
            numReactTE.Width = 60;
            numReactTE.Maximum = 100;
            numReactTE.Minimum = 0;
            numReactTE.DecimalPlaces = 2;

            gbReact.Controls.Add(lblReactME);
            gbReact.Controls.Add(numReactME);
            gbReact.Controls.Add(lblReactTE);
            gbReact.Controls.Add(numReactTE);

            // ---- gbBpo ----
            gbBpo.Text = "Blueprints ME/TE";
            gbBpo.Width = 200;
            gbBpo.Height = 70;
            gbBpo.Left = gbReact.Right + 10;
            gbBpo.Top = gbMode.Bottom + 8;

            lblBpoME.Text = "ME, %:";
            lblBpoME.Left = 12;
            lblBpoME.Top = 22;
            lblBpoME.AutoSize = true;

            numBpoME.Left = 60;
            numBpoME.Top = 20;
            numBpoME.Width = 60;
            numBpoME.Maximum = 100;
            numBpoME.Minimum = 0;
            numBpoME.DecimalPlaces = 2;

            lblBpoTE.Text = "TE, %:";
            lblBpoTE.Left = 12;
            lblBpoTE.Top = 42;
            lblBpoTE.AutoSize = true;

            numBpoTE.Left = 60;
            numBpoTE.Top = 40;
            numBpoTE.Width = 60;
            numBpoTE.Maximum = 100;
            numBpoTE.Minimum = 0;
            numBpoTE.DecimalPlaces = 2;

            gbBpo.Controls.Add(lblBpoME);
            gbBpo.Controls.Add(numBpoME);
            gbBpo.Controls.Add(lblBpoTE);
            gbBpo.Controls.Add(numBpoTE);

            // ---- btnCalc ----
            btnCalc.Text = "Пересчитать";
            btnCalc.Width = 140;
            btnCalc.Height = 40;
            btnCalc.Left = gbBpo.Right + 20;
            btnCalc.Top = gbMode.Bottom + 16;

            // ---- grid ----
            grid.Dock = DockStyle.Fill;
            grid.ReadOnly = true;
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToResizeRows = false;
            grid.RowHeadersVisible = false;
            grid.MultiSelect = false;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.AutoGenerateColumns = false;

            // ---- progress + status ----
            progress.Dock = DockStyle.Bottom;
            progress.Height = 8;

            statusStrip.Dock = DockStyle.Bottom;
            statusLabel.Text = "Готово";
            statusStrip.Items.Add(statusLabel);

            // ---- layout ----
            panelTop.Controls.Add(gbMode);
            panelTop.Controls.Add(gbOre);
            panelTop.Controls.Add(gbRefine);
            panelTop.Controls.Add(gbSales);
            panelTop.Controls.Add(gbReact);
            panelTop.Controls.Add(gbBpo);
            panelTop.Controls.Add(btnCalc);

            this.Controls.Add(grid);
            this.Controls.Add(progress);
            this.Controls.Add(statusStrip);
            this.Controls.Add(panelTop);

            this.ResumeLayout(false);
        }
    }
}
