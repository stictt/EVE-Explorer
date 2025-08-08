namespace TestForm
{
    partial class Form1
    {
        /// <summary>
        ///  Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        ///  Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            dataGridView1 = new System.Windows.Forms.DataGridView();
            marginDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ratingTypeDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            averageVolumeDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            nameDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            buyPriceDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            sellPriceDataGridViewTextBoxColumn = new System.Windows.Forms.DataGridViewTextBoxColumn();
            modelBindingSource1 = new System.Windows.Forms.BindingSource(components);
            modelBindingSource = new System.Windows.Forms.BindingSource(components);
            toolStrip1 = new System.Windows.Forms.ToolStrip();
            toolStripLabelSearch = new System.Windows.Forms.ToolStripLabel();
            txtSearch = new System.Windows.Forms.ToolStripTextBox();
            toolStripSeparator1 = new System.Windows.Forms.ToolStripSeparator();
            toolStripLabelMode = new System.Windows.Forms.ToolStripLabel();
            cmbMode = new System.Windows.Forms.ToolStripComboBox();
            toolStripSeparator2 = new System.Windows.Forms.ToolStripSeparator();
            btnRefresh = new System.Windows.Forms.ToolStripButton();
            statusStrip1 = new System.Windows.Forms.StatusStrip();
            statusLabel = new System.Windows.Forms.ToolStripStatusLabel();
            statusProgress = new System.Windows.Forms.ToolStripProgressBar();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)modelBindingSource1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)modelBindingSource).BeginInit();
            toolStrip1.SuspendLayout();
            statusStrip1.SuspendLayout();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left | System.Windows.Forms.AnchorStyles.Right;
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.EnableHeadersVisualStyles = false;
            dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            marginDataGridViewTextBoxColumn,
            ratingTypeDataGridViewTextBoxColumn,
            averageVolumeDataGridViewTextBoxColumn,
            nameDataGridViewTextBoxColumn,
            buyPriceDataGridViewTextBoxColumn,
            sellPriceDataGridViewTextBoxColumn});
            dataGridView1.DataSource = modelBindingSource1;
            dataGridView1.Location = new System.Drawing.Point(12, 39);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.ReadOnly = true;
            dataGridView1.RowHeadersVisible = false;
            dataGridView1.RowHeadersWidth = 51;
            dataGridView1.RowTemplate.Height = 29;
            dataGridView1.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dataGridView1.Size = new System.Drawing.Size(1107, 560);
            dataGridView1.TabIndex = 0;
            dataGridView1.CellDoubleClick += dataGridView1_CellDoubleClick;
            dataGridView1.KeyDown += dataGridView1_KeyDown;
            // 
            // marginDataGridViewTextBoxColumn
            // 
            marginDataGridViewTextBoxColumn.DataPropertyName = "Margin";
            marginDataGridViewTextBoxColumn.HeaderText = "Margin";
            marginDataGridViewTextBoxColumn.MinimumWidth = 6;
            marginDataGridViewTextBoxColumn.Name = "marginDataGridViewTextBoxColumn";
            marginDataGridViewTextBoxColumn.ReadOnly = true;
            marginDataGridViewTextBoxColumn.Width = 110;
            // 
            // ratingTypeDataGridViewTextBoxColumn
            // 
            ratingTypeDataGridViewTextBoxColumn.DataPropertyName = "ratingType";
            ratingTypeDataGridViewTextBoxColumn.HeaderText = "ratingType";
            ratingTypeDataGridViewTextBoxColumn.MinimumWidth = 6;
            ratingTypeDataGridViewTextBoxColumn.Name = "ratingTypeDataGridViewTextBoxColumn";
            ratingTypeDataGridViewTextBoxColumn.ReadOnly = true;
            ratingTypeDataGridViewTextBoxColumn.Width = 110;
            // 
            // averageVolumeDataGridViewTextBoxColumn
            // 
            averageVolumeDataGridViewTextBoxColumn.DataPropertyName = "averageVolume";
            averageVolumeDataGridViewTextBoxColumn.HeaderText = "averageVolume";
            averageVolumeDataGridViewTextBoxColumn.MinimumWidth = 6;
            averageVolumeDataGridViewTextBoxColumn.Name = "averageVolumeDataGridViewTextBoxColumn";
            averageVolumeDataGridViewTextBoxColumn.ReadOnly = true;
            averageVolumeDataGridViewTextBoxColumn.Width = 130;
            // 
            // nameDataGridViewTextBoxColumn
            // 
            nameDataGridViewTextBoxColumn.DataPropertyName = "Name";
            nameDataGridViewTextBoxColumn.HeaderText = "Name";
            nameDataGridViewTextBoxColumn.MinimumWidth = 6;
            nameDataGridViewTextBoxColumn.Name = "nameDataGridViewTextBoxColumn";
            nameDataGridViewTextBoxColumn.ReadOnly = true;
            nameDataGridViewTextBoxColumn.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            // 
            // buyPriceDataGridViewTextBoxColumn
            // 
            buyPriceDataGridViewTextBoxColumn.DataPropertyName = "buyPrice";
            buyPriceDataGridViewTextBoxColumn.HeaderText = "buyPrice";
            buyPriceDataGridViewTextBoxColumn.MinimumWidth = 6;
            buyPriceDataGridViewTextBoxColumn.Name = "buyPriceDataGridViewTextBoxColumn";
            buyPriceDataGridViewTextBoxColumn.ReadOnly = true;
            buyPriceDataGridViewTextBoxColumn.Width = 120;
            // 
            // sellPriceDataGridViewTextBoxColumn
            // 
            sellPriceDataGridViewTextBoxColumn.DataPropertyName = "sellPrice";
            sellPriceDataGridViewTextBoxColumn.HeaderText = "sellPrice";
            sellPriceDataGridViewTextBoxColumn.MinimumWidth = 6;
            sellPriceDataGridViewTextBoxColumn.Name = "sellPriceDataGridViewTextBoxColumn";
            sellPriceDataGridViewTextBoxColumn.ReadOnly = true;
            sellPriceDataGridViewTextBoxColumn.Width = 120;
            // 
            // modelBindingSource1
            // 
            modelBindingSource1.DataSource = typeof(Model);
            // 
            // modelBindingSource
            // 
            modelBindingSource.DataSource = typeof(Model);
            // 
            // toolStrip1
            // 
            toolStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            toolStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            toolStripLabelSearch,
            txtSearch,
            toolStripSeparator1,
            toolStripLabelMode,
            cmbMode,
            toolStripSeparator2,
            btnRefresh});
            toolStrip1.Location = new System.Drawing.Point(0, 0);
            toolStrip1.Name = "toolStrip1";
            toolStrip1.Size = new System.Drawing.Size(1131, 27);
            toolStrip1.TabIndex = 10;
            toolStrip1.Text = "toolStrip1";
            // 
            // toolStripLabelSearch
            // 
            toolStripLabelSearch.Name = "toolStripLabelSearch";
            toolStripLabelSearch.Size = new System.Drawing.Size(53, 24);
            toolStripLabelSearch.Text = "Поиск:";
            // 
            // txtSearch
            // 
            txtSearch.Name = "txtSearch";
            txtSearch.Size = new System.Drawing.Size(240, 27);
            txtSearch.ToolTipText = "Имя или TypeID";
            txtSearch.TextChanged += txtSearch_TextChanged;
            // 
            // toolStripSeparator1
            // 
            toolStripSeparator1.Name = "toolStripSeparator1";
            toolStripSeparator1.Size = new System.Drawing.Size(6, 27);
            // 
            // toolStripLabelMode
            // 
            toolStripLabelMode.Name = "toolStripLabelMode";
            toolStripLabelMode.Size = new System.Drawing.Size(60, 24);
            toolStripLabelMode.Text = "Режим:";
            // 
            // cmbMode
            // 
            cmbMode.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            cmbMode.Items.AddRange(new object[] { "Арбитраж", "Топ объём", "Маржа %" });
            cmbMode.Name = "cmbMode";
            cmbMode.Size = new System.Drawing.Size(151, 27);
            cmbMode.SelectedIndex = 0;
            cmbMode.SelectedIndexChanged += cmbMode_SelectedIndexChanged;
            // 
            // toolStripSeparator2
            // 
            toolStripSeparator2.Name = "toolStripSeparator2";
            toolStripSeparator2.Size = new System.Drawing.Size(6, 27);
            // 
            // btnRefresh
            // 
            btnRefresh.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Text;
            btnRefresh.ImageTransparentColor = System.Drawing.Color.Magenta;
            btnRefresh.Name = "btnRefresh";
            btnRefresh.Size = new System.Drawing.Size(80, 24);
            btnRefresh.Text = "Обновить";
            btnRefresh.Click += btnRefresh_Click;
            // 
            // statusStrip1
            // 
            statusStrip1.ImageScalingSize = new System.Drawing.Size(20, 20);
            statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] { statusLabel, statusProgress });
            statusStrip1.Location = new System.Drawing.Point(0, 605);
            statusStrip1.Name = "statusStrip1";
            statusStrip1.Size = new System.Drawing.Size(1131, 22);
            statusStrip1.TabIndex = 11;
            statusStrip1.Text = "statusStrip1";
            // 
            // statusLabel
            // 
            statusLabel.Name = "statusLabel";
            statusLabel.Size = new System.Drawing.Size(52, 17);
            statusLabel.Text = "Готово";
            // 
            // statusProgress
            // 
            statusProgress.Name = "statusProgress";
            statusProgress.Size = new System.Drawing.Size(200, 16);
            statusProgress.Visible = false;
            // 
            // Form1
            // 
            AutoScaleDimensions = new System.Drawing.SizeF(8F, 20F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            ClientSize = new System.Drawing.Size(1131, 627);
            Controls.Add(statusStrip1);
            Controls.Add(toolStrip1);
            Controls.Add(dataGridView1);
            Name = "Form1";
            Text = "Аналитический отчёт";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)modelBindingSource1).EndInit();
            ((System.ComponentModel.ISupportInitialize)modelBindingSource).EndInit();
            toolStrip1.ResumeLayout(false);
            toolStrip1.PerformLayout();
            statusStrip1.ResumeLayout(false);
            statusStrip1.PerformLayout();
            ResumeLayout(false);
            PerformLayout();
        }

        #endregion

        private System.Windows.Forms.DataGridView dataGridView1;
        private System.Windows.Forms.BindingSource modelBindingSource;
        private System.Windows.Forms.BindingSource modelBindingSource1;
        private System.Windows.Forms.DataGridViewTextBoxColumn marginDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn ratingTypeDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn averageVolumeDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn nameDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn buyPriceDataGridViewTextBoxColumn;
        private System.Windows.Forms.DataGridViewTextBoxColumn sellPriceDataGridViewTextBoxColumn;

        private System.Windows.Forms.ToolStrip toolStrip1;
        private System.Windows.Forms.ToolStripLabel toolStripLabelSearch;
        private System.Windows.Forms.ToolStripTextBox txtSearch;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator1;
        private System.Windows.Forms.ToolStripLabel toolStripLabelMode;
        private System.Windows.Forms.ToolStripComboBox cmbMode;
        private System.Windows.Forms.ToolStripSeparator toolStripSeparator2;
        private System.Windows.Forms.ToolStripButton btnRefresh;

        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel statusLabel;
        private System.Windows.Forms.ToolStripProgressBar statusProgress;
    }
}
