using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TestForm
{
    public partial class Form1 : Form
    {
        private readonly BindingList<Model> _view = new BindingList<Model>();
        private List<Model> _source = new List<Model>();
        private CancellationTokenSource? _cts;

        public Form1()
        {
            InitializeComponent();

            // стиль тёмный (из нашего UiStyle)
            UiStyle.ApplyDark(FindMainFormOrSelf());
            StyleGridDark(dataGridView1);

            dataGridView1.AutoGenerateColumns = false;
            dataGridView1.DataSource = _view;

            // двойная буферизация, чтобы не мерцало
            EnableDoubleBuffer(dataGridView1);

            // начальная загрузка по желанию
            // _ = LoadDataAsync();
        }

        private Form FindMainFormOrSelf()
            => this.Owner as MainForm ?? this as Form;

        private async Task LoadDataAsync()
        {
            btnRefresh.Enabled = false;
            txtSearch.Enabled = false;
            cmbMode.Enabled = false;

            statusProgress.Visible = true;
            statusProgress.Style = ProgressBarStyle.Marquee;
            statusLabel.Text = "Загрузка отчёта...";

            _cts = new CancellationTokenSource();

            try
            {
                var svc = new TestAnalitic();
                var items = await svc.GetOrder(); // твой метод
                _source = items?.ToList() ?? new List<Model>();
                ApplyFilter();
                statusLabel.Text = $"Готово. Позиции: {_view.Count}";
            }
            catch (OperationCanceledException)
            {
                statusLabel.Text = "Отменено.";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Ошибка: " + ex.Message;
            }
            finally
            {
                statusProgress.Visible = false;
                statusProgress.Style = ProgressBarStyle.Blocks;

                btnRefresh.Enabled = true;
                txtSearch.Enabled = true;
                cmbMode.Enabled = true;

                _cts?.Dispose();
                _cts = null;
            }
        }

        private void ApplyFilter()
        {
            var query = (txtSearch.Text ?? "").Trim();
            var mode = (cmbMode.SelectedItem as string) ?? "Арбитраж";

            IEnumerable<Model> data = _source;

            if (!string.IsNullOrEmpty(query))
            {
                if (int.TryParse(query, out var id))
                    data = data.Where(m => GetTypeId(m) == id);
                else
                    data = data.Where(m => (m.Name ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0);
            }

            // простые режимы сортировки как пример
            data = mode switch
            {
                "Топ объём" => data.OrderByDescending(m => m.averageVolume),
                "Маржа %" => data.OrderByDescending(m => m.Margin),
                _ => data.OrderByDescending(m => m.Margin)
            };

            _view.RaiseListChangedEvents = false;
            _view.Clear();
            foreach (var x in data)
                _view.Add(x);
            _view.RaiseListChangedEvents = true;
            _view.ResetBindings();
        }

        private static int GetTypeId(Model m)
        {
            // если в Model нет TypeId — вернёт 0. Добавь поле в Model, и будет ок.
            var prop = m.GetType().GetProperty("TypeId");
            if (prop != null && prop.PropertyType == typeof(int))
                return (int)(prop.GetValue(m) ?? 0);
            return 0;
        }

        // UI handlers
        private async void btnRefresh_Click(object? sender, EventArgs e) => await LoadDataAsync();
        private void txtSearch_TextChanged(object? sender, EventArgs e) => ApplyFilter();
        private void cmbMode_SelectedIndexChanged(object? sender, EventArgs e) => ApplyFilter();

        // двойной клик — хук на будущую форму «рынок по TypeID»
        private void dataGridView1_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (dataGridView1.Rows[e.RowIndex].DataBoundItem is not Model m) return;

            var typeId = GetTypeId(m);
            if (typeId == 0)
            {
                MessageBox.Show("В модели нет поля TypeId. Добавь его, чтобы открывать рынок по позиции.",
                    "Нет TypeId", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // TODO: открой MarketDetailsForm (когда добавим)
            MessageBox.Show($"Открыть рынок по TypeID={typeId} (в разработке).",
                "Маркет", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void dataGridView1_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter && dataGridView1.CurrentRow != null)
            {
                e.Handled = e.SuppressKeyPress = true;
                dataGridView1_CellDoubleClick(sender, new DataGridViewCellEventArgs(0, dataGridView1.CurrentRow.Index));
            }
        }

        // стилизация грида под тёмную тему
        private static void StyleGridDark(DataGridView g)
        {
            g.BackgroundColor = Color.FromArgb(18, 22, 28);
            g.GridColor = Color.FromArgb(55, 65, 81);
            g.DefaultCellStyle.BackColor = Color.FromArgb(28, 34, 45);
            g.DefaultCellStyle.ForeColor = Color.FromArgb(230, 237, 243);
            g.DefaultCellStyle.SelectionBackColor = Color.FromArgb(48, 56, 72);
            g.DefaultCellStyle.SelectionForeColor = Color.White;
            g.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(28, 34, 45);
            g.ColumnHeadersDefaultCellStyle.ForeColor = Color.Gainsboro;
        }

        private static void EnableDoubleBuffer(DataGridView grid)
        {
            grid.GetType()
                .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
                .SetValue(grid, true, null);
        }
    }
}
