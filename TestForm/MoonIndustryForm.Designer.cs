using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

namespace TestForm
{
    partial class MoonIndustryForm
    {
        private IContainer components = null;

        private Panel panelTop;
        private FlowLayoutPanel flowTop;

        private GroupBox gbMode;
        private RadioButton rbModeOre;
        private RadioButton rbModeReactions;
        private RadioButton rbModeBlueprints;

        private GroupBox gbOre;
        private CheckBox chkUseOre;
        private CheckBox chkAllOres;               // NEW
        private Label lblMinOreRatingBn;
        private NumericUpDown numMinOreRatingBn;
        private Label lblMinOreHint;

        private GroupBox gbRefine;
        private Label lblRefine;
        private NumericUpDown numRefine;

        private GroupBox gbSales;
        private Label lblSalesTax;
        private NumericUpDown numSalesTax;

        private GroupBox gbBuy;
        private Label lblBuyTax;
        private NumericUpDown numBuyTax;

        private GroupBox gbReact;
        private Label lblReactME;
        private NumericUpDown numReactME;
        private Label lblReactTE;
        private NumericUpDown numReactTE;

        private GroupBox gbReactFee;
        private Label lblReactJobTax;
        private NumericUpDown numReactJobTax;

        private GroupBox gbBpo;
        private Label lblBpoME;
        private NumericUpDown numBpoME;
        private Label lblBpoTE;
        private NumericUpDown numBpoTE;

        private GroupBox gbBpoFee;
        private Label lblBpoJobTax;
        private NumericUpDown numBpoJobTax;

        private GroupBox gbBasis;
        private RadioButton rbInputsUseBuy;
        private RadioButton rbInputsUseSell;

        private GroupBox gbSearch;                 // NEW
        private TextBox txtSearch;                 // NEW
        private Label lblMinRatingBn;              // NEW
        private NumericUpDown numMinRatingBn;      // NEW

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
            flowTop = new FlowLayoutPanel();

            gbMode = new GroupBox();
            rbModeOre = new RadioButton();
            rbModeReactions = new RadioButton();
            rbModeBlueprints = new RadioButton();

            gbOre = new GroupBox();
            chkUseOre = new CheckBox();
            chkAllOres = new CheckBox();           // NEW
            lblMinOreRatingBn = new Label();
            numMinOreRatingBn = new NumericUpDown();
            lblMinOreHint = new Label();

            gbRefine = new GroupBox();
            lblRefine = new Label();
            numRefine = new NumericUpDown();

            gbSales = new GroupBox();
            lblSalesTax = new Label();
            numSalesTax = new NumericUpDown();

            gbBuy = new GroupBox();
            lblBuyTax = new Label();
            numBuyTax = new NumericUpDown();

            gbReact = new GroupBox();
            lblReactME = new Label();
            numReactME = new NumericUpDown();
            lblReactTE = new Label();
            numReactTE = new NumericUpDown();

            gbReactFee = new GroupBox();
            lblReactJobTax = new Label();
            numReactJobTax = new NumericUpDown();

            gbBpo = new GroupBox();
            lblBpoME = new Label();
            numBpoME = new NumericUpDown();
            lblBpoTE = new Label();
            numBpoTE = new NumericUpDown();

            gbBpoFee = new GroupBox();
            lblBpoJobTax = new Label();
            numBpoJobTax = new NumericUpDown();

            gbBasis = new GroupBox();
            rbInputsUseBuy = new RadioButton();
            rbInputsUseSell = new RadioButton();

            gbSearch = new GroupBox();            // NEW
            txtSearch = new TextBox();            // NEW
            lblMinRatingBn = new Label();         // NEW
            numMinRatingBn = new NumericUpDown(); // NEW

            btnCalc = new Button();
            grid = new DataGridView();
            progress = new ProgressBar();
            statusStrip = new StatusStrip();
            statusLabel = new ToolStripStatusLabel();

            // ---- form ----
            SuspendLayout();
            Text = "Moon Industry";
            ClientSize = new Size(1280, 800);
            StartPosition = FormStartPosition.CenterScreen;

            // ---- top panel ----
            panelTop.Dock = DockStyle.Top;
            panelTop.Height = 210;
            panelTop.Padding = new Padding(6);

            flowTop.Dock = DockStyle.Fill;
            flowTop.WrapContents = true;
            flowTop.AutoSize = false;
            flowTop.FlowDirection = FlowDirection.LeftToRight;
            flowTop.Padding = new Padding(0);
            flowTop.Margin = new Padding(0);

            Size szSmall = new Size(230, 72);
            Size szMed = new Size(260, 72);
            Size szLong = new Size(380, 92);      // выше из-за второй галочки

            // ---- gbMode ----
            gbMode.Text = "Режим";
            gbMode.Size = szMed;
            gbMode.Margin = new Padding(6);
            rbModeOre.Text = "Руда → материалы";
            rbModeOre.AutoSize = true; rbModeOre.Left = 10; rbModeOre.Top = 20;
            rbModeReactions.Text = "Конечные реакции";
            rbModeReactions.AutoSize = true; rbModeReactions.Left = 10; rbModeReactions.Top = 40;
            rbModeBlueprints.Text = "Чертежи (BPO/BPC)";
            rbModeBlueprints.AutoSize = true; rbModeBlueprints.Left = 150; rbModeBlueprints.Top = 40;
            gbMode.Controls.AddRange(new Control[] { rbModeOre, rbModeReactions, rbModeBlueprints });

            // ---- gbOre ----
            gbOre.Text = "Руда/ликвидность";
            gbOre.Size = szLong;
            gbOre.Margin = new Padding(6);

            chkUseOre.Text = "Только ликвидная руда";
            chkUseOre.AutoSize = true; chkUseOre.Left = 12; chkUseOre.Top = 18;

            chkAllOres.Text = "Все руды (вкл. лёд)";       // NEW
            chkAllOres.AutoSize = true; chkAllOres.Left = 12; chkAllOres.Top = 38;

            lblMinOreRatingBn.Text = "мин. рейтинг (млрд):";
            lblMinOreRatingBn.AutoSize = true; lblMinOreRatingBn.Left = 12; lblMinOreRatingBn.Top = 62;
            numMinOreRatingBn.Left = 160; numMinOreRatingBn.Top = 58; numMinOreRatingBn.Width = 80;
            numMinOreRatingBn.DecimalPlaces = 2; numMinOreRatingBn.Maximum = 1000000;

            lblMinOreHint.Text = "оценка ликвидности руды";
            lblMinOreHint.AutoSize = true; lblMinOreHint.Left = 245; lblMinOreHint.Top = 62;

            gbOre.Controls.AddRange(new Control[] { chkUseOre, chkAllOres, lblMinOreRatingBn, numMinOreRatingBn, lblMinOreHint });

            // ---- gbRefine ----
            gbRefine.Text = "Рефайн";
            gbRefine.Size = szSmall; gbRefine.Margin = new Padding(6);
            lblRefine.Text = "Выход, %:"; lblRefine.AutoSize = true; lblRefine.Left = 12; lblRefine.Top = 20;
            numRefine.Left = 80; numRefine.Top = 18; numRefine.Width = 60;
            numRefine.Maximum = 100; numRefine.DecimalPlaces = 2; numRefine.Value = 90;
            gbRefine.Controls.AddRange(new Control[] { lblRefine, numRefine });

            // ---- gbSales ----
            gbSales.Text = "Продажи";
            gbSales.Size = szSmall; gbSales.Margin = new Padding(6);
            lblSalesTax.Text = "Налог, %:"; lblSalesTax.AutoSize = true; lblSalesTax.Left = 12; lblSalesTax.Top = 20;
            numSalesTax.Left = 80; numSalesTax.Top = 18; numSalesTax.Width = 60;
            numSalesTax.Maximum = 100; numSalesTax.DecimalPlaces = 2; numSalesTax.Value = 3;
            gbSales.Controls.AddRange(new Control[] { lblSalesTax, numSalesTax });

            // ---- gbBuy ----
            gbBuy.Text = "Покупки";
            gbBuy.Size = szSmall; gbBuy.Margin = new Padding(6);
            lblBuyTax.Text = "Налог, %:"; lblBuyTax.AutoSize = true; lblBuyTax.Left = 12; lblBuyTax.Top = 20;
            numBuyTax.Left = 80; numBuyTax.Top = 18; numBuyTax.Width = 60;
            numBuyTax.Maximum = 100; numBuyTax.DecimalPlaces = 2; numBuyTax.Value = 3;
            gbBuy.Controls.AddRange(new Control[] { lblBuyTax, numBuyTax });

            // ---- gbReact ----
            gbReact.Text = "Reactions ME/TE";
            gbReact.Size = szSmall; gbReact.Margin = new Padding(6);
            lblReactME.Text = "ME, %:"; lblReactME.AutoSize = true; lblReactME.Left = 12; lblReactME.Top = 22;
            numReactME.Left = 60; numReactME.Top = 20; numReactME.Width = 60; numReactME.Maximum = 100; numReactME.DecimalPlaces = 2;
            lblReactTE.Text = "TE, %:"; lblReactTE.AutoSize = true; lblReactTE.Left = 12; lblReactTE.Top = 42;
            numReactTE.Left = 60; numReactTE.Top = 40; numReactTE.Width = 60; numReactTE.Maximum = 100; numReactTE.DecimalPlaces = 2;
            gbReact.Controls.AddRange(new Control[] { lblReactME, numReactME, lblReactTE, numReactTE });

            // ---- gbReactFee ----
            gbReactFee.Text = "Налог реакций";
            gbReactFee.Size = szSmall; gbReactFee.Margin = new Padding(6);
            lblReactJobTax.Text = "Комиссия, %:"; lblReactJobTax.AutoSize = true; lblReactJobTax.Left = 12; lblReactJobTax.Top = 22;
            numReactJobTax.Left = 100; numReactJobTax.Top = 20; numReactJobTax.Width = 70; numReactJobTax.Maximum = 100; numReactJobTax.DecimalPlaces = 2; numReactJobTax.Value = 14;
            gbReactFee.Controls.AddRange(new Control[] { lblReactJobTax, numReactJobTax });

            // ---- gbBpo ----
            gbBpo.Text = "Blueprints ME/TE";
            gbBpo.Size = szSmall; gbBpo.Margin = new Padding(6);
            lblBpoME.Text = "ME, %:"; lblBpoME.AutoSize = true; lblBpoME.Left = 12; lblBpoME.Top = 22;
            numBpoME.Left = 60; numBpoME.Top = 20; numBpoME.Width = 60; numBpoME.Maximum = 100; numBpoME.DecimalPlaces = 2;
            lblBpoTE.Text = "TE, %:"; lblBpoTE.AutoSize = true; lblBpoTE.Left = 12; lblBpoTE.Top = 42;
            numBpoTE.Left = 60; numBpoTE.Top = 40; numBpoTE.Width = 60; numBpoTE.Maximum = 100; numBpoTE.DecimalPlaces = 2;
            gbBpo.Controls.AddRange(new Control[] { lblBpoME, numBpoME, lblBpoTE, numBpoTE });

            // ---- gbBpoFee ----
            gbBpoFee.Text = "Налог BPO";
            gbBpoFee.Size = szSmall; gbBpoFee.Margin = new Padding(6);
            lblBpoJobTax.Text = "Комиссия, %:"; lblBpoJobTax.AutoSize = true; lblBpoJobTax.Left = 12; lblBpoJobTax.Top = 22;
            numBpoJobTax.Left = 100; numBpoJobTax.Top = 20; numBpoJobTax.Width = 70; numBpoJobTax.Maximum = 100; numBpoJobTax.DecimalPlaces = 2; numBpoJobTax.Value = 2;
            gbBpoFee.Controls.AddRange(new Control[] { lblBpoJobTax, numBpoJobTax });

            // ---- gbBasis ----
            gbBasis.Text = "Базис входов";
            gbBasis.Size = szSmall; gbBasis.Margin = new Padding(6);
            rbInputsUseBuy.Text = "BUY"; rbInputsUseBuy.AutoSize = true; rbInputsUseBuy.Left = 12; rbInputsUseBuy.Top = 22;
            rbInputsUseSell.Text = "SELL"; rbInputsUseSell.AutoSize = true; rbInputsUseSell.Left = 80; rbInputsUseSell.Top = 22;
            gbBasis.Controls.AddRange(new Control[] { rbInputsUseBuy, rbInputsUseSell });

            // ---- gbSearch ----
            gbSearch.Text = "Поиск / Фильтр";
            gbSearch.Size = new Size(360, 72);
            gbSearch.Margin = new Padding(6);
            txtSearch.Left = 12; txtSearch.Top = 28; txtSearch.Width = 160;
            txtSearch.BorderStyle = BorderStyle.FixedSingle;

            lblMinRatingBn.Text = "мин. рейтинг (млрд):";
            lblMinRatingBn.AutoSize = true; lblMinRatingBn.Left = 180; lblMinRatingBn.Top = 31;
            numMinRatingBn.Left = 315; numMinRatingBn.Top = 28; numMinRatingBn.Width = 60;
            numMinRatingBn.DecimalPlaces = 2; numMinRatingBn.Maximum = 1000000;

            gbSearch.Controls.Add(txtSearch);
            gbSearch.Controls.Add(lblMinRatingBn);
            gbSearch.Controls.Add(numMinRatingBn);

            // ---- btnCalc ----
            btnCalc.Text = "Пересчитать";
            btnCalc.Width = 140; btnCalc.Height = 40; btnCalc.Margin = new Padding(12, 22, 6, 6);

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

            // ---- layout assembly ----
            flowTop.Controls.Add(gbMode);
            flowTop.Controls.Add(gbOre);
            flowTop.Controls.Add(gbRefine);
            flowTop.Controls.Add(gbSales);
            flowTop.Controls.Add(gbBuy);
            flowTop.Controls.Add(gbReact);
            flowTop.Controls.Add(gbReactFee);
            flowTop.Controls.Add(gbBpo);
            flowTop.Controls.Add(gbBpoFee);
            flowTop.Controls.Add(gbBasis);
            flowTop.Controls.Add(gbSearch);      // NEW
            flowTop.Controls.Add(btnCalc);

            panelTop.Controls.Add(flowTop);

            Controls.Add(grid);
            Controls.Add(progress);
            Controls.Add(statusStrip);
            Controls.Add(panelTop);

            ResumeLayout(false);
        }
    }
}
