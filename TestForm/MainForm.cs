using ANALYTICS;
using Domain.Infrastructure;
using Domain.Models.ResourceDTO;
using Domain.Services;
using Loader.Infrastructure;
using Loader.Services;
using OreTools;
using System;
using System.Drawing;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TestForm
{
    public partial class MainForm : Form
    {
        private AppSettings _settings;
        private OrderHistoryMonthManager _historyManager;
        private CancellationTokenSource? _cts;

        private CsvService _csv;
        private SdeAggregateDTO? _sde;

        public MainForm()
        {
            InitializeComponent();

            _settings = SettingsService.Load();
            EnsureDefaults();
            _historyManager = new OrderHistoryMonthManager(_settings, Log);

            _csv = new CsvService(new CSVMapService(), new LoggerFactoryBase().CreateLogger<CsvService>());

            UiStyle.ApplyDark(this);
            UpdateHistoryLabels();
        }

        private void EnsureDefaults()
        {

        }

        private void UpdateHistoryLabels()
        {
            var txt = _settings.OrderHistoryMonthGeneratedUtc is DateTime dt
                ? dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
                : "нет";

            lblHistory.Text = $"История: {txt}";
            if (lblHistoryInfo != null) lblHistoryInfo.Text = $"Последняя генерация: {txt}";
        }

        private void Log(string msg) => statusLabel.Text = msg;

        private void btnOpenArbitrage_Click(object? sender, EventArgs e)
        {
            var form = new Form1();
            form.Show(this);
        }

        private async void btnBuildHistory_Click(object? sender, EventArgs e)
        {
            btnBuildHistory.Enabled = false;
            btnCancel.Enabled = true;
            toolStripProgress.Value = 0;

            _cts = new CancellationTokenSource();

            var progress = new Progress<OrderHistoryMonthManager.ProgressInfo>(p =>
            {
                statusLabel.Text = $"{p.Phase}: {p.Done}/{p.Total}";
                toolStripProgress.Maximum = Math.Max(1, p.Total);
                toolStripProgress.Value = Math.Min(p.Done, p.Total);
            });

            try
            {
                var res = await _historyManager.BuildAsync(progress, _cts.Token);
                if (res.Updated)
                    Log($"История обновлена. Типов: {res.Items}");
                else
                    Log("История не обновлена" + (string.IsNullOrEmpty(res.Error) ? "." : $": {res.Error}"));

                UpdateHistoryLabels();
            }
            catch (OperationCanceledException)
            {
                Log("Отменено.");
            }
            catch (Exception ex)
            {
                Log("Ошибка: " + ex.Message);
            }
            finally
            {
                toolStripProgress.Value = 0;
                btnBuildHistory.Enabled = true;
                btnCancel.Enabled = false;
                _cts?.Dispose();
                _cts = null;
            }
        }

        private void btnCancel_Click(object? sender, EventArgs e) => _cts?.Cancel();

        private void btnSettings_Click(object? sender, EventArgs e)
        {
            using var f = new Form();
            f.Text = "Настройки";
            f.StartPosition = FormStartPosition.CenterParent;
            f.MinimizeBox = false;
            f.MaximizeBox = false;
            f.FormBorderStyle = FormBorderStyle.FixedDialog;
            f.Width = 560;
            f.Height = 360;

            var tbRegion = new TextBox { Text = _settings.RegionId.ToString(CultureInfo.InvariantCulture), Dock = DockStyle.Top };
            var tbPar = new TextBox { Text = _settings.Parallelism.ToString(CultureInfo.InvariantCulture), Dock = DockStyle.Top };
            var tbStale = new TextBox { Text = _settings.HistoryStaleAfterHours.ToString(CultureInfo.InvariantCulture), Dock = DockStyle.Top };

            var nudTax = new NumericUpDown
            {
                DecimalPlaces = 2,
                Increment = 0.10M,
                Minimum = 0,
                Maximum = 100,

                Dock = DockStyle.Top
            };
            var nudRefOre = new NumericUpDown
            {
                DecimalPlaces = 2,
                Increment = 0.10M,
                Minimum = 0,
                Maximum = 100,

                Dock = DockStyle.Top
            };
            var nudRefIce = new NumericUpDown
            {
                DecimalPlaces = 2,
                Increment = 0.10M,
                Minimum = 0,
                Maximum = 100,

                Dock = DockStyle.Top
            };

            var btnOk = new Button { Text = "OK", Dock = DockStyle.Bottom, Height = 36 };
            btnOk.Click += (s, ev) =>
            {
                if (int.TryParse(tbRegion.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)) _settings.RegionId = r;
                if (int.TryParse(tbPar.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var p)) _settings.Parallelism = p;
                if (int.TryParse(tbStale.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var h)) _settings.HistoryStaleAfterHours = h;


                SettingsService.Save(_settings);
                UpdateHistoryLabels();
                f.DialogResult = DialogResult.OK;
                f.Close();
            };

            var panel = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                Padding = new Padding(10),
                ColumnCount = 2,
                RowCount = 6
            };
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55));
            panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45));
            panel.RowStyles.Clear();
            for (int i = 0; i < 6; i++) panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

            void addRow(string label, Control ctl)
            {
                var lbl = new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left, Padding = new Padding(0, 6, 0, 0) };
                panel.Controls.Add(lbl);
                panel.Controls.Add(ctl);
            }

            addRow("Регион Id (по умолчанию Jita=10000002):", tbRegion);
            addRow("Параллелизм запросов:", tbPar);
            addRow("Устаревание истории (часы):", tbStale);
            addRow("Налог реакций, %:", nudTax);
            addRow("Дефолтный рефайн руды, %:", nudRefOre);
            addRow("Дефолтный рефайн льда, %:", nudRefIce);

            f.Controls.Add(panel);
            f.Controls.Add(btnOk);

            try { UiStyle.ApplyDark(f); } catch { }
            f.ShowDialog(this);
        }

        private async void btnOpenOre_Click(object? sender, EventArgs e)
        {
            try
            {
                UseWaitCursor = true;
                statusLabel.Text = "Загрузка SDE...";

                _sde ??= _csv.BuildSdeAggregate();

                var priceProvider = new EsiPriceProvider(s => Log(s));
                var avgProvider = new HistoryAvgProvider();

                using var f = new OreYieldForm(_sde, priceProvider, avgProvider);
                f.ShowDialog(this);

                statusLabel.Text = "Готово";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Ошибка: " + ex.Message;
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        // --------- NEW: открыть окно аналитики лунных реакций ----------
        private void btnOpenMoon_Click(object? sender, EventArgs e)
        {
            try
            {
                UseWaitCursor = true;
                statusLabel.Text = "Загрузка SDE...";

                _sde ??= _csv.BuildSdeAggregate();

                // Если у твоего окна другой конструктор — поправь ниже.
                var priceProvider = new EsiPriceProvider(s => Log(s));
                var avgProvider = new HistoryAvgProvider();

                using var f = new LunarProfitForm(_sde, priceProvider, avgProvider);
                f.ShowDialog(this);

                statusLabel.Text = "Готово";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Ошибка: " + ex.Message;
            }
            finally
            {
                UseWaitCursor = false;
            }
        }
    }
}
