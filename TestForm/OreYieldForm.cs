using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;
using Domain.Infrastructure;
using Domain.Models.ResourceDTO;
using Domain.Services;
using Loader.Infrastructure;
using Loader.Infrastructure.Api;
using Loader.Models.Api;
using Loader.Services;
using OreTools;

namespace TestForm
{
    public partial class OreYieldForm : Form
    {
        private readonly SdeAggregateDTO _sde;
        private readonly IPriceProvider _priceProvider;
        private readonly IAvgPriceProvider? _avgProvider;

        private CancellationTokenSource? _cts;
        private List<OreRow> _currentRows = new();

        private CheckBox? _chkMillions;

        private readonly string _settingsPath =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "OreYieldForm.settings.bin");

        public OreYieldForm(SdeAggregateDTO sde,
                            IPriceProvider priceProvider,
                            IAvgPriceProvider? avgProvider = null)
        {
            InitializeComponent();
            _sde = sde;
            _priceProvider = priceProvider;
            _avgProvider = avgProvider;

            TryApplyDark();
            CreateMillionsCheck();
            WireGridSorting();
            LoadSettingsSafe();
            FixColumns();
            UpdatePriceColumnHeaders();

            grid.CellDoubleClick += grid_CellDoubleClick;
        }

        private int _lastRegionId = 10000002;
        private double _lastRefineYield = 0.88;
        private void grid_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            if (grid.Rows[e.RowIndex].DataBoundItem is not OreRow row) return;

            using var dlg = new OreDetailsForm(_sde, _priceProvider, _lastRegionId, _lastRefineYield, row);
            dlg.ShowDialog(this);
        }

        private void TryApplyDark()
        {
            try { UiStyle.ApplyDark(this); } catch { }
        }

        private void CreateMillionsCheck()
        {
            _chkMillions = new CheckBox
            {
                AutoSize = true,
                Text = "Показывать цены в млн (без дроб.)",
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            _chkMillions.Location = new System.Drawing.Point(
                chkIncludeCompressed.Left, chkIncludeCompressed.Bottom + 6);

            Controls.Add(_chkMillions);
            _chkMillions.BringToFront();

            _chkMillions.CheckedChanged += (_, __) =>
            {
                UpdatePriceColumnHeaders();
                grid.Refresh();
            };

            grid.CellFormatting += (s, e) =>
            {
                if (e.Value == null) return;

                bool asMillions = _chkMillions.Checked;
                if (asMillions && e.Value is double dv)
                {
                    e.Value = (dv / 1_000_000d).ToString("N0", CultureInfo.InvariantCulture);
                    e.FormattingApplied = true;
                }
                else if (asMillions && e.Value is decimal dec)
                {
                    e.Value = (dec / 1_000_000m).ToString("N0", CultureInfo.InvariantCulture);
                    e.FormattingApplied = true;
                }
            };
        }

        private void UpdatePriceColumnHeaders()
        {
            bool asMillions = _chkMillions?.Checked == true;
            void set(string header, params string[] candidates)
            {
                var col = grid.Columns.Cast<DataGridViewColumn>()
                    .FirstOrDefault(c => candidates.Contains(c.DataPropertyName, StringComparer.OrdinalIgnoreCase)
                                      || candidates.Contains(c.Name, StringComparer.OrdinalIgnoreCase)
                                      || c.HeaderText.Equals(header, StringComparison.OrdinalIgnoreCase));
                if (col != null)
                    col.HeaderText = asMillions ? header + " (млн/ч)" : header;
            }

            set("Raw BUY ISK/час", nameof(OreRow.RawBuyIskPerHour));
            set("Raw SELL ISK/час", nameof(OreRow.RawSellIskPerHour));
            set("Refined BUY ISK/час", nameof(OreRow.RefinedBuyIskPerHour));
            set("Refined SELL ISK/час", nameof(OreRow.RefinedSellIskPerHour));
            set("Avg30d ISK/час (ср.)", nameof(OreRow.Avg30dIskPerHour));
            set("CompressedBUY/час", nameof(OreRow.CompressedBuyIskPerHour));
            set("CompressedSELL/час", nameof(OreRow.CompressedSellIskPerHour));
        }

        private void FixColumns()
        {
            grid.AutoGenerateColumns = false;
            HideColumn(nameof(OreRow.IsIce));

            var typeCol = grid.Columns.Cast<DataGridViewColumn>()
                .FirstOrDefault(c => c.HeaderText.Equals("Тип", StringComparison.OrdinalIgnoreCase)
                                  || c.Name.Equals("colType", StringComparison.OrdinalIgnoreCase));
            if (typeCol != null)
                typeCol.DataPropertyName = nameof(OreRow.TypeLabel);
        }

        private void HideColumn(string dataPropertyOrName)
        {
            var col = grid.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c =>
                c.DataPropertyName.Equals(dataPropertyOrName, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Equals(dataPropertyOrName, StringComparison.OrdinalIgnoreCase) ||
                c.HeaderText.Equals(dataPropertyOrName, StringComparison.OrdinalIgnoreCase));
            if (col != null) col.Visible = false;
        }

        private void WireGridSorting()
        {
            foreach (DataGridViewColumn c in grid.Columns)
                c.SortMode = DataGridViewColumnSortMode.Programmatic;

            grid.ColumnHeaderMouseClick += (s, e) =>
            {
                if (_currentRows == null || _currentRows.Count == 0) return;

                var col = grid.Columns[e.ColumnIndex];
                bool desc = col.HeaderCell.SortGlyphDirection != SortOrder.Descending;

                string prop = string.IsNullOrWhiteSpace(col.DataPropertyName) ? col.Name : col.DataPropertyName;

                IEnumerable<OreRow> query;
                var propInfo = typeof(OreRow).GetProperty(prop);
                if (propInfo != null)
                {
                    query = desc
                        ? _currentRows.OrderByDescending(r => propInfo.GetValue(r, null))
                        : _currentRows.OrderBy(r => propInfo.GetValue(r, null));
                }
                else query = _currentRows;

                _currentRows = query.ToList();
                grid.DataSource = null;
                grid.DataSource = _currentRows;

                foreach (DataGridViewColumn c in grid.Columns)
                    c.HeaderCell.SortGlyphDirection = SortOrder.None;
                col.HeaderCell.SortGlyphDirection = desc ? SortOrder.Descending : SortOrder.Ascending;

                FixColumns();
                UpdatePriceColumnHeaders();
            };
        }

        [Serializable]
        private sealed class FormSettings
        {
            public int Ships { get; set; }
            public int Strips { get; set; }
            public decimal M3PerStrip { get; set; }
            public int OreCycleSec { get; set; }
            public int IceCycleSec { get; set; }
            public int RefinePct { get; set; }
            public int RegionIndex { get; set; }
            public bool IncludeCompressed { get; set; }
            public bool PricesAsMillions { get; set; }
            public System.Drawing.Point Location { get; set; }
            public System.Drawing.Size Size { get; set; }
            public FormWindowState WindowState { get; set; }
        }

        private void LoadSettingsSafe()
        {
            try
            {
                if (!File.Exists(_settingsPath)) return;
                using var fs = File.OpenRead(_settingsPath);
                var bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
#pragma warning disable SYSLIB0011
                var s = (FormSettings?)bf.Deserialize(fs);
#pragma warning restore SYSLIB0011
                if (s == null) return;

                numShips.Value = Math.Max(numShips.Minimum, Math.Min(numShips.Maximum, s.Ships));
                numStrips.Value = Math.Max(numStrips.Minimum, Math.Min(numStrips.Maximum, s.Strips));
                numM3PerStrip.Value = Math.Max(numM3PerStrip.Minimum, Math.Min(numM3PerStrip.Maximum, s.M3PerStrip));
                numCycleSec.Value = Math.Max(numCycleSec.Minimum, Math.Min(numCycleSec.Maximum, s.OreCycleSec));
                numIceCycleSec.Value = Math.Max(numIceCycleSec.Minimum, Math.Min(numIceCycleSec.Maximum, s.IceCycleSec));
                numRefineYield.Value = Math.Max(numRefineYield.Minimum, Math.Min(numRefineYield.Maximum, s.RefinePct));
                if (s.RegionIndex >= 0 && s.RegionIndex < cmbRegion.Items.Count)
                    cmbRegion.SelectedIndex = s.RegionIndex;
                chkIncludeCompressed.Checked = s.IncludeCompressed;
                if (_chkMillions != null) _chkMillions.Checked = s.PricesAsMillions;

                StartPosition = FormStartPosition.Manual;
                Location = s.Location;
                Size = s.Size;
                WindowState = s.WindowState;
            }
            catch { }
        }

        private void SaveSettingsSafe()
        {
            try
            {
                var s = new FormSettings
                {
                    Ships = (int)numShips.Value,
                    Strips = (int)numStrips.Value,
                    M3PerStrip = numM3PerStrip.Value,
                    OreCycleSec = (int)numCycleSec.Value,
                    IceCycleSec = (int)numIceCycleSec.Value,
                    RefinePct = (int)numRefineYield.Value,
                    RegionIndex = cmbRegion.SelectedIndex,
                    IncludeCompressed = chkIncludeCompressed.Checked,
                    PricesAsMillions = _chkMillions?.Checked ?? false,
                    Location = this.WindowState == FormWindowState.Normal ? this.Location : this.RestoreBounds.Location,
                    Size = this.WindowState == FormWindowState.Normal ? this.Size : this.RestoreBounds.Size,
                    WindowState = this.WindowState
                };

                using var fs = File.Open(_settingsPath, FileMode.Create, FileAccess.Write);
                var bf = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
#pragma warning disable SYSLIB0011
                bf.Serialize(fs, s);
#pragma warning restore SYSLIB0011
            }
            catch { }
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            SaveSettingsSafe();
            base.OnFormClosing(e);
        }

        private async void btnCalc_Click(object? sender, EventArgs e)
        {
            btnCalc.Enabled = false;
            progress.Value = 0;
            statusLabel.Text = "Сбор цен...";

            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            try
            {
                int regionId = ParseRegionIdFromCombo(cmbRegion);
                double ships = (double)numShips.Value;
                double strips = (double)numStrips.Value;
                double m3PerStrip = (double)numM3PerStrip.Value;
                double oreCycleSec = (double)numCycleSec.Value;
                double iceCycleSec = (double)numIceCycleSec.Value;
                double refineYield = ((double)numRefineYield.Value) / 100.0;
                bool includeCompressed = chkIncludeCompressed.Checked;

                _lastRegionId = regionId;
                _lastRefineYield = refineYield;

                var miningItems = _sde.Items.Values
                    .Where(x => x.Published)
                    .Where(i => (i.Kind == Kind.Ore || i.Kind == Kind.IceOre) && !i.IsCompressed)
                    .ToList();

                var neededTypeIds = new HashSet<int>(miningItems.Select(o => o.TypeID));
                foreach (var it in miningItems)
                {
                    if (_sde.Reprocessing.TryGetValue(it.TypeID, out var plan))
                        foreach (var outp in plan.Outputs)
                            neededTypeIds.Add(outp.TypeID);
                }

                // --- ВЫБОР ЛУЧШЕЙ СЖАТОЙ: по партии рефайна СЖАТОЙ руды ---
                // --- ВЫБОР ЛУЧШЕЙ СЖАТОЙ: приоритет тем, у кого рефайн-порция 100 ---
                // и правильное отображение UPC (1 для portion=100, 100 для portion=1)
                var bestCompressedByInput = new Dictionary<int, (int outType, string name, double upc)>();

                foreach (var it in miningItems)
                {
                    if (!_sde.Compression.TryGetValue(it.TypeID, out var opts) || opts.Count == 0)
                        continue;

                    var candidates = new List<(int outType, string name, double upc, int compPortion)>();

                    foreach (var o in opts)
                    {
                        int compPortion = 0;
                        if (_sde.Reprocessing.TryGetValue(o.OutTypeID, out var compPlan))
                            compPortion = Math.Max(1, compPlan.PortionSize);

                        double mappedUpc;
                        if (it.Kind == Kind.IceOre) mappedUpc = 1;                 // лёд 1:1
                        else if (compPortion == 100) mappedUpc = 1;                // Compressed ...  -> 1:1
                        else if (compPortion == 1) mappedUpc = 100;                // Batch ...       -> 100:1
                        else mappedUpc = o.UnitsPerCompressed > 0 ? o.UnitsPerCompressed : 1;

                        candidates.Add((o.OutTypeID, o.OutName, mappedUpc, compPortion));
                    }

                    if (candidates.Count == 0) continue;

                    // приоритет: сначала все с компортией 100; если таких нет — берём с наибольшим UPC
                    var best = candidates.FirstOrDefault(c => c.compPortion == 100);
                    if (best.outType == 0)
                        best = candidates.OrderByDescending(c => c.upc).First();

                    bestCompressedByInput[it.TypeID] = (best.outType, best.name, best.upc);

                    // цены тоже понадобятся
                    neededTypeIds.Add(best.outType);
                }

                var prices = await _priceProvider.GetBestPricesAsync(neededTypeIds, regionId, ct);

                progress.Value = 50;
                statusLabel.Text = "Расчёт...";

                var oreCyclesPerHour = 3600.0 / oreCycleSec;
                var oreM3PerHour = ships * strips * m3PerStrip * oreCyclesPerHour;

                var iceCyclesPerHour = 3600.0 / iceCycleSec;
                var iceUnitsPerHourBase = ships * strips * iceCyclesPerHour;

                var rows = new List<OreRow>(miningItems.Count);
                foreach (var it in miningItems)
                {
                    bool isIce = it.Kind == Kind.IceOre;

                    double unitsPerHour = isIce
                        ? iceUnitsPerHourBase
                        : (oreM3PerHour / Math.Max(0.0001, it.Volume));

                    prices.TryGetValue(it.TypeID, out var orePx);
                    double buyOre = orePx.bestBuy * unitsPerHour;
                    double sellOre = orePx.bestSell * unitsPerHour;

                    double? avgUnit = _avgProvider?.TryGetAvgPrice(it.TypeID);
                    double avgIsk = avgUnit.HasValue ? avgUnit.Value * unitsPerHour : 0;

                    double refinedBuy = 0, refinedSell = 0;
                    if (_sde.Reprocessing.TryGetValue(it.TypeID, out var plan))
                    {
                        var portion = Math.Max(1, plan.PortionSize);
                        foreach (var outp in plan.Outputs)
                        {
                            var perUnit = (outp.Quantity / (double)portion) * refineYield;
                            var perHour = perUnit * unitsPerHour;
                            if (prices.TryGetValue(outp.TypeID, out var p))
                            {
                                refinedBuy += p.bestBuy * perHour;
                                refinedSell += p.bestSell * perHour;
                            }
                        }
                    }

                    int? compType = null;
                    string? compName = null;
                    double? upc = null;
                    double compBuy = 0, compSell = 0;

                    if (includeCompressed && bestCompressedByInput.TryGetValue(it.TypeID, out var best))
                    {
                        compType = best.outType;
                        compName = best.name;
                        upc = best.upc;

                        var compressedUnitsPerHour = unitsPerHour / Math.Max(1.0, upc.Value);
                        if (prices.TryGetValue(compType.Value, out var cp))
                        {
                            compBuy = cp.bestBuy * compressedUnitsPerHour;
                            compSell = cp.bestSell * compressedUnitsPerHour;
                        }
                    }

                    rows.Add(new OreRow
                    {
                        TypeID = it.TypeID,
                        Name = it.Name,
                        IsIce = isIce,
                        UnitVolume = it.Volume,
                        UnitsPerHour = unitsPerHour,
                        RawBuyIskPerHour = buyOre,
                        RawSellIskPerHour = sellOre,
                        RefinedBuyIskPerHour = refinedBuy,
                        RefinedSellIskPerHour = refinedSell,
                        Avg30dIskPerHour = avgIsk,

                        CompressedTypeID = compType,
                        CompressedName = compName,
                        UnitsPerCompressed = upc,
                        CompressedBuyIskPerHour = compBuy,
                        CompressedSellIskPerHour = compSell
                    });
                }

                _currentRows = rows
                    .OrderByDescending(r => r.RefinedSellIskPerHour)
                    .ThenByDescending(r => r.RawSellIskPerHour)
                    .ToList();

                grid.AutoGenerateColumns = false;
                grid.DataSource = _currentRows;
                FixColumns();
                UpdatePriceColumnHeaders();

                statusLabel.Text = $"Готово: {_currentRows.Count} позиций.";
                progress.Value = 100;
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
                btnCalc.Enabled = true;
            }
        }

        private static int ParseRegionIdFromCombo(ComboBox cmb)
        {
            if (cmb.SelectedItem is string s)
            {
                int lb = s.LastIndexOf('(');
                int rb = s.LastIndexOf(')');
                if (lb >= 0 && rb > lb && int.TryParse(s.Substring(lb + 1, rb - lb - 1), out int id))
                    return id;
            }
            return 10000002;
        }
    }

    public interface IAvgPriceProvider { double? TryGetAvgPrice(int typeId); }

    public sealed class HistoryAvgProvider : IAvgPriceProvider
    {
        private readonly Dictionary<int, double> _avgByType = new();

        public HistoryAvgProvider()
        {
            try
            {
                var cache = new BinaryCachingService();
                if (cache.TryLoad<ANALYTICS.OrderHistoryMonthList>(Paths.OrderHistoryMonthPath, out var data, out var res) && data != null)
                {
                    foreach (var it in data.List)
                    {
                        if (it.AverageVolume > 0)
                        {
                            var avg = it.Rating / it.AverageVolume;
                            _avgByType[it.TypeId] = avg;
                        }
                    }
                }
            }
            catch { }
        }

        public double? TryGetAvgPrice(int typeId)
            => _avgByType.TryGetValue(typeId, out var v) ? v : (double?)null;
    }
}
