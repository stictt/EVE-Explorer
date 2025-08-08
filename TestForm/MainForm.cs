using ANALYTICS;
using System;
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

        public MainForm()
        {
            InitializeComponent();

            _settings = SettingsService.Load();
            _historyManager = new OrderHistoryMonthManager(_settings, Log);

            UiStyle.ApplyDark(this);
            UpdateHistoryLabels();
        }

        private void UpdateHistoryLabels()
        {
            var txt = _settings.OrderHistoryMonthGeneratedUtc is DateTime dt
                ? dt.ToLocalTime().ToString("dd.MM.yyyy HH:mm")
                : "нет";

            lblHistory.Text = $"История: {txt}";
            if (lblHistoryInfo != null) lblHistoryInfo.Text = $"Последняя генерация: {txt}";
        }

        private void Log(string msg)
        {
            statusLabel.Text = msg;
        }

        private void btnOpenArbitrage_Click(object? sender, EventArgs e)
        {
            var form = new Form1(); // твоя тестовая форма отчёта
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
                {
                    Log($"История обновлена. Типов: {res.Items}");
                }
                else
                {
                    Log("История не обновлена" + (string.IsNullOrEmpty(res.Error) ? "." : $": {res.Error}"));
                }

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


        private void btnCancel_Click(object? sender, EventArgs e)
        {
            _cts?.Cancel();
        }

        private void btnSettings_Click(object? sender, EventArgs e)
        {
            // простой диалог настроек (минимум)
            using var f = new Form();
            f.Text = "Настройки";
            f.StartPosition = FormStartPosition.CenterParent;

            var tbRegion = new TextBox { Text = _settings.RegionId.ToString(), Dock = DockStyle.Top };
            var tbPar = new TextBox { Text = _settings.Parallelism.ToString(), Dock = DockStyle.Top };
            var tbStale = new TextBox { Text = _settings.HistoryStaleAfterHours.ToString(), Dock = DockStyle.Top };
     

            var btnOk = new Button { Text = "OK", Dock = DockStyle.Bottom };
            btnOk.Click += (s, ev) =>
            {
                if (int.TryParse(tbRegion.Text, out var r)) _settings.RegionId = r;
                if (int.TryParse(tbPar.Text, out var p)) _settings.Parallelism = p;
                if (int.TryParse(tbStale.Text, out var h)) _settings.HistoryStaleAfterHours = h;

                SettingsService.Save(_settings);
                UpdateHistoryLabels();
                f.DialogResult = DialogResult.OK;
                f.Close();
            };

            var panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            panel.Controls.AddRange(new Control[]
            {
                new Label{Text="Регион Id"}, tbRegion,
                new Label{Text="Параллелизм"}, tbPar,
                new Label{Text="Устаревание (ч)"}, tbStale
            });

            f.Controls.Add(panel);
            f.Controls.Add(btnOk);
            f.Width = 520; f.Height = 260;
            f.ShowDialog(this);
        }
    }
}
