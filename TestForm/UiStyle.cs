using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using TestForm; // чтобы работала перегрузка для MainForm (обёртка)

internal static class UiStyle
{
    // Цвета в духе EVE Workbench
    private static readonly Color Bg = Color.FromArgb(18, 22, 28);
    private static readonly Color CardBg = Color.FromArgb(28, 34, 45);
    private static readonly Color Border = Color.FromArgb(55, 65, 81);
    private static readonly Color TextMain = Color.FromArgb(230, 237, 243);
    private static readonly Color TextSub = Color.FromArgb(160, 170, 180);
    private static readonly Color BtnBg = Color.FromArgb(38, 45, 58);
    private static readonly Color BtnBgHot = Color.FromArgb(48, 56, 72);

    // --- Старая сигнатура, чтобы не ломать MainForm ---
    public static void ApplyDark(MainForm f) => ApplyDark((Form)f);

    // --- Универсальная тёмная тема для любой формы ---
    public static void ApplyDark(Form f)
    {
        f.BackColor = Bg;
        f.ForeColor = TextMain;

        // ToolStrip / MenuStrip
        ToolStripManager.Renderer = new DarkRenderer(Bg, Border);
        foreach (var ts in f.Controls.OfType<ToolStrip>())
        {
            ts.BackColor = Bg;
            ts.ForeColor = TextMain;
            ts.GripStyle = ToolStripGripStyle.Hidden;
            foreach (ToolStripItem i in ts.Items) i.ForeColor = TextMain;
        }
        foreach (var ms in f.Controls.OfType<MenuStrip>())
        {
            ms.BackColor = Bg;
            ms.ForeColor = TextMain;
            foreach (ToolStripItem i in ms.Items) i.ForeColor = TextMain;
        }

        // StatusStrip
        foreach (var ss in f.Controls.OfType<StatusStrip>())
        {
            ss.BackColor = Bg;
            ss.ForeColor = TextMain;
            foreach (ToolStripItem i in ss.Items) i.ForeColor = TextMain;
        }

        // Карточки (панели внутри FlowLayoutPanel)
        StyleCards(f.Controls);

        // Таб-контролы/панели/сплиттеры под фон
        foreach (var tab in f.Controls.OfType<TabControl>()) { tab.BackColor = Bg; tab.ForeColor = TextMain; }
        foreach (var p in f.Controls.OfType<Panel>()) { if (p.Parent is not FlowLayoutPanel) { p.BackColor = Bg; p.ForeColor = TextMain; } }
        foreach (var sc in f.Controls.OfType<SplitContainer>()) { sc.BackColor = Bg; sc.Panel1.BackColor = Bg; sc.Panel2.BackColor = Bg; }

        // DataGridView — затемняем и включаем дабл-буфер
        foreach (var g in f.Controls.OfType<DataGridView>())
        {
            StyleGridDark(g);
            EnableDoubleBuffer(g);
        }
        // …и поиск вглубь
        foreach (Control c in f.Controls) ApplyToNestedGrids(c);
    }

    private static void ApplyToNestedGrids(Control parent)
    {
        foreach (Control child in parent.Controls)
        {
            if (child is DataGridView g)
            {
                StyleGridDark(g);
                EnableDoubleBuffer(g);
            }
            if (child.HasChildren) ApplyToNestedGrids(child);
        }
    }

    private static void StyleCards(Control.ControlCollection controls)
    {
        foreach (Control c in controls)
        {
            if (c is Panel p && p.Parent is FlowLayoutPanel)
            {
                p.BackColor = CardBg;
                p.ForeColor = TextMain;
                p.Paint += (s, e) =>
                {
                    using var pen = new Pen(Border);
                    e.Graphics.DrawRectangle(pen, 0, 0, p.Width - 1, p.Height - 1);
                };

                foreach (Control k in p.Controls)
                {
                    switch (k)
                    {
                        case Label lbl:
                            lbl.BackColor = Color.Transparent;
                            lbl.ForeColor = lbl.Font.Bold ? TextMain : TextSub;
                            break;
                        case Button b:
                            b.FlatStyle = FlatStyle.Flat;
                            b.FlatAppearance.BorderColor = Border;
                            b.FlatAppearance.MouseOverBackColor = BtnBgHot;
                            b.FlatAppearance.MouseDownBackColor = BtnBgHot;
                            b.BackColor = BtnBg;
                            b.ForeColor = TextMain;
                            break;
                    }
                }
            }

            if (c.HasChildren) StyleCards(c.Controls);
        }
    }

    // Публично: можно вызывать из любых форм, если нужен явный контроль
    public static void StyleGridDark(DataGridView g)
    {
        g.BackgroundColor = Bg;
        g.GridColor = Border;

        g.EnableHeadersVisualStyles = false;
        g.DefaultCellStyle.BackColor = CardBg;
        g.DefaultCellStyle.ForeColor = TextMain;
        g.DefaultCellStyle.SelectionBackColor = BtnBgHot;
        g.DefaultCellStyle.SelectionForeColor = Color.White;

        g.ColumnHeadersDefaultCellStyle.BackColor = CardBg;
        g.ColumnHeadersDefaultCellStyle.ForeColor = Color.Gainsboro;
        g.RowHeadersVisible = false;
    }

    public static void EnableDoubleBuffer(DataGridView grid)
    {
        grid.GetType()
            .GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?
            .SetValue(grid, true, null);
    }

    // --- Рендерер для ToolStrip/MenuStrip ---
    private sealed class DarkRenderer : ToolStripProfessionalRenderer
    {
        private readonly Color _bg, _border;
        public DarkRenderer(Color bg, Color border) { _bg = bg; _border = border; }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(_bg);
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            // размеры элемента сепаратора
            int w = e.Item.Width;
            int h = e.Item.Height;

            using var bg = new SolidBrush(_bg);
            e.Graphics.FillRectangle(bg, new Rectangle(0, 0, w, h));

            using var pen = new Pen(_border);
            int y = h / 2;
            e.Graphics.DrawLine(pen, 4, y, w - 4, y);
        }
    }
}
