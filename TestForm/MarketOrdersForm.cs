using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Loader.Infrastructure;
using Loader.Infrastructure.Api;
using Loader.Models.Api;
using Loader.Services;

namespace OreTools
{
    /// <summary>
    /// Просмотр актуальных ордеров по типу. Две таблицы: SELL и BUY.
    /// </summary>
    public sealed class MarketOrdersForm : Form
    {
        private readonly int[] _systemFilter = new[] { 30000142, 30000144 }; // Jita, Perimeter
        private readonly int _typeId;
        private readonly string _typeName;
        private readonly int _regionId;
        private readonly Action<string> _log;

        private CancellationTokenSource? _cts;

        private Label lblTitle;
        private Label lblStatus;

        private DataGridView gridSell;
        private DataGridView gridBuy;

        private SplitContainer split;

        public MarketOrdersForm(int typeId, string typeName, int regionId, Action<string>? log = null)
        {
            _typeId = typeId;
            _typeName = typeName;
            _regionId = regionId;
            _log = log ?? (_ => { });

            InitializeComponent();
            TryApplyDark();

            Shown += async (_, __) => await LoadOrdersAsync();
            FormClosed += (_, __) => _cts?.Cancel();
        }

        private void TryApplyDark()
        {
            try { UiStyle.ApplyDark(this); }
            catch { /* если нет темы - просто пропустим */ }
        }

        #region UI

        private void InitializeComponent()
        {
            Text = "Рыночные ордера";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(900, 600);

            lblTitle = new Label
            {
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 6, 10, 4),
                Height = 28,
                Font = new Font(Font, FontStyle.Bold)
            };

            lblStatus = new Label
            {
                Dock = DockStyle.Top,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 10, 6),
                Height = 22
            };

            split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterWidth = 6
            };
            // держим панели строго 50/50
            split.Resize += (_, __) =>
            {
                if (split.Height > 0)
                    split.SplitterDistance = split.Height / 2;
            };

            // SELL
            var pnlSellHeader = new Panel { Dock = DockStyle.Top, Height = 22 };
            var lblSell = new Label
            {
                Text = "SELL (дешевле — выше)",
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 220,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            pnlSellHeader.Controls.Add(lblSell);

            gridSell = MakeGrid();
            var pnlSell = new Panel { Dock = DockStyle.Fill };
            pnlSell.Controls.Add(gridSell);
            pnlSell.Controls.Add(pnlSellHeader);

            // BUY
            var pnlBuyHeader = new Panel { Dock = DockStyle.Top, Height = 22 };
            var lblBuy = new Label
            {
                Text = "BUY (дороже — выше)",
                Dock = DockStyle.Left,
                AutoSize = false,
                Width = 220,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10, 0, 0, 0)
            };
            pnlBuyHeader.Controls.Add(lblBuy);

            gridBuy = MakeGrid();
            var pnlBuy = new Panel { Dock = DockStyle.Fill };
            pnlBuy.Controls.Add(gridBuy);
            pnlBuy.Controls.Add(pnlBuyHeader);

            split.Panel1.Controls.Add(pnlSell);
            split.Panel2.Controls.Add(pnlBuy);

            Controls.Add(split);
            Controls.Add(lblStatus);
            Controls.Add(lblTitle);
        }

        private DataGridView MakeGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                AutoGenerateColumns = false
            };

            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.Price),
                HeaderText = "Цена",
                Width = 110,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.VolumeRemain),
                HeaderText = "Остаток",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.MinVolume),
                HeaderText = "Мин.лот",
                Width = 70,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.VolumeTotal),
                HeaderText = "Всего",
                Width = 90,
                DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Format = "N0" }
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.SystemId),
                HeaderText = "Система",
                Width = 80
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.LocationId),
                HeaderText = "Локация",
                Width = 120
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.Range),
                HeaderText = "Радиус",
                Width = 70
            });
            g.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = nameof(OrderRow.Expires),
                HeaderText = "Истекает",
                Width = 130,
                DefaultCellStyle = new DataGridViewCellStyle { Format = "yyyy-MM-dd HH:mm" }
            });

            return g;
        }

        #endregion

        private async Task LoadOrdersAsync()
        {
            _cts?.Cancel();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            lblTitle.Text = $"{_typeName} [{_typeId}] — Region: {_regionId}";
            lblStatus.Text = "Загрузка ордеров...";

            try
            {
                // Request: только нужный typeId в выбранном регионе
                var builder = new MarketOrderByIdBuilder(
                    parallelCount: 10,
                    regionsId: new List<int> { _regionId },
                    typeId: new List<int> { _typeId },
                    loggerFactory: new LoggerFactoryBase(_log));

                var scheduler = new RequestApiScheduler<OrderApi>(new LoggerFactoryBase(_log), builder);
                var orders = await scheduler.Start(); // IEnumerable<OrderApi>

                var typed = orders
                    .Where(o => o.TypeId == _typeId)
                    .Where(o => _systemFilter.Contains(o.SystemId))
                    .ToList();

                var sell = typed.Where(o => !o.IsBuyOrder)
                                .OrderBy(o => (double)o.Price)
                                .Select(Map)
                                .ToList();

                var buy = typed.Where(o => o.IsBuyOrder)
                               .OrderByDescending(o => (double)o.Price)
                               .Select(Map)
                               .ToList();

                gridSell.DataSource = sell;
                gridBuy.DataSource = buy;

                lblStatus.Text = $"SELL: {sell.Count:N0} | BUY: {buy.Count:N0}";
            }
            catch (OperationCanceledException)
            {
                lblStatus.Text = "Отменено.";
            }
            catch (Exception ex)
            {
                lblStatus.Text = "Ошибка загрузки: " + ex.Message;
            }
        }

        private static OrderRow Map(OrderApi o)
        {
            // issued + duration (сутки) -> момент истечения
            DateTime? expires = null;
            try
            {
                if (o.Issued != null && o.Duration > 0)
                    expires = o.Issued.AddDays(o.Duration);
            }
            catch { /* на всякий */ }

            return new OrderRow
            {
                OrderId = o.OrderId,
                Price = (double)o.Price,
                VolumeRemain = (int)o.VolumeRemain,
                VolumeTotal = (int)o.VolumeTotal,
                MinVolume = o.MinVolume,
                SystemId = o.SystemId,
                LocationId = o.LocationId,
                Range = o.Range.ToString(),
                Issued = o.Issued,
                Expires = expires
            };
        }

        private sealed class OrderRow
        {
            public long OrderId { get; set; }
            public double Price { get; set; }
            public int VolumeRemain { get; set; }
            public int VolumeTotal { get; set; }
            public int MinVolume { get; set; }
            public int SystemId { get; set; }
            public long LocationId { get; set; }
            public string Range { get; set; } = "";
            public DateTime? Issued { get; set; }
            public DateTime? Expires { get; set; }
        }
    }
}
