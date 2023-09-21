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

        /// <summary>
        ///  Required method for Designer support - do not modify
        ///  the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            components = new System.ComponentModel.Container();
            dataGridView1 = new DataGridView();
            marginDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            ratingTypeDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            averageVolumeDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            nameDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            buyPriceDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            sellPriceDataGridViewTextBoxColumn = new DataGridViewTextBoxColumn();
            modelBindingSource1 = new BindingSource(components);
            modelBindingSource = new BindingSource(components);
            button1 = new Button();
            ((System.ComponentModel.ISupportInitialize)dataGridView1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)modelBindingSource1).BeginInit();
            ((System.ComponentModel.ISupportInitialize)modelBindingSource).BeginInit();
            SuspendLayout();
            // 
            // dataGridView1
            // 
            dataGridView1.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridView1.Columns.AddRange(new DataGridViewColumn[] { marginDataGridViewTextBoxColumn, ratingTypeDataGridViewTextBoxColumn, averageVolumeDataGridViewTextBoxColumn, nameDataGridViewTextBoxColumn, buyPriceDataGridViewTextBoxColumn, sellPriceDataGridViewTextBoxColumn });
            dataGridView1.DataSource = modelBindingSource1;
            dataGridView1.Location = new Point(12, 12);
            dataGridView1.Name = "dataGridView1";
            dataGridView1.RowHeadersWidth = 51;
            dataGridView1.RowTemplate.Height = 29;
            dataGridView1.Size = new Size(989, 603);
            dataGridView1.TabIndex = 0;
            // 
            // marginDataGridViewTextBoxColumn
            // 
            marginDataGridViewTextBoxColumn.DataPropertyName = "Margin";
            marginDataGridViewTextBoxColumn.HeaderText = "Margin";
            marginDataGridViewTextBoxColumn.MinimumWidth = 6;
            marginDataGridViewTextBoxColumn.Name = "marginDataGridViewTextBoxColumn";
            marginDataGridViewTextBoxColumn.Width = 125;
            // 
            // ratingTypeDataGridViewTextBoxColumn
            // 
            ratingTypeDataGridViewTextBoxColumn.DataPropertyName = "ratingType";
            ratingTypeDataGridViewTextBoxColumn.HeaderText = "ratingType";
            ratingTypeDataGridViewTextBoxColumn.MinimumWidth = 6;
            ratingTypeDataGridViewTextBoxColumn.Name = "ratingTypeDataGridViewTextBoxColumn";
            ratingTypeDataGridViewTextBoxColumn.Width = 125;
            // 
            // averageVolumeDataGridViewTextBoxColumn
            // 
            averageVolumeDataGridViewTextBoxColumn.DataPropertyName = "averageVolume";
            averageVolumeDataGridViewTextBoxColumn.HeaderText = "averageVolume";
            averageVolumeDataGridViewTextBoxColumn.MinimumWidth = 6;
            averageVolumeDataGridViewTextBoxColumn.Name = "averageVolumeDataGridViewTextBoxColumn";
            averageVolumeDataGridViewTextBoxColumn.Width = 125;
            // 
            // nameDataGridViewTextBoxColumn
            // 
            nameDataGridViewTextBoxColumn.DataPropertyName = "Name";
            nameDataGridViewTextBoxColumn.HeaderText = "Name";
            nameDataGridViewTextBoxColumn.MinimumWidth = 6;
            nameDataGridViewTextBoxColumn.Name = "nameDataGridViewTextBoxColumn";
            nameDataGridViewTextBoxColumn.Width = 125;
            // 
            // buyPriceDataGridViewTextBoxColumn
            // 
            buyPriceDataGridViewTextBoxColumn.DataPropertyName = "buyPrice";
            buyPriceDataGridViewTextBoxColumn.HeaderText = "buyPrice";
            buyPriceDataGridViewTextBoxColumn.MinimumWidth = 6;
            buyPriceDataGridViewTextBoxColumn.Name = "buyPriceDataGridViewTextBoxColumn";
            buyPriceDataGridViewTextBoxColumn.Width = 125;
            // 
            // sellPriceDataGridViewTextBoxColumn
            // 
            sellPriceDataGridViewTextBoxColumn.DataPropertyName = "sellPrice";
            sellPriceDataGridViewTextBoxColumn.HeaderText = "sellPrice";
            sellPriceDataGridViewTextBoxColumn.MinimumWidth = 6;
            sellPriceDataGridViewTextBoxColumn.Name = "sellPriceDataGridViewTextBoxColumn";
            sellPriceDataGridViewTextBoxColumn.Width = 125;
            // 
            // modelBindingSource1
            // 
            modelBindingSource1.DataSource = typeof(Model);
            // 
            // modelBindingSource
            // 
            modelBindingSource.DataSource = typeof(Model);
            // 
            // button1
            // 
            button1.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            button1.Location = new Point(1025, 12);
            button1.Name = "button1";
            button1.Size = new Size(94, 29);
            button1.TabIndex = 1;
            button1.Text = "Update";
            button1.UseVisualStyleBackColor = true;
            button1.Click += button1_Click;
            // 
            // Form1
            // 
            AutoScaleDimensions = new SizeF(8F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(1131, 627);
            Controls.Add(button1);
            Controls.Add(dataGridView1);
            Name = "Form1";
            Text = "Form1";
            ((System.ComponentModel.ISupportInitialize)dataGridView1).EndInit();
            ((System.ComponentModel.ISupportInitialize)modelBindingSource1).EndInit();
            ((System.ComponentModel.ISupportInitialize)modelBindingSource).EndInit();
            ResumeLayout(false);
        }

        #endregion

        private DataGridView dataGridView1;
        private BindingSource modelBindingSource;
        private Button button1;
        private BindingSource modelBindingSource1;
        private DataGridViewTextBoxColumn marginDataGridViewTextBoxColumn;
        private DataGridViewTextBoxColumn ratingTypeDataGridViewTextBoxColumn;
        private DataGridViewTextBoxColumn averageVolumeDataGridViewTextBoxColumn;
        private DataGridViewTextBoxColumn nameDataGridViewTextBoxColumn;
        private DataGridViewTextBoxColumn buyPriceDataGridViewTextBoxColumn;
        private DataGridViewTextBoxColumn sellPriceDataGridViewTextBoxColumn;
    }
}