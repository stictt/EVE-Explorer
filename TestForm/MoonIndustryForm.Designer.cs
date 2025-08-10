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
        private Label lblRefinePct;
        private NumericUpDown numRefine;

        private GroupBox gbTaxes;
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
            lblRefinePct = new Label();
            numRefine = new NumericUpDown();

            gbTaxes = new GroupBox();
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

            // panelTop
            panelTop.Dock = DockStyle.Top;
            panelTop.Height = 165;
            panelTop.Padding = new Padding(8);

            // gbMode
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

            rbModeBlueprints.Text = "BPO с реакциями";
            rbModeBlueprints.Left = 140;
            rbModeBlueprints.Top = 40;
            rbModeBlueprints.AutoSize = true;

            gbMode.Controls.Add(rbModeOre);
            gbMode.Controls.Add(rbModeReactions);
            gbMode.Controls.Add(rbModeBlueprints);

            // gbOre
            gbOre.Text = "Руда (ликвидность)";
            gbOre.Width = 290;
            gbOre.Height = 70;
            gbOre.Left = gbMode.Right + 8;
            gbOre.Top = gbMode.Top;

            chkUseOre.Text = "Пробовать покупать руду";
            chkUseOre.Left = 12;
            chkUseOre.Top = 20;
            chkUseOre.AutoSize = true;

            lblMinOreRatingBn.Text = "Мин. Rating (в млрд):";
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

            // gbRefine
            gbRefine.Text = "Рефайн";
            gbRefine.Width = 160;
            gbRefine.Height = 70;
            gbRefine.Left = gbOre.Right + 8;
            gbRefine.Top = gbMode.Top;

            lblRefinePct.Text = "Yield %:";
            lblRefinePct.Left = 12;
            lblRefinePct.Top = 30;
            lblRefinePct.AutoSize = true;

            numRefine.Left = 70;
            numRefine.Top = 26;
            numRefine.Width = 70;
            numRefine.DecimalPlaces = 0;
            numRefine.Minimum = 0;
            numRefine.Maximum = 100;
            numRefine.Value = 55;

            gbRefine.Controls.Add(lblRefinePct);
            gbRefine.Controls.Add(numRefine);

            // gbTaxes
            gbTaxes.Text = "Налоги";
            gbTaxes.Width = 160;
            gbTaxes.Height = 70;
            gbTaxes.Left = gbRefine.Right + 8;
            gbTaxes.Top = gbMode.Top;

            lblSalesTax.Text = "Продажа %:";
            lblSalesTax.Left = 12;
            lblSalesTax.Top = 30;
            lblSalesTax.AutoSize = true;

            numSalesTax.Left = 90;
            numSalesTax.Top = 26;
            numSalesTax.Width = 60;
            numSalesTax.DecimalPlaces = 2;
            numSalesTax.Minimum = 0;
            numSalesTax.Maximum = 100;
            numSalesTax.Increment = 0.10M;
            numSalesTax.Value = 1.50M;

            gbTaxes.Controls.Add(lblSalesTax);
            gbTaxes.Controls.Add(numSalesTax);

            // gbReact
            gbReact.Text = "Реакции (ME/TE)";
            gbReact.Width = 220;
            gbReact.Height = 70;
            gbReact.Left = gbMode.Left;
            gbReact.Top = gbMode.Bottom + 8;

            lblReactME.Text = "ME %:";
            lblReactME.Left = 12;
            lblReactME.Top = 30;
            lblReactME.AutoSize = true;

            numReactME.Left = 60;
            numReactME.Top = 26;
            numReactME.Width = 60;
            numReactME.DecimalPlaces = 2;
            numReactME.Minimum = 0;
            numReactME.Maximum = 100;

            lblReactTE.Text = "TE %:";
            lblReactTE.Left = 130;
            lblReactTE.Top = 30;
            lblReactTE.AutoSize = true;

            numReactTE.Left = 170;
            numReactTE.Top = 26;
            numReactTE.Width = 40;
            numReactTE.DecimalPlaces = 2;
            numReactTE.Minimum = 0;
            numReactTE.Maximum = 100;

            gbReact.Controls.Add(lblReactME);
            gbReact.Controls.Add(numReactME);
            gbReact.Controls.Add(lblReactTE);
            gbReact.Controls.Add(numReactTE);

            // gbBpo
            gbBpo.Text = "BPO (ME/TE)";
            gbBpo.Width = 220;
            gbBpo.Height = 70;
            gbBpo.Left = gbReact.Right + 8;
            gbBpo.Top = gbReact.Top;

            lblBpoME.Text = "ME %:";
            lblBpoME.Left = 12;
            lblBpoME.Top = 30;
            lblBpoME.AutoSize = true;

            numBpoME.Left = 60;
            numBpoME.Top = 26;
            numBpoME.Width = 60;
            numBpoME.DecimalPlaces = 2;
            numBpoME.Minimum = 0;
            numBpoME.Maximum = 100;

            lblBpoTE.Text = "TE %:";
            lblBpoTE.Left = 130;
            lblBpoTE.Top = 30;
            lblBpoTE.AutoSize = true;

            numBpoTE.Left = 170;
            numBpoTE.Top = 26;
            numBpoTE.Width = 40;
            numBpoTE.DecimalPlaces = 2;
            numBpoTE.Minimum = 0;
            numBpoTE.Maximum = 100;

            gbBpo.Controls.Add(lblBpoME);
            gbBpo.Controls.Add(numBpoME);
            gbBpo.Controls.Add(lblBpoTE);
            gbBpo.Controls.Add(numBpoTE);

            // btnCalc
            btnCalc.Text = "Рассчитать";
            btnCalc.Width = 120;
            btnCalc.Height = 30;
            btnCalc.Left = gbBpo.Right + 16;
            btnCalc.Top = gbBpo.Top + 18;

            // grid
            grid.AllowUserToAddRows = false;
            grid.AllowUserToDeleteRows = false;
            grid.AllowUserToOrderColumns = true;
            grid.ReadOnly = true;
            grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
            grid.MultiSelect = false;
            grid.Dock = DockStyle.Fill;
            grid.RowHeadersVisible = false;
            grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells;

            // progress
            progress.Dock = DockStyle.Bottom;
            progress.Height = 6;
            progress.Minimum = 0;
            progress.Maximum = 100;

            // statusStrip
            statusStrip.Dock = DockStyle.Bottom;
            statusLabel.Text = "Готово";
            statusStrip.Items.Add(statusLabel);

            // Compose panelTop
            panelTop.Controls.Add(gbMode);
            panelTop.Controls.Add(gbOre);
            panelTop.Controls.Add(gbRefine);
            panelTop.Controls.Add(gbTaxes);
            panelTop.Controls.Add(gbReact);
            panelTop.Controls.Add(gbBpo);
            panelTop.Controls.Add(btnCalc);

            // Form
            this.Text = "Moon Industry — аналитика реакций/BPO/руды";
            this.StartPosition = FormStartPosition.CenterParent;
            this.Width = 1300;
            this.Height = 800;

            this.Controls.Add(grid);
            this.Controls.Add(progress);
            this.Controls.Add(statusStrip);
            this.Controls.Add(panelTop);
        }
    }
}
