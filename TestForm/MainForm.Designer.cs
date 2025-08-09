namespace TestForm
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null)) components.Dispose();
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();

            toolStripMain = new System.Windows.Forms.ToolStrip();
            btnOpenArbitrage = new System.Windows.Forms.ToolStripButton();
            btnOpenOre = new System.Windows.Forms.ToolStripButton();
            btnBuildHistory = new System.Windows.Forms.ToolStripButton();
            btnCancel = new System.Windows.Forms.ToolStripButton();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            lblHistory = new System.Windows.Forms.ToolStripLabel();
            toolStripProgress = new System.Windows.Forms.ToolStripProgressBar();
            toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            btnSettings = new System.Windows.Forms.ToolStripButton();

            statusStrip = new System.Windows.Forms.StatusStrip();
            statusLabel = new System.Windows.Forms.ToolStripStatusLabel();

            flowCards = new System.Windows.Forms.FlowLayoutPanel();
            cardArb = new System.Windows.Forms.Panel();
            cardHist = new System.Windows.Forms.Panel();
            cardOre = new System.Windows.Forms.Panel();
            cardMoon = new System.Windows.Forms.Panel();
            cardPi = new System.Windows.Forms.Panel();

            // --- ToolStrip ---
            toolStripMain.ImageScalingSize = new System.Drawing.Size(20, 20);
            toolStripMain.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
                btnOpenArbitrage,
                btnOpenOre,
                btnBuildHistory,
                btnCancel,
                toolStripSeparator1,
                lblHistory,
                toolStripProgress,
                toolStripSeparator2,
                btnSettings
            });
            toolStripMain.Location = new System.Drawing.Point(0, 0);
            toolStripMain.Name = "toolStripMain";
            toolStripMain.Size = new System.Drawing.Size(1000, 27);
            toolStripMain.TabIndex = 0;

            btnOpenArbitrage.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            btnOpenArbitrage.Text = "Открыть отчёт (Арбитраж)";
            btnOpenArbitrage.Click += btnOpenArbitrage_Click;

            btnOpenOre.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            btnOpenOre.Text = "Добыча руды";
            btnOpenOre.Click += btnOpenOre_Click;

            btnBuildHistory.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            btnBuildHistory.Text = "Пересобрать историю";
            btnBuildHistory.Click += btnBuildHistory_Click;

            btnCancel.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            btnCancel.Text = "Отмена";
            btnCancel.Enabled = false;
            btnCancel.Click += btnCancel_Click;

            lblHistory.Text = "История: нет";

            toolStripProgress.AutoSize = false;
            toolStripProgress.Size = new System.Drawing.Size(200, 22);
            toolStripProgress.Step = 1;

            btnSettings.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            btnSettings.Text = "Настройки";
            btnSettings.Click += btnSettings_Click;

            // --- StatusStrip ---
            statusStrip.ImageScalingSize = new System.Drawing.Size(20, 20);
            statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { statusLabel });
            statusStrip.Location = new System.Drawing.Point(0, 571);
            statusStrip.Name = "statusStrip";
            statusStrip.Size = new System.Drawing.Size(1000, 22);
            statusStrip.TabIndex = 2;
            statusLabel.Text = "Готово";

            // --- Flow of cards ---
            flowCards.Dock = System.Windows.Forms.DockStyle.Fill;
            flowCards.Location = new System.Drawing.Point(0, 27);
            flowCards.Name = "flowCards";
            flowCards.Padding = new System.Windows.Forms.Padding(12);
            flowCards.Size = new System.Drawing.Size(1000, 544);
            flowCards.TabIndex = 3;
            flowCards.WrapContents = true;
            flowCards.AutoScroll = true;

            // Card helper
            System.Drawing.Size cardSize = new System.Drawing.Size(300, 140);

            // --- Card: Arbitrage ---
            cardArb = MakeCard("Арбитраж Jita",
                "Просмотреть текущие возможности арбитража в главном хабе.",
                "Открыть", btnOpenArbitrage_Click, cardSize);

            // --- Card: History ---
            cardHist = MakeCardWithInfo(
                "История (OrderHistoryMonthList)",
                "Пересобрать историю цен/объёмов за ~30 дней на основе торгуемых позиций.",
                "Пересобрать", btnBuildHistory_Click,
                out lblHistoryInfo, cardSize);

            // --- Card: Ore ---
            cardOre = MakeCard("Добыча руды",
                "Посчитать ISK/час для сырой руды и рефайна по текущим ценам Джиты.",
                "Открыть", btnOpenOre_Click, cardSize);

            // --- Card: Moon (NEW) ---
            cardMoon = MakeCard("Лунные реакции (аналитика)",
                "Сравнить маржу от лунной руды/материалов и конечных реакций/чертежей.",
                "Открыть", btnOpenMoon_Click, cardSize);

            // --- Card: PI (placeholder) ---
            cardPi = MakeCard("Планетарка (скоро)",
                "Калькулятор сетапов с учётом CPU/Power и выхода/час.", null, null, cardSize);

            flowCards.Controls.Add(cardArb);
            flowCards.Controls.Add(cardHist);
            flowCards.Controls.Add(cardOre);
            flowCards.Controls.Add(cardMoon);   // <-- добавили
            flowCards.Controls.Add(cardPi);

            // --- Form ---
            AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1000, 593);
            Controls.Add(flowCards);
            Controls.Add(statusStrip);
            Controls.Add(toolStripMain);
            Name = "MainForm";
            Text = "EVE Explorer — Панель управления";
        }

        private System.Windows.Forms.Panel MakeCard(string title, string desc, string? btnText, System.EventHandler? onClick, System.Drawing.Size size)
        {
            var p = new System.Windows.Forms.Panel
            {
                BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle,
                Margin = new System.Windows.Forms.Padding(12),
                Size = size
            };
            var lblTitle = new System.Windows.Forms.Label
            {
                Text = title,
                Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold),
                Location = new System.Drawing.Point(12, 10),
                AutoSize = true
            };
            var lblDesc = new System.Windows.Forms.Label
            {
                Text = desc,
                Location = new System.Drawing.Point(12, 38),
                Size = new System.Drawing.Size(size.Width - 24, 48)
            };
            p.Controls.Add(lblTitle);
            p.Controls.Add(lblDesc);

            if (!string.IsNullOrEmpty(btnText) && onClick != null)
            {
                var btn = new System.Windows.Forms.Button
                {
                    Text = btnText,
                    Anchor = (System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right),
                    Location = new System.Drawing.Point(size.Width - 112 - 12, size.Height - 36 - 12),
                    Size = new System.Drawing.Size(112, 36)
                };
                btn.Click += onClick;
                p.Controls.Add(btn);
            }
            return p;
        }

        private System.Windows.Forms.Panel MakeCardWithInfo(
            string title, string desc, string btnText, System.EventHandler onClick,
            out System.Windows.Forms.Label infoLabel, System.Drawing.Size size)
        {
            var p = MakeCard(title, desc, btnText, onClick, size);

            infoLabel = new System.Windows.Forms.Label
            {
                Text = "Последняя генерация: нет",
                ForeColor = System.Drawing.Color.DimGray,
                AutoSize = true
            };

            int padding = 12;
            int buttonHeight = 36;
            int spacing = 8;
            int y = size.Height - padding - buttonHeight - spacing - infoLabel.PreferredHeight;

            infoLabel.Location = new System.Drawing.Point(padding, y);
            infoLabel.Anchor = (System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left);

            p.Controls.Add(infoLabel);
            return p;
        }

        #endregion

        private System.Windows.Forms.ToolStrip toolStripMain;
        private System.Windows.Forms.ToolStripButton btnOpenArbitrage;
        private System.Windows.Forms.ToolStripButton btnOpenOre;
        private System.Windows.Forms.ToolStripButton btnBuildHistory;
        private System.Windows.Forms.ToolStripButton btnCancel;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripLabel lblHistory;
        private System.Windows.Forms.ToolStripProgressBar toolStripProgress;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton btnSettings;

        private System.Windows.Forms.StatusStrip statusStrip;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;

        private System.Windows.Forms.FlowLayoutPanel flowCards;
        private System.Windows.Forms.Panel cardArb;
        private System.Windows.Forms.Panel cardHist;
        private System.Windows.Forms.Panel cardOre;
        private System.Windows.Forms.Panel cardMoon;   // NEW
        private System.Windows.Forms.Panel cardPi;

        private System.Windows.Forms.Label lblHistoryInfo;
    }
}
