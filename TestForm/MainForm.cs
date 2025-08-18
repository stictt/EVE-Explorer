using ANALYTICS;
using Domain.Infrastructure;
using Domain.Models.BaseResourceModels;
using Domain.Models.ResourceDTO;
using Domain.Services;
using Loader.Infrastructure;
using Loader.Services;
using OreTools;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
// NEW:
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using TestForm;               // IPriceProvider, EsiPriceProvider
using TestForm.Infrastructure; // PlanetaryAnalysisForm, ProductionPlanner, SdePiRepository, PiConfigProvider, DTOs

namespace TestForm
{
    public partial class MainForm : Form
    {
        private AppSettings _settings;
        private OrderHistoryMonthManager _historyManager;
        private CancellationTokenSource? _cts;

        private CsvService _csv;
        private SdeAggregateDTO? _sde;

        // NEW: PI singletons (lazy)
        private IPiConfigProvider? _piCfg;
        private ISdePiRepository? _piRepo;
        private IProductionPlanner? _piPlanner;
        private PlanetaryAnalysisForm? _piForm;

        public MainForm()
        {
            InitializeComponent();

            _settings = SettingsService.Load();
            EnsureDefaults();
            _historyManager = new OrderHistoryMonthManager(_settings, Log);

            _csv = new CsvService(new CSVMapService());

            UiStyle.ApplyDark(this);
            UpdateHistoryLabels();
        }

        private void EnsureDefaults()
        {
            // при необходимости — инициализация дефолтов приложения
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

        // --------- Лунные реакции ----------
        private void btnOpenMoon_Click(object? sender, EventArgs e)
        {
            try
            {
                UseWaitCursor = true;
                statusLabel.Text = "Загрузка SDE...";

                _sde ??= _csv.BuildSdeAggregate();

                var priceProvider = new EsiPriceProvider(s => Log(s));
                var avgProvider = new HistoryAvgProvider();

                using var f = new MoonIndustryForm(_sde, priceProvider);
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

        // --------- NEW: Планетарка (PI) ----------
        private void btnOpenPi_Click(object? sender, EventArgs e)
        {
            try
            {
                UseWaitCursor = true;
                statusLabel.Text = "Инициализация PI...";

                EnsurePiInitialized(); // ленивый одноразовый init

                // Цены — как в других разделах (можно поставить null, тогда кнопка Get Prices будет неактивна)
                IPriceProvider? priceProvider = new EsiPriceProvider(s => Log(s));

                if (_piPlanner == null)
                    throw new InvalidOperationException("_piPlanner не инициализирован");
                // _piRepo можно не передавать, но без него не будет имён — мы передаём.

                if (_piForm == null || _piForm.IsDisposed)
                {
                    _piForm = new PlanetaryAnalysisForm(_piPlanner, _piRepo, priceProvider);
                    _piForm.FormClosed += (_, __) => _piForm = null;
                    _piForm.Show(this);          // modeless; нужно модальное — поменяй на ShowDialog(this)
                }
                else
                {
                    _piForm.BringToFront();
                    _piForm.Focus();
                }

                statusLabel.Text = "Готово";
            }
            catch (Exception ex)
            {
                statusLabel.Text = "Ошибка: " + ex.Message;
                MessageBox.Show(this, ex.ToString(), "PI init error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                UseWaitCursor = false;
            }
        }

        // =========================
        // PI init (минимальный, без Bootstrap)
        // =========================
        private void EnsurePiInitialized()
        {
            if (_piPlanner != null) return;

            // 1) Конфиг
            if (!File.Exists(Paths.PiConfigPath))
                throw new FileNotFoundException("Не найден pi.config.json", Paths.PiConfigPath);

            _piCfg = new PiConfigProvider(Paths.PiConfigPath);

            // 2) CSV через стандартный CsvDataReader<T>
            if (!File.Exists(Paths.DataInvTypesPath))
                throw new FileNotFoundException("Не найден invTypes.csv", Paths.DataInvTypesPath);
            if (!File.Exists(Paths.PlanetSchematicsPath))
                throw new FileNotFoundException("Не найден planetSchematics.csv", Paths.PlanetSchematicsPath);
            if (!File.Exists(Paths.PlanetSchematicsTypeMapPath))
                throw new FileNotFoundException("Не найден planetSchematicsTypeMap.csv", Paths.PlanetSchematicsTypeMapPath);
            if (!File.Exists(Paths.PlanetSchematicsPinMapPath))
                throw new FileNotFoundException("Не найден planetSchematicsPinMap.csv", Paths.PlanetSchematicsPinMapPath);

            var invTypes = new CsvDataReader<InvType>(Paths.DataInvTypesPath).Read();
            var schem = new CsvDataReader<PlanetSchematic>(Paths.PlanetSchematicsPath).Read();
            var typeMap = new CsvDataReader<PlanetSchematicTypeMap>(Paths.PlanetSchematicsTypeMapPath).Read();
            var pinMap = new CsvDataReader<PlanetSchematicPinMap>(Paths.PlanetSchematicsPinMapPath).Read();

            // 3) Нормализация и защита от дублей/битых строк
            invTypes = invTypes
                .Where(x => x.TypeID > 0)
                .GroupBy(x => x.TypeID)
                .Select(g => g.First())
                .ToList();

            schem = schem
                .Where(x => x.SchematicID > 0)
                .GroupBy(x => x.SchematicID)
                .Select(g => g.First())
                .ToList();

            typeMap = typeMap
                .Where(x => x.SchematicID > 0 && x.TypeID > 0)
                .ToList();

            pinMap = pinMap
                .Where(x => x.SchematicID > 0 && x.PinTypeID > 0)
                .GroupBy(x => new { x.SchematicID, x.PinTypeID })
                .Select(g => g.First())
                .ToList();

            // 4) Репозиторий + Планировщик
            _piRepo = new SdePiRepository(_piCfg, invTypes, schem, typeMap, pinMap);
            _piPlanner = new ProductionPlanner(_piRepo, _piCfg);
        }

        // =========================
        // Простые CSV-лоадеры для PI (без внешних зависимостей)
        // =========================
        private static List<InvType> LoadInvTypes(string path)
        {
            var rows = ReadCsv(path);
            return rows.Select(r => new InvType
            {
                TypeID = GetInt(r, "typeID"),
                TypeName = GetStr(r, "typeName")
            }).ToList();
        }

        private static List<PlanetSchematic> LoadPlanetSchematics(string path)
        {
            var rows = ReadCsv(Paths.PlanetSchematicsPath);
            return rows.Select(r => new PlanetSchematic
            {
                SchematicID = GetInt(r, "schematicID"),
                CycleTimeSeconds = GetInt(r, "cycleTime")
            }).ToList();
        }

        private static List<PlanetSchematicTypeMap> LoadPlanetSchematicTypeMap(string path)
        {
            var rows = ReadCsv(Paths.PlanetSchematicsTypeMapPath);
            return rows.Select(r => new PlanetSchematicTypeMap
            {
                SchematicID = GetInt(r, "schematicID"),
                TypeID = GetInt(r, "typeID"),
                Quantity = GetInt(r, "quantity"),
                IsInput = GetBool(r, "isInput") || GetInt(r, "isInput") == 1
            }).ToList();
        }

        private static List<PlanetSchematicPinMap> LoadPlanetSchematicPinMap(string path)
        {
            var rows = ReadCsv(Paths.PlanetSchematicsPinMapPath);
            return rows.Select(r => new PlanetSchematicPinMap
            {
                SchematicID = GetInt(r, "schematicID"),
                PinTypeID = GetInt(r, "pinTypeID")
            }).ToList();
        }

        // --- CSV helpers (разделитель — запятая; если у тебя ';', замени SplitCsvLine) ---
        private static List<Dictionary<string, string>> ReadCsv(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException("CSV не найден", path);
            var lines = File.ReadAllLines(path).Where(l => !string.IsNullOrWhiteSpace(l)).ToList();
            if (lines.Count == 0) return new();

            var header = SplitCsvLine(lines[0]).Select(h => h.Trim()).ToArray();
            var list = new List<Dictionary<string, string>>(lines.Count - 1);

            for (int i = 1; i < lines.Count; i++)
            {
                var cells = SplitCsvLine(lines[i]);
                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int c = 0; c < header.Length && c < cells.Count; c++)
                    dict[header[c]] = cells[c];
                list.Add(dict);
            }
            return list;
        }

        private static List<string> SplitCsvLine(string line)
        {
            var res = new List<string>();
            if (string.IsNullOrEmpty(line)) { res.Add(""); return res; }
            bool inQuotes = false;
            var cur = new System.Text.StringBuilder();
            for (int i = 0; i < line.Length; i++)
            {
                char ch = line[i];
                if (ch == '"')
                {
                    if (inQuotes && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                    else inQuotes = !inQuotes;
                }
                else if (ch == ',' && !inQuotes)
                {
                    res.Add(cur.ToString().Trim());
                    cur.Clear();
                }
                else cur.Append(ch);
            }
            res.Add(cur.ToString().Trim());
            return res;
        }

        private static int GetInt(Dictionary<string, string> r, string k)
            => r.TryGetValue(k, out var v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out var x) ? x : 0;

        private static string GetStr(Dictionary<string, string> r, string k)
            => r.TryGetValue(k, out var v) ? v : "";

        private static bool GetBool(Dictionary<string, string> r, string k)
            => r.TryGetValue(k, out var v) && (v.Equals("true", StringComparison.OrdinalIgnoreCase) || v == "1");
    }
}
