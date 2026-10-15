using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MyGamaCards
{
    /// <summary>
    /// 2D 图表窗口 —— 课程设计演示的主可视化方案（替代/补充 E-GAMA 三维预览）。
    ///
    /// 每张卡持有一个 ChartWindow 实例：Build 计算完成后 SetDraw() 刷新，
    /// 右键卡片 →「显示二维图表」弹出。窗口已打开时，改参数重跑自动刷新。
    /// 独立于 previewController（三维预览的控制器），100% 可靠。
    /// </summary>
    public class ChartWindow : Form
    {
        private readonly TabControl _tabs;
        private readonly ToolStrip _bar;
        private readonly ToolStripStatusLabel _status;
        private List<ChartFigure> _figures = new List<ChartFigure>();
        private string _cardTitle = "";

        /// <summary>导出 PNG 的默认目录：桌面\GAMA图表\</summary>
        public static string ExportDir
        {
            get
            {
                string d = System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.Desktop), "GAMA图表");
                try { if (!System.IO.Directory.Exists(d)) System.IO.Directory.CreateDirectory(d); } catch { }
                return d;
            }
        }

        public ChartWindow(string title)
        {
            Text = title;
            _cardTitle = title;
            ClientSize = new Size(1180, 720);
            StartPosition = FormStartPosition.CenterScreen;
            DoubleBuffered = true;
            BackColor = Color.White;
            Font = new Font("微软雅黑", 9.5f);
            MinimumSize = new Size(640, 420);

            _bar = new ToolStrip { GripStyle = ToolStripGripStyle.Hidden, RenderMode = ToolStripRenderMode.System };
            var btnOpen = new ToolStripButton("📊 图表页");
            var btnCurrent = new ToolStripButton("💾 导出当前图表 PNG (2× 高清)");
            var btnAll = new ToolStripButton("📁 导出全部图表 PNG (2× 高清)");
            btnCurrent.Click += (s, e) => { ExportCurrent(); };
            btnAll.Click += (s, e) => { ExportAll(); };
            _bar.Items.Add(btnOpen);
            _bar.Items.Add(new ToolStripSeparator());
            _bar.Items.Add(btnCurrent);
            _bar.Items.Add(btnAll);

            _status = new ToolStripStatusLabel("导出目录：" + ExportDir) { Spring = true, TextAlign = ContentAlignment.MiddleLeft };
            var strip = new StatusStrip();
            strip.Items.Add(_status);

            _tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(14, 5) };

            Controls.Add(_tabs);
            Controls.Add(strip);
            Controls.Add(_bar);
        }

        private void OnPaint(object sender, PaintEventArgs e)
        {
            // 各图表页自绘（FigureCanvas.OnPaint），窗体本身不再绘制
        }

        /// <summary>更新图表页并重绘（线程安全：Build 可能在后台线程执行）</summary>
        public void SetFigures(IList<ChartFigure> figures, string windowTitle)
        {
            if (figures == null) figures = new List<ChartFigure>();
            if (IsDisposed) return;

            Action apply = () =>
            {
                if (IsDisposed) return;
                _figures = new List<ChartFigure>(figures);
                if (!string.IsNullOrEmpty(windowTitle)) { Text = windowTitle; _cardTitle = windowTitle; }

                // 尽量按标签名复用已有页，避免刷新时闪烁
                _tabs.SuspendLayout();
                var existing = new List<TabPage>();
                foreach (TabPage p in _tabs.TabPages) existing.Add(p);
                _tabs.TabPages.Clear();
                for (int i = 0; i < _figures.Count; i++)
                {
                    var fig = _figures[i];
                    var panel = new FigureCanvas(fig) { Dock = DockStyle.Fill, BackColor = Color.White };
                    var page = new TabPage(fig.TabTitle ?? ("图表 " + (i + 1)));
                    page.BackColor = Color.White;
                    page.Controls.Add(panel);
                    _tabs.TabPages.Add(page);
                }
                if (_figures.Count > 0 && _tabs.SelectedIndex < 0) _tabs.SelectedIndex = 0;
                _tabs.ResumeLayout();
                _status.Text = string.Format("图表 {0} 页　|　导出目录：{1}", _figures.Count, ExportDir);
            };

            if (InvokeRequired) BeginInvoke(apply);
            else apply();
        }

        /// <summary>导出当前页 PNG（2 倍分辨率）</summary>
        public void ExportCurrent()
        {
            int idx = _tabs.SelectedIndex;
            if (idx < 0 || idx >= _figures.Count) return;
            ExportOne(_figures[idx], idx);
        }

        /// <summary>导出全部页 PNG（2 倍分辨率）</summary>
        public void ExportAll()
        {
            if (_figures.Count == 0) return;
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _figures.Count; i++)
                sb.AppendLine(ExportOne(_figures[i], i, true));
            MessageBox.Show("已导出全部图表：\r\n" + sb.ToString(), "导出完成",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private string ExportOne(ChartFigure fig, int index, bool silent = false)
        {
            try
            {
                string path = PathFor(fig, index);
                RenderToPng(fig, path, ExportWidth, ExportHeight, 2);
                string msg = string.Format("{0} → {1}", fig.TabTitle, path);
                if (!silent)
                {
                    var r = MessageBox.Show("图表已导出：\r\n" + path + "\r\n\r\n是否打开所在文件夹？", "导出完成",
                        MessageBoxButtons.YesNo, MessageBoxIcon.Information);
                    if (r == DialogResult.Yes)
                        try { System.Diagnostics.Process.Start("explorer.exe", "/select,\"" + path + "\""); } catch { }
                }
                return msg;
            }
            catch (Exception ex)
            {
                if (!silent) MessageBox.Show("导出失败：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return "导出失败：" + fig.TabTitle + " → " + ex.Message;
            }
        }

        private const int ExportWidth = 1600;
        private const int ExportHeight = 950;

        internal static string Sanitize(string s)
        {
            if (string.IsNullOrEmpty(s)) return "figure";
            var sb = new System.Text.StringBuilder();
            foreach (char c in s)
            {
                if (char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '(' || c == ')') sb.Append(c);
                else if (c == ' ' || c == '·' || c == '/') sb.Append('_');
                else sb.Append('_');
            }
            string r = sb.ToString().Trim('_');
            return r.Length == 0 ? "figure" : (r.Length > 60 ? r.Substring(0, 60) : r);
        }

        private string PathFor(ChartFigure fig, int index)
        {
            string name = Sanitize(_cardTitle + "_" + (index + 1) + "_" + (fig.TabTitle ?? "chart")) + ".png";
            return System.IO.Path.Combine(ExportDir, name);
        }

        /// <summary>把一张图表按指定逻辑尺寸 × 倍数渲染为 PNG 文件（屏幕外重绘，不受窗口大小影响）</summary>
        public static void RenderToPng(ChartFigure fig, string path, int w, int h, int scale)
        {
            using (var bmp = new Bitmap(w * scale, h * scale))
            {
                using (var g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                    g.Clear(Color.White);
                    g.ScaleTransform(scale, scale);
                    fig.Draw(g, w, h);
                }
                bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);
            }
        }

        /// <summary>无窗口批量导出（供卡片右键菜单使用）</summary>
        public static int ExportFigures(string cardTitle, IList<ChartFigure> figures, out string message)
        {
            var sb = new System.Text.StringBuilder();
            int ok = 0;
            if (figures != null)
                for (int i = 0; i < figures.Count; i++)
                {
                    var fig = figures[i];
                    string name = Sanitize(cardTitle + "_" + (i + 1) + "_" + (fig.TabTitle ?? "chart")) + ".png";
                    string path = System.IO.Path.Combine(ExportDir, name);
                    try
                    {
                        RenderToPng(fig, path, ExportWidth, ExportHeight, 2);
                        sb.AppendLine("✔ " + path);
                        ok++;
                    }
                    catch (Exception ex)
                    {
                        sb.AppendLine("✘ " + (fig.TabTitle ?? "chart") + "：" + ex.Message);
                    }
                }
            sb.AppendLine();
            sb.AppendLine("导出目录：" + ExportDir);
            message = sb.ToString();
            return ok;
        }
    }

    /// <summary>单张图表的画布（自绘 + 抗锯齿 + 双击支持）</summary>
    public class FigureCanvas : Panel
    {
        private readonly ChartFigure _fig;

        public FigureCanvas(ChartFigure fig)
        {
            _fig = fig;
            DoubleBuffered = true;
            ResizeRedraw = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.White);
            try { if (_fig != null && _fig.Draw != null) _fig.Draw(g, ClientSize.Width, ClientSize.Height); }
            catch (Exception ex)
            {
                using (var b = new SolidBrush(Color.FromArgb(180, 60, 60)))
                    g.DrawString("图表绘制失败：" + ex.Message, new Font("微软雅黑", 10f), b, 20, 20);
            }
        }
    }

    /// <summary>2D 绘图工具（GDI+）：坐标轴 / 折线 / 散点 / 热力图 / 色带 / 网络结构图</summary>
    public static class Chart2D
    {
        public static readonly Font TitleFont = new Font("微软雅黑", 12f, FontStyle.Bold);
        public static readonly Font LabelFont = new Font("微软雅黑", 9.5f);

        /// <summary>绘制标题（居中，可带副标题）</summary>
        public static void DrawTitle(Graphics g, int width, string title, string subtitle = null)
        {
            using (var b = new SolidBrush(Color.FromArgb(45, 45, 45)))
            {
                var sz = g.MeasureString(title, TitleFont);
                g.DrawString(title, TitleFont, b, (width - sz.Width) / 2, 12);
                if (!string.IsNullOrEmpty(subtitle))
                {
                    using (var b2 = new SolidBrush(Color.FromArgb(130, 130, 130)))
                    {
                        var sz2 = g.MeasureString(subtitle, LabelFont);
                        g.DrawString(subtitle, LabelFont, b2, (width - sz2.Width) / 2, 42);
                    }
                }
            }
        }

        public static readonly Color Accent = Color.FromArgb(47, 111, 190);     // 主色（钢蓝）
        public static readonly Color Orange = Color.FromArgb(232, 131, 58);     // 次色（橙）
        public static readonly Color Red = Color.FromArgb(198, 60, 60);         // 强调（红）
        public static readonly Color GridColor = Color.FromArgb(235, 235, 235);
        private static readonly Color AxisColor = Color.FromArgb(70, 70, 70);
        private static readonly Color LabelColor = Color.FromArgb(105, 105, 105);

        /// <summary>颜色线性插值</summary>
        public static Color Lerp(Color a, Color b, double t)
        {
            t = t < 0 ? 0 : (t > 1 ? 1 : t);
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        /// <summary>数据坐标 → 像素坐标（y 轴向上）</summary>
        public static PointF Map(double x, double y, RectangleF r, double x0, double x1, double y0, double y1)
        {
            if (x1 - x0 < 1e-15) x1 = x0 + 1;
            if (y1 - y0 < 1e-15) y1 = y0 + 1;
            double fx = (x - x0) / (x1 - x0);
            double fy = (y - y0) / (y1 - y0);
            return new PointF((float)(r.Left + fx * r.Width), (float)(r.Bottom - fy * r.Height));
        }

        /// <summary>紧凑数字格式</summary>
        public static string FmtNum(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v)) return "N/A";
            double a = Math.Abs(v);
            if (a >= 100000) return v.ToString("0.#e0");
            if (a >= 100) return v.ToString("0");
            if (a >= 1) return v.ToString("0.##");
            if (a >= 0.01) return v.ToString("0.###");
            if (a < 1e-9) return "0";
            return v.ToString("0.####e0");
        }

        /// <summary>坐标轴 + 网格 + 刻度 + 轴标题（x 轴 5 等分，y 轴 5 等分）</summary>
        public static void DrawAxes(Graphics g, RectangleF plot,
                                    double x0, double x1, double y0, double y1,
                                    string xTitle, string yTitle, Font font)
        {
            using (var gridPen = new Pen(GridColor, 1f))
            using (var axisPen = new Pen(AxisColor, 1.4f))
            using (var lb = new SolidBrush(LabelColor))
            {
                for (int i = 0; i <= 5; i++)
                {
                    double t = i / 5.0;
                    float px = plot.Left + (float)(t * plot.Width);
                    float py = plot.Bottom - (float)(t * plot.Height);
                    g.DrawLine(gridPen, px, plot.Top, px, plot.Bottom);
                    g.DrawLine(gridPen, plot.Left, py, plot.Right, py);
                    var tx = FmtNum(x0 + t * (x1 - x0));
                    var ty = FmtNum(y0 + t * (y1 - y0));
                    var sx = g.MeasureString(tx, font);
                    g.DrawString(tx, font, lb, px - sx.Width / 2, plot.Bottom + 4);
                    var sy = g.MeasureString(ty, font);
                    g.DrawString(ty, font, lb, plot.Left - sy.Width - 6, py - sy.Height / 2);
                }
                g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
                g.DrawLine(axisPen, plot.Left, plot.Bottom, plot.Left, plot.Top);

                // X 轴标题（水平）
                var sx2 = g.MeasureString(xTitle, font);
                g.DrawString(xTitle, font, lb, plot.Left + (plot.Width - sx2.Width) / 2, plot.Bottom + 24);
                // Y 轴标题（旋转 90°，避免长标题被画布裁掉；左移避免与刻度重叠）
                var st = g.Save();
                g.TranslateTransform(plot.Left - 34, plot.Top + plot.Height / 2);
                g.RotateTransform(-90);
                var sy2 = g.MeasureString(yTitle, font);
                g.DrawString(yTitle, font, lb, -sy2.Width / 2, -sy2.Height / 2);
                g.Restore(st);
            }
        }

        /// <summary>折线序列</summary>
        public static void DrawSeries(Graphics g, IList<double> xs, IList<double> ys, RectangleF plot,
                                      double x0, double x1, double y0, double y1,
                                      Color color, float penWidth = 2f)
        {
            if (xs == null || ys == null || xs.Count == 0) return;
            using (var pen = new Pen(color, penWidth))
            {
                var pts = new List<PointF>(xs.Count);
                for (int i = 0; i < xs.Count && i < ys.Count; i++)
                    pts.Add(Map(xs[i], ys[i], plot, x0, x1, y0, y1));
                if (pts.Count == 1)
                    g.FillEllipse(new SolidBrush(color), pts[0].X - 2, pts[0].Y - 2, 4, 4);
                else
                    g.DrawLines(pen, pts.ToArray());
            }
        }

        /// <summary>散点（统一颜色）</summary>
        public static void DrawScatter(Graphics g, IList<double> xs, IList<double> ys, RectangleF plot,
                                       double x0, double x1, double y0, double y1,
                                       Color color, float size = 4f)
        {
            using (var b = new SolidBrush(color))
                for (int i = 0; i < xs.Count && i < ys.Count; i++)
                {
                    var p = Map(xs[i], ys[i], plot, x0, x1, y0, y1);
                    g.FillEllipse(b, p.X - size / 2, p.Y - size / 2, size, size);
                }
        }

        /// <summary>散点（按 z 值着色：蓝→橙→红，带色带）</summary>
        public static void DrawScatterColored(Graphics g, IList<double> xs, IList<double> ys, IList<double> zs,
                                              RectangleF plot, double x0, double x1, double y0, double y1,
                                              double z0, double z1, float size = 5f)
        {
            if (z1 - z0 < 1e-15) z1 = z0 + 1;
            for (int i = 0; i < xs.Count && i < ys.Count && i < zs.Count; i++)
            {
                double t = (zs[i] - z0) / (z1 - z0);
                if (t < 0) t = 0; if (t > 1) t = 1;
                var c = t < 0.5 ? Lerp(Color.FromArgb(59, 110, 190), Color.FromArgb(232, 131, 58), t * 2)
                                : Lerp(Color.FromArgb(232, 131, 58), Red, (t - 0.5) * 2);
                using (var b = new SolidBrush(c))
                {
                    var p = Map(xs[i], ys[i], plot, x0, x1, y0, y1);
                    g.FillEllipse(b, p.X - size / 2, p.Y - size / 2, size, size);
                }
            }
        }

        /// <summary>热力图：网格上按 zFn 求值着色（蓝→橙→红），可选叠加等值网格线</summary>
        public static void DrawHeatmap(Graphics g, RectangleF plot,
                                       Func<double, double, double> zFn,
                                       double x0, double x1, double y0, double y1,
                                       double zMin, double zMax, int gridN = 24, bool gridLines = false)
        {
            double dx = (x1 - x0) / gridN, dy = (y1 - y0) / gridN;
            if (zMax - zMin < 1e-15) zMax = zMin + 1;
            for (int i = 0; i < gridN; i++)
                for (int j = 0; j < gridN; j++)
                {
                    double cx = x0 + (i + 0.5) * dx;
                    double cy = y0 + (j + 0.5) * dy;
                    double z = zFn(cx, cy);
                    double t = (z - zMin) / (zMax - zMin);
                    if (t < 0) t = 0; if (t > 1) t = 1;
                    var c = t < 0.5 ? Lerp(Color.FromArgb(59, 110, 190), Color.FromArgb(232, 131, 58), t * 2)
                                    : Lerp(Color.FromArgb(232, 131, 58), Red, (t - 0.5) * 2);
                    using (var b = new SolidBrush(c))
                        g.FillRectangle(b,
                            (float)(plot.Left + i * plot.Width / gridN),
                            (float)(plot.Top + (gridN - 1 - j) * plot.Height / gridN),
                            (float)(plot.Width / gridN) + 0.5f,
                            (float)(plot.Height / gridN) + 0.5f);
                }
            if (gridLines)
            {
                using (var pen = new Pen(Color.FromArgb(120, 255, 255, 255), 0.6f))
                    for (int i = 0; i <= gridN; i++)
                    {
                        float px = plot.Left + i * plot.Width / gridN;
                        float py = plot.Top + i * plot.Height / gridN;
                        g.DrawLine(pen, px, plot.Top, px, plot.Bottom);
                        g.DrawLine(pen, plot.Left, py, plot.Right, py);
                    }
            }
        }

        /// <summary>右侧纵向色带（底部=低值蓝，顶部=高值红，与热力图方向一致）</summary>
        public static void DrawColorBar(Graphics g, RectangleF bar, double z0, double z1, string label, Font font)
        {
            int n = 40;
            for (int i = 0; i < n; i++)
            {
                // i=0 画在底部 → t≈0 → 蓝色（低值）；i=n-1 画在顶部 → 红（高值）
                double t = (i + 0.5) / n;
                var c = t < 0.5 ? Lerp(Color.FromArgb(59, 110, 190), Color.FromArgb(232, 131, 58), t * 2)
                                : Lerp(Color.FromArgb(232, 131, 58), Red, (t - 0.5) * 2);
                using (var b = new SolidBrush(c))
                    g.FillRectangle(b, bar.Left, bar.Bottom - (i + 1) * bar.Height / n, bar.Width, bar.Height / n + 1);
            }
            using (var pen = new Pen(AxisColor, 1f))
                g.DrawRectangle(pen, bar.Left, bar.Top, bar.Width, bar.Height);
            using (var lb = new SolidBrush(LabelColor))
            {
                g.DrawString(FmtNum(z1), font, lb, bar.Right + 6, bar.Top - 4);
                g.DrawString(FmtNum(z0), font, lb, bar.Right + 6, bar.Bottom - 14);
                var sz = g.MeasureString(label, font);
                g.DrawString(label, font, lb, bar.Right + 6, bar.Top + (bar.Height - sz.Height) / 2);
            }
        }

        /// <summary>文本标注（右上角指标面板）</summary>
        public static void DrawMetricPanel(Graphics g, float x, float y, string[] lines, Font font)
        {
            using (var b = new SolidBrush(Color.FromArgb(250, 252, 255)))
            using (var pen = new Pen(Color.FromArgb(210, 220, 235), 1f))
            using (var lb = new SolidBrush(Color.FromArgb(50, 60, 80)))
            {
                float w = 0;
                var sizes = new SizeF[lines.Length];
                for (int i = 0; i < lines.Length; i++)
                {
                    sizes[i] = g.MeasureString(lines[i], font);
                    w = Math.Max(w, sizes[i].Width);
                }
                float h = 0;
                for (int i = 0; i < lines.Length; i++) h += sizes[i].Height + 4;
                var rect = new RectangleF(x, y, w + 20, h + 12);
                g.FillRectangle(b, rect);
                g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                float ty = rect.Y + 6;
                for (int i = 0; i < lines.Length; i++)
                {
                    g.DrawString(lines[i], font, lb, rect.X + 10, ty);
                    ty += sizes[i].Height + 4;
                }
            }
        }

        /// <summary>
        /// 神经网络结构图（2D）：层=一列神经元圆点，层间=细连线，按层类型着色。
        /// sizes = [输入维, 隐藏层..., 输出维]
        /// </summary>
        public static void DrawNetwork(Graphics g, RectangleF area, int[] sizes, Font font)
        {
            if (sizes == null || sizes.Length < 2) return;
            int L = sizes.Length;
            float layerGap = area.Width / (L + 1);
            float maxNeurons = 1;
            for (int i = 0; i < L; i++) maxNeurons = Math.Max(maxNeurons, sizes[i]);
            float neuronGap = area.Height / (maxNeurons + 1);
            float r = Math.Min(11f, neuronGap * 0.34f);

            var cols = new List<PointF[]>();
            for (int l = 0; l < L; l++)
            {
                var pts = new PointF[sizes[l]];
                float cx = area.Left + layerGap * (l + 1);
                for (int j = 0; j < sizes[l]; j++)
                {
                    float cy = area.Top + area.Height / 2 - (sizes[l] - 1) * neuronGap / 2 + j * neuronGap;
                    pts[j] = new PointF(cx, cy);
                }
                cols.Add(pts);
            }

            // 层间连线（先画，垫底）
            using (var linePen = new Pen(Color.FromArgb(120, 130, 140, 155), 0.9f))
                for (int l = 0; l < L - 1; l++)
                    foreach (var a in cols[l])
                        foreach (var b in cols[l + 1])
                            g.DrawLine(linePen, a, b);

            // 神经元圆点 + 层标题
            for (int l = 0; l < L; l++)
            {
                var c = l == 0 ? Accent : (l == L - 1 ? Red : Orange);
                using (var fill = new SolidBrush(c))
                using (var edge = new Pen(Color.FromArgb(60, Color.White), 1.2f))
                {
                    foreach (var p in cols[l])
                    {
                        g.FillEllipse(fill, p.X - r, p.Y - r, r * 2, r * 2);
                        g.DrawEllipse(edge, p.X - r, p.Y - r, r * 2, r * 2);
                    }
                }
                string name = l == 0 ? "输入层" : (l == L - 1 ? "输出层" : (L > 3 ? "隐藏层" + l : "隐藏层"));
                using (var lb = new SolidBrush(LabelColor))
                {
                    var label = name + "(" + sizes[l] + ")";
                    var sz = g.MeasureString(label, font);
                    g.DrawString(label, font, lb, cols[l][0].X - sz.Width / 2, area.Top + 4);
                }
            }
        }
    }
}
