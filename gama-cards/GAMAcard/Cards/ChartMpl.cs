using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace MyGamaCards
{
    /// <summary>
    /// 一张图表页。TabTitle = 窗口里的标签名；Draw = 绘制委托（参数为画布逻辑尺寸）。
    /// 导出 PNG 时按同一份委托放大重绘，所以屏幕与图片完全一致、没有失真。
    /// </summary>
    public class ChartFigure
    {
        public string TabTitle { get; private set; }
        public string Description { get; private set; }
        public Action<Graphics, int, int> Draw { get; private set; }

        public ChartFigure(string tabTitle, Action<Graphics, int, int> draw, string description = null)
        {
            TabTitle = tabTitle;
            Description = description;
            Draw = draw;
        }
    }

    /// <summary>图例项样式</summary>
    public enum LegendMark { Point, Line, DashedLine }

    /// <summary>图例项</summary>
    public class LegendItem
    {
        public string Label { get; set; }
        public Color Color { get; set; }
        public LegendMark Mark { get; set; }
        public float Size { get; set; }

        public LegendItem(string label, Color color, LegendMark mark, float size = 6f)
        {
            Label = label; Color = color; Mark = mark; Size = size;
        }
    }

    /// <summary>
    /// Matplotlib 风格 2D 绘图工具（GDI+）。
    /// 配色/布局刻意对齐课程设计里的 matplotlib 图（蓝=训练集、绿=测试集、红虚线=理想线 y=x、
    /// 灰虚线网格、四周带框坐标轴、左上角图例），用于复刻课程设计图并支持高清 PNG 导出。
    /// </summary>
    public static class Mpl
    {
        // ---------- 课程设计（matplotlib）配色 ----------
        public static readonly Color CBlue = Color.FromArgb(0, 0, 255);       // mpl 'blue'
        public static readonly Color CGreen = Color.FromArgb(0, 128, 0);      // mpl 'green'
        public static readonly Color CRed = Color.FromArgb(255, 0, 0);        // mpl 'red'
        public static readonly Color CGrid = Color.FromArgb(204, 204, 204);   // mpl grid #CCCCCC
        public static readonly Color CSpine = Color.FromArgb(50, 50, 50);
        public static readonly Color CLabel = Color.FromArgb(35, 35, 35);
        public static readonly Color CNote = Color.FromArgb(110, 110, 110);

        // 直方图配色：照抄 preprocess.py 里 data.png 的四色（alpha 0.6 叠在白底上的效果）
        public static readonly Color HistWater = Blend(Color.White, Color.FromArgb(0x98, 0xC8, 0x97), 0.6);
        public static readonly Color HistFlyAsh = Blend(Color.White, Color.FromArgb(0x9C, 0x6B, 0xA3), 0.6);
        public static readonly Color HistSilica = Blend(Color.White, Color.FromArgb(0x9D, 0xBA, 0xD2), 0.6);
        public static readonly Color HistStrength = Blend(Color.White, Color.FromArgb(0xFE, 0x9C, 0x9D), 0.6);

        // 多模型对比用的 tab10 调色板
        public static readonly Color[] Palette =
        {
            Color.FromArgb(31, 119, 180), Color.FromArgb(255, 127, 14), Color.FromArgb(44, 160, 44),
            Color.FromArgb(214, 39, 40), Color.FromArgb(148, 103, 189), Color.FromArgb(140, 86, 75),
            Color.FromArgb(227, 119, 194), Color.FromArgb(23, 190, 207)
        };

        public static readonly Font FAxis = new Font("Segoe UI", 10f);
        public static readonly Font FTick = new Font("Segoe UI", 9.2f);
        public static readonly Font FTitle = new Font("Segoe UI", 11.5f, FontStyle.Bold);
        public static readonly Font FCn = new Font("微软雅黑", 10f);
        public static readonly Font FCnTick = new Font("微软雅黑", 9f);
        public static readonly Font FCnTitle = new Font("微软雅黑", 12f, FontStyle.Bold);
        public static readonly Font FCnSmall = new Font("微软雅黑", 9f);

        /// <summary>颜色混合（t=1 取 b）</summary>
        public static Color Blend(Color a, Color b, double t)
        {
            t = t < 0 ? 0 : (t > 1 ? 1 : t);
            return Color.FromArgb(
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }

        public static Color Alpha(Color c, int a) => Color.FromArgb(a, c.R, c.G, c.B);

        // ==================== 坐标映射与刻度 ====================

        public static float MapX(double v, RectangleF p, double x0, double x1)
        {
            if (Math.Abs(x1 - x0) < 1e-15) x1 = x0 + 1;
            return (float)(p.Left + (v - x0) / (x1 - x0) * p.Width);
        }

        public static float MapY(double v, RectangleF p, double y0, double y1)
        {
            if (Math.Abs(y1 - y0) < 1e-15) y1 = y0 + 1;
            return (float)(p.Bottom - (v - y0) / (y1 - y0) * p.Height);
        }

        /// <summary>1-2-5 规则的“好看”刻度序列（matplotlib 默认风格）</summary>
        public static List<double> Ticks(double lo, double hi, int target = 6)
        {
            var list = new List<double>();
            if (double.IsNaN(lo) || double.IsNaN(hi) || double.IsInfinity(lo) || double.IsInfinity(hi)) return list;
            if (hi < lo) { double t = lo; lo = hi; hi = t; }
            double span = hi - lo;
            if (span <= 0) span = Math.Abs(hi) > 0 ? Math.Abs(hi) : 1;

            double raw = span / Math.Max(2, target);
            double mag = Math.Pow(10, Math.Floor(Math.Log10(raw)));
            double norm = raw / mag;
            double step = norm <= 1 ? 1 : (norm <= 2 ? 2 : (norm <= 2.5 ? 2.5 : (norm <= 5 ? 5 : 10)));
            step *= mag;

            double start = Math.Ceiling(lo / step) * step;
            for (double v = start; v <= hi + step * 1e-6 && list.Count < 30; v += step)
                list.Add(Math.Abs(v) < step * 1e-9 ? 0 : v);
            return list;
        }

        public static int DecimalsFor(double step)
        {
            if (step <= 0) return 2;
            int d = (int)Math.Ceiling(-Math.Log10(step));
            if (d < 0) d = 0;
            if (d > 4) d = 4;
            return d;
        }

        /// <summary>固定步长的刻度序列（用于样本序号这类必须整数刻度、不能出现 2.5 的轴）</summary>
        public static List<double> StepTicks(double lo, double hi, double step)
        {
            var list = new List<double>();
            if (step <= 0 || double.IsNaN(step)) return list;
            double start = Math.Ceiling(lo / step) * step;
            for (double v = start; v <= hi + step * 1e-6 && list.Count < 60; v += step)
                list.Add(v);
            return list;
        }

        public static string FmtTick(double v, int dec)
        {
            if (Math.Abs(v) < 1e-12) return "0";
            double a = Math.Abs(v);
            if (a >= 1e5 || a < 1e-3) return v.ToString("0.#e+0");
            return v.ToString("F" + dec);
        }

        /// <summary>指标数值统一 4 位小数</summary>
        public static string F4(double v) => double.IsNaN(v) ? "N/A" : v.ToString("F4");

        /// <summary>把区域切成 rows×cols 的子块（索引从 0 开始，行优先）</summary>
        public static RectangleF SubRect(RectangleF area, int rows, int cols, int row, int col, float gapX, float gapY)
        {
            float cw = (area.Width - gapX * (cols - 1)) / cols;
            float ch = (area.Height - gapY * (rows - 1)) / rows;
            float x = area.Left + col * (cw + gapX);
            float y = area.Top + row * (ch + gapY);
            return new RectangleF(x, y, cw, ch);
        }

        // ==================== 文字 ====================

        public static void Text(Graphics g, string s, Font f, Color c, float x, float y)
        {
            if (string.IsNullOrEmpty(s)) return;
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, x, y);
        }

        public static void TextCenter(Graphics g, string s, Font f, Color c, float cx, float y)
        {
            if (string.IsNullOrEmpty(s)) return;
            var sz = g.MeasureString(s, f);
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, cx - sz.Width / 2, y);
        }

        public static void TextRight(Graphics g, string s, Font f, Color c, float right, float y)
        {
            if (string.IsNullOrEmpty(s)) return;
            var sz = g.MeasureString(s, f);
            using (var b = new SolidBrush(c)) g.DrawString(s, f, b, right - sz.Width, y);
        }

        /// <summary>整幅图的大标题（居中）+ 副标题</summary>
        public static void FigureTitle(Graphics g, int w, string title, string subtitle, bool chineseFont = false)
        {
            var f = chineseFont ? FCnTitle : new Font("Segoe UI", 13f, FontStyle.Bold);
            var sz = g.MeasureString(title, f);
            Text(g, title, f, Color.FromArgb(30, 30, 30), (w - sz.Width) / 2, 10);
            if (!string.IsNullOrEmpty(subtitle))
                TextCenter(g, subtitle, chineseFont ? FCnSmall : FTick, CNote, w / 2f, 10 + sz.Height + 1);
        }

        // ==================== 坐标轴 ====================

        /// <summary>
        /// 带边框（四周 spine）+ 外向刻度 + 轴标题的坐标轴，风格对齐 matplotlib 默认样式。
        /// </summary>
        public static void Axes(Graphics g, RectangleF plot, double x0, double x1, double y0, double y1,
                                string xTitle, string yTitle, Font font = null, bool grid = false, bool chineseFont = false,
                                double xTickStep = 0, double yTickStep = 0)
        {
            if (font == null) font = chineseFont ? FCn : FAxis;
            var tickFont = chineseFont ? FCnTick : FTick;
            using (var spine = new Pen(CSpine, 1.1f))
            using (var tickPen = new Pen(CSpine, 1.1f))
            {
                var xs = xTickStep > 0 ? StepTicks(x0, x1, xTickStep) : Ticks(x0, x1, 6);
                var ys = yTickStep > 0 ? StepTicks(y0, y1, yTickStep) : Ticks(y0, y1, 6);
                double xstep = xs.Count > 1 ? xs[1] - xs[0] : (x1 - x0);
                double ystep = ys.Count > 1 ? ys[1] - ys[0] : (y1 - y0);
                int xdec = DecimalsFor(xstep), ydec = DecimalsFor(ystep);

                // 网格（虚线）
                if (grid)
                {
                    using (var gp = new Pen(CGrid, 1f))
                    {
                        gp.DashStyle = DashStyle.Custom;
                        gp.DashPattern = new float[] { 4f, 4f };
                        foreach (var v in xs) { float px = MapX(v, plot, x0, x1); g.DrawLine(gp, px, plot.Top, px, plot.Bottom); }
                        foreach (var v in ys) { float py = MapY(v, plot, y0, y1); g.DrawLine(gp, plot.Left, py, plot.Right, py); }
                    }
                }

                // 四周边框
                g.DrawRectangle(spine, plot.X, plot.Y, plot.Width, plot.Height);

                // X 轴刻度
                foreach (var v in xs)
                {
                    float px = MapX(v, plot, x0, x1);
                    if (px < plot.Left - 0.5f || px > plot.Right + 0.5f) continue;
                    g.DrawLine(tickPen, px, plot.Bottom, px, plot.Bottom + 5f);
                    TextCenter(g, FmtTick(v, xdec), tickFont, CLabel, px, plot.Bottom + 7f);
                }
                // Y 轴刻度
                foreach (var v in ys)
                {
                    float py = MapY(v, plot, y0, y1);
                    if (py < plot.Top - 0.5f || py > plot.Bottom + 0.5f) continue;
                    g.DrawLine(tickPen, plot.Left, py, plot.Left - 5f, py);
                    TextRight(g, FmtTick(v, ydec), tickFont, CLabel, plot.Left - 8f, py - g.MeasureString("0", tickFont).Height / 2);
                }

                // 轴标题
                if (!string.IsNullOrEmpty(xTitle))
                    TextCenter(g, xTitle, font, CLabel, plot.Left + plot.Width / 2, plot.Bottom + 24f);

                if (!string.IsNullOrEmpty(yTitle))
                {
                    var st = g.Save();
                    g.TranslateTransform(plot.Left - 46f, plot.Top + plot.Height / 2);
                    g.RotateTransform(-90);
                    var sz = g.MeasureString(yTitle, font);
                    using (var b = new SolidBrush(CLabel))
                        g.DrawString(yTitle, font, b, -sz.Width / 2, -sz.Height / 2);
                    g.Restore(st);
                }
            }
        }

        /// <summary>Y 轴取对数（log10）的坐标轴：刻度按十进制幂次标注，用于对比多个模型的损失曲线</summary>
        public static void AxesLogY(Graphics g, RectangleF plot, double x0, double x1, double yLo, double yHi,
                                    string xTitle, string yTitle, Font font = null, bool grid = true, bool chineseFont = false)
        {
            if (font == null) font = chineseFont ? FCn : FAxis;
            var tickFont = chineseFont ? FCnTick : FTick;
            if (yLo <= 0) yLo = 1e-8;
            if (yHi <= yLo) yHi = yLo * 10;
            double t0 = Math.Floor(Math.Log10(yLo)), t1 = Math.Ceiling(Math.Log10(yHi));
            using (var spine = new Pen(CSpine, 1.1f))
            using (var tickPen = new Pen(CSpine, 1.1f))
            {
                var xs = Ticks(x0, x1, 6);
                double xstep = xs.Count > 1 ? xs[1] - xs[0] : (x1 - x0);
                int xdec = DecimalsFor(xstep);

                if (grid)
                {
                    using (var gp = new Pen(CGrid, 1f))
                    {
                        gp.DashStyle = DashStyle.Custom;
                        gp.DashPattern = new float[] { 4f, 4f };
                        foreach (var v in xs) { float px = MapX(v, plot, x0, x1); g.DrawLine(gp, px, plot.Top, px, plot.Bottom); }
                        for (double t = t0; t <= t1 + 1e-9; t += 1)
                        {
                            float py = LogMapY(Math.Pow(10, t), plot, yLo, yHi);
                            g.DrawLine(gp, plot.Left, py, plot.Right, py);
                        }
                    }
                }
                g.DrawRectangle(spine, plot.X, plot.Y, plot.Width, plot.Height);

                foreach (var v in xs)
                {
                    float px = MapX(v, plot, x0, x1);
                    if (px < plot.Left - 0.5f || px > plot.Right + 0.5f) continue;
                    g.DrawLine(tickPen, px, plot.Bottom, px, plot.Bottom + 5f);
                    TextCenter(g, FmtTick(v, xdec), tickFont, CLabel, px, plot.Bottom + 7f);
                }
                for (double t = t0; t <= t1 + 1e-9; t += 1)
                {
                    float py = LogMapY(Math.Pow(10, t), plot, yLo, yHi);
                    if (py < plot.Top - 0.5f || py > plot.Bottom + 0.5f) continue;
                    g.DrawLine(tickPen, plot.Left, py, plot.Left - 5f, py);
                    TextRight(g, "1e" + t.ToString("0"), tickFont, CLabel, plot.Left - 8f,
                        py - g.MeasureString("0", tickFont).Height / 2);
                }
                if (!string.IsNullOrEmpty(xTitle))
                    TextCenter(g, xTitle, font, CLabel, plot.Left + plot.Width / 2, plot.Bottom + 24f);
                if (!string.IsNullOrEmpty(yTitle))
                {
                    var st = g.Save();
                    g.TranslateTransform(plot.Left - 46f, plot.Top + plot.Height / 2);
                    g.RotateTransform(-90);
                    var sz = g.MeasureString(yTitle, font);
                    using (var b = new SolidBrush(CLabel)) g.DrawString(yTitle, font, b, -sz.Width / 2, -sz.Height / 2);
                    g.Restore(st);
                }
            }
        }

        public static float LogMapY(double v, RectangleF p, double yLo, double yHi)
        {
            if (v <= 0) v = yLo;
            return MapY(Math.Log10(v), p, Math.Log10(yLo), Math.Log10(yHi));
        }

        // ==================== 数据图元 ====================

        public static readonly Color IdealLineColor = Color.FromArgb(230, 30, 30);

        /// <summary>理想线 y=x（红色虚线）</summary>
        public static void IdealLine(Graphics g, RectangleF plot, double lo, double hi, bool clip = true)
        {
            var st = g.Save();
            if (clip) g.SetClip(plot);
            using (var pen = new Pen(IdealLineColor, 1.8f))
            {
                pen.DashStyle = DashStyle.Custom;
                pen.DashPattern = new float[] { 6f, 4f };
                g.DrawLine(pen,
                    MapX(lo, plot, lo, hi), MapY(lo, plot, lo, hi),
                    MapX(hi, plot, lo, hi), MapY(hi, plot, lo, hi));
            }
            g.Restore(st);
        }

        public static void Scatter(Graphics g, RectangleF plot, double x0, double x1, double y0, double y1,
                                   double[] xs, double[] ys, Color color, float size = 6.5f)
        {
            if (xs == null || ys == null) return;
            var st = g.Save();
            g.SetClip(plot, CombineMode.Intersect);
            using (var b = new SolidBrush(color))
            {
                int n = Math.Min(xs.Length, ys.Length);
                for (int i = 0; i < n; i++)
                {
                    if (double.IsNaN(xs[i]) || double.IsNaN(ys[i])) continue;
                    var p = new PointF(MapX(xs[i], plot, x0, x1), MapY(ys[i], plot, y0, y1));
                    g.FillEllipse(b, p.X - size / 2, p.Y - size / 2, size, size);
                }
            }
            g.Restore(st);
        }

        /// <summary>折线（可带圆点标记）</summary>
        public static void Series(Graphics g, RectangleF plot, double x0, double x1, double y0, double y1,
                                  double[] xs, double[] ys, Color color, float width = 2f, float markerSize = 0f, bool logY = false)
        {
            if (xs == null || ys == null || xs.Length < 1) return;
            int n = Math.Min(xs.Length, ys.Length);
            var st = g.Save();
            g.SetClip(plot, CombineMode.Intersect);
            using (var pen = new Pen(color, width))
            using (var b = new SolidBrush(color))
            {
                var pts = new List<PointF>(n);
                for (int i = 0; i < n; i++)
                {
                    if (double.IsNaN(xs[i]) || double.IsNaN(ys[i])) continue;
                    float px = MapX(xs[i], plot, x0, x1);
                    float py = logY ? LogMapY(ys[i], plot, y0, y1) : MapY(ys[i], plot, y0, y1);
                    pts.Add(new PointF(px, py));
                }
                if (pts.Count == 1) g.FillEllipse(b, pts[0].X - width, pts[0].Y - width, width * 2, width * 2);
                else if (pts.Count > 1) g.DrawLines(pen, pts.ToArray());

                if (markerSize > 0)
                    foreach (var p in pts)
                        g.FillEllipse(b, p.X - markerSize / 2, p.Y - markerSize / 2, markerSize, markerSize);
            }
            g.Restore(st);
        }

        /// <summary>图例（白色底 + 浅灰边框，元素：点/实线/虚线）</summary>
        public static void Legend(Graphics g, PointF topLeft, IList<LegendItem> items, Font font = null, bool chineseFont = false)
        {
            if (items == null || items.Count == 0) return;
            if (font == null) font = chineseFont ? FCnSmall : FTick;
            float lineH = g.MeasureString("Ag", font).Height + 4f;
            float w = 0;
            foreach (var it in items)
                w = Math.Max(w, 40f + g.MeasureString(it.Label, font).Width);
            var rect = new RectangleF(topLeft.X, topLeft.Y, w + 14f, lineH * items.Count + 8f);

            using (var bg = new SolidBrush(Color.FromArgb(238, 255, 255, 255)))
            using (var bd = new Pen(Color.FromArgb(190, 190, 190), 1f))
            {
                g.FillRectangle(bg, rect);
                g.DrawRectangle(bd, rect.X, rect.Y, rect.Width, rect.Height);
            }
            float y = rect.Y + 4f;
            foreach (var it in items)
            {
                float cy = y + lineH / 2f;
                if (it.Mark == LegendMark.Point)
                {
                    using (var b = new SolidBrush(it.Color))
                        g.FillEllipse(b, rect.X + 7f, cy - it.Size / 2, it.Size, it.Size);
                }
                else
                {
                    using (var pen = new Pen(it.Color, 1.8f))
                    {
                        if (it.Mark == LegendMark.DashedLine)
                        {
                            pen.DashStyle = DashStyle.Custom;
                            pen.DashPattern = new float[] { 6f, 4f };
                        }
                        g.DrawLine(pen, rect.X + 7f, cy, rect.X + 30f, cy);
                    }
                }
                Text(g, it.Label, font, Color.FromArgb(40, 40, 40), rect.X + 36f, y);
                y += lineH;
            }
        }

        /// <summary>自动选择图例角落（哪个象限的数据点最少就放哪）</summary>
        public static PointF BestLegendPosition(RectangleF plot, double[] xs, double[] ys, double x0, double x1, double y0, double y1, float boxW, float boxH)
        {
            int[] cnt = new int[4]; // 0=右上 1=左上 2=右下 3=左下
            if (xs != null && ys != null)
            {
                int n = Math.Min(xs.Length, ys.Length);
                for (int i = 0; i < n; i++)
                {
                    bool right = xs[i] > (x0 + x1) / 2;
                    bool isTop = ys[i] > (y0 + y1) / 2;
                    cnt[(isTop ? 0 : 2) + (right ? 0 : 1)]++;
                }
            }
            int best = 0;
            for (int i = 1; i < 4; i++) if (cnt[i] < cnt[best]) best = i;
            float left = (best % 2 == 1) ? plot.Left + 10f : plot.Right - boxW - 10f;
            float top = (best < 2) ? plot.Top + 10f : plot.Bottom - boxH - 10f;
            return new PointF(left, top);
        }

        /// <summary>图例尺寸估算（配合 BestLegendPosition 使用）</summary>
        public static SizeF LegendSize(Graphics g, IList<LegendItem> items, Font font = null, bool chineseFont = false)
        {
            if (font == null) font = chineseFont ? FCnSmall : FTick;
            float lineH = g.MeasureString("Ag", font).Height + 4f;
            float w = 0;
            foreach (var it in items) w = Math.Max(w, 40f + g.MeasureString(it.Label, font).Width);
            return new SizeF(w + 14f, lineH * items.Count + 8f);
        }

        // ==================== 复合面板 ====================

        /// <summary>
        /// 「预测 vs 真实」散点面板 —— 完整复刻课程设计的 matplotlib 子图：
        /// 标题带 R²、蓝色/绿色散点、红色虚线理想线 y=x、左上角图例、轴标题为英文。
        /// </summary>
        public static void ScatterPanel(Graphics g, RectangleF area, double[] truth, double[] pred,
                                        string title, Color pointColor, string xTitle = "Actual Compressive Strength",
                                        string yTitle = "Predicted Compressive Strength", bool grid = false)
        {
            var plot = Inset(area, 62f, 42f, 20f, 46f);
            if (plot.Width < 40 || plot.Height < 40) return;

            double lo = double.MaxValue, hi = double.MinValue;
            int n = Math.Min(truth == null ? 0 : truth.Length, pred == null ? 0 : pred.Length);
            for (int i = 0; i < n; i++)
            {
                lo = Math.Min(lo, Math.Min(truth[i], pred[i]));
                hi = Math.Max(hi, Math.Max(truth[i], pred[i]));
            }
            if (n == 0 || hi < lo) { lo = 0; hi = 1; }
            double pad = (hi - lo) * 0.06;
            if (pad < 1e-9) pad = Math.Abs(hi) > 0 ? Math.Abs(hi) * 0.05 : 0.5;
            double x0 = lo - pad, x1 = hi + pad;

            TextCenter(g, title, FTitle, CLabel, area.Left + area.Width / 2, area.Top + 2f);
            Axes(g, plot, x0, x1, x0, x1, xTitle, yTitle, FAxis, grid);
            IdealLine(g, plot, x0, x1);
            Scatter(g, plot, x0, x1, x0, x1, truth, pred, pointColor, 6.5f);

            var items = new List<LegendItem>
            {
                new LegendItem("Predicted vs Actual", pointColor, LegendMark.Point),
                new LegendItem("Ideal Fit (y=x)", IdealLineColor, LegendMark.DashedLine)
            };
            var sz = LegendSize(g, items);
            var pos = BestLegendPosition(plot, truth, pred, x0, x1, x0, x1, sz.Width, sz.Height);
            Legend(g, pos, items);
        }

        /// <summary>子块内缩出绘图区</summary>
        public static RectangleF Inset(RectangleF area, float left, float top, float right, float bottom)
        {
            var r = new RectangleF(area.Left + left, area.Top + top,
                                   area.Width - left - right, area.Height - top - bottom);
            if (r.Width < 10) r.Width = 10;
            if (r.Height < 10) r.Height = 10;
            return r;
        }

        /// <summary>分组柱状图（模型精度对比）。values[系列][类别]，返回每组柱宽</summary>
        public static void GroupedBars(Graphics g, RectangleF plot, double y0, double y1,
                                       string[] categories, string[] series, double[][] values,
                                       string yTitle, Func<double, string> valueLabel = null,
                                       bool chineseFont = true, bool showValues = true)
        {
            var font = chineseFont ? FCn : FAxis;
            Axes(g, plot, 0, categories.Length, y0, y1, "", yTitle, font, true, chineseFont);

            int ns = series.Length, nc = categories.Length;
            float groupW = plot.Width / nc;
            float barW = Math.Min(52f, groupW * 0.72f / ns);
            float step = barW + 4f;
            var labels = new List<LegendItem>();

            for (int s = 0; s < ns; s++)
            {
                var c = Palette[s % Palette.Length];
                labels.Add(new LegendItem(series[s], c, LegendMark.Point, 8f));
                using (var b = new SolidBrush(Alpha(c, 235)))
                using (var pen = new Pen(Blend(c, Color.Black, 0.25), 1f))
                {
                    for (int i = 0; i < nc; i++)
                    {
                        double v = values[s][i];
                        if (double.IsNaN(v)) continue;
                        float cx = plot.Left + groupW * (i + 0.5f) + (s - (ns - 1) / 2f) * step;
                        float py = MapY(v, plot, y0, y1);
                        float top = Math.Min(py, MapY(0, plot, y0, y1));
                        float hgt = Math.Abs(MapY(0, plot, y0, y1) - py);
                        var rect = new RectangleF(cx - barW / 2, top, barW, Math.Max(1f, hgt));
                        g.FillRectangle(b, rect);
                        g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                        if (showValues)
                        {
                            string txt = valueLabel != null ? valueLabel(v) : F4(v);
                            TextCenter(g, txt, FCnSmall, Color.FromArgb(70, 70, 70), cx, rect.Y - 16f);
                        }
                    }
                }
            }
            // 类别名
            using (var b = new SolidBrush(CLabel))
                for (int i = 0; i < nc; i++)
                {
                    var sz = g.MeasureString(categories[i], FCnSmall);
                    float cx = plot.Left + groupW * (i + 0.5f);
                    float y = plot.Bottom + 8f;
                    var st = g.Save();
                    g.TranslateTransform(cx, y);
                    if (sz.Width > groupW - 6)
                    {
                        g.TranslateTransform(0, 6f);
                        g.RotateTransform(-28);
                        g.DrawString(categories[i], FCnSmall, b, -sz.Width / 2, 0);
                    }
                    else g.DrawString(categories[i], FCnSmall, b, -sz.Width / 2, 0);
                    g.Restore(st);
                }

            var sz2 = LegendSize(g, labels);
            Legend(g, new PointF(plot.Right - sz2.Width - 6, plot.Top + 6), labels);
        }

        /// <summary>直方图面板（复刻 preprocess.py 的 data.png 风格：填色 + 黑边）</summary>
        public static void HistPanel(Graphics g, RectangleF area, double[] values, string title, Color fill,
                                     int bins = 12, string xTitle = "Value", string yTitle = "Frequency")
        {
            var plot = Inset(area, 58f, 40f, 18f, 46f);
            if (plot.Width < 30 || plot.Height < 30) return;

            double lo = double.MaxValue, hi = double.MinValue;
            foreach (var v in values) { if (v < lo) lo = v; if (v > hi) hi = v; }
            if (hi <= lo) { lo -= 0.5; hi += 0.5; }
            double span = hi - lo;
            lo -= span * 0.02; hi += span * 0.02;
            double w = (hi - lo) / bins;
            var counts = new int[bins];
            foreach (var v in values)
            {
                int k = (int)Math.Floor((v - lo) / w);
                if (k < 0) k = 0; if (k >= bins) k = bins - 1;
                counts[k]++;
            }
            int maxC = 0; foreach (var c in counts) maxC = Math.Max(maxC, c);
            if (maxC == 0) maxC = 1;

            TextCenter(g, title, FCnTitle, CLabel, area.Left + area.Width / 2, area.Top + 2f);
            Axes(g, plot, lo, hi, 0, maxC * 1.08, xTitle, yTitle, FCn, false, true);

            using (var b = new SolidBrush(fill))
            using (var pen = new Pen(Color.FromArgb(20, 20, 20), 1.1f))
            {
                for (int i = 0; i < bins; i++)
                {
                    if (counts[i] == 0) continue;
                    float x = MapX(lo + i * w, plot, lo, hi);
                    float xr = MapX(lo + (i + 1) * w, plot, lo, hi);
                    float yTop = MapY(counts[i], plot, 0, maxC * 1.08);
                    var rect = RectangleF.FromLTRB(x, yTop, xr, plot.Bottom);
                    g.FillRectangle(b, rect);
                    g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                }
            }
        }

        /// <summary>横向条形图：左侧为名称，右侧条形长度按数值，条形末端标注数值（用于排名/汇总页）</summary>
        public static void HBars(Graphics g, RectangleF area, string title, string[] labels, double[] values,
                                 Func<double, string> valueLabel, Color[] colors = null, double vMin = 0, double vMax = 0)
        {
            if (labels == null || labels.Length == 0) return;
            if (!string.IsNullOrEmpty(title))
                TextCenter(g, title, FCn, CLabel, area.Left + area.Width / 2, area.Top);

            float labelW = 130f;
            var plot = new RectangleF(area.Left + labelW + 8f, area.Top + 26f,
                                      area.Width - labelW - 90f, area.Height - 34f);
            if (plot.Width < 40 || plot.Height < 30) return;

            if (vMax <= vMin)
            {
                vMax = double.MinValue; vMin = double.MaxValue;
                foreach (var v in values) { if (double.IsNaN(v)) continue; if (v > vMax) vMax = v; if (v < vMin) vMin = v; }
                if (vMax <= vMin) { vMax = vMin + 1; }
                vMin = Math.Min(vMin, 0);
                vMax = vMax + (vMax - vMin) * 0.12;
            }

            float rowH = plot.Height / labels.Length;
            float barH = Math.Min(26f, rowH * 0.62f);
            using (var axis = new Pen(CSpine, 1f))
            using (var b = new SolidBrush(Color.FromArgb(235, 235, 235)))
            {
                // 竖向网格 + 底轴
                var ticks = Ticks(vMin, vMax, 5);
                double step = ticks.Count > 1 ? ticks[1] - ticks[0] : (vMax - vMin);
                int dec = DecimalsFor(step);
                foreach (var t in ticks)
                {
                    float px = MapX(t, plot, vMin, vMax);
                    g.DrawLine(new Pen(CGrid, 1f), px, plot.Top, px, plot.Bottom);
                    TextCenter(g, FmtTick(t, dec), FCnTick, CLabel, px, plot.Bottom + 4f);
                }

                for (int i = 0; i < labels.Length; i++)
                {
                    float cy = plot.Top + rowH * (i + 0.5f);
                    float y = cy - barH / 2;
                    // 名称
                    TextRight(g, labels[i], FCnTick, CLabel, area.Left + labelW, cy - g.MeasureString(labels[i], FCnTick).Height / 2);
                    // 条形
                    var c = colors != null && colors.Length > 0 ? colors[i % colors.Length] : Palette[i % Palette.Length];
                    using (var fill = new SolidBrush(Alpha(c, 220)))
                    using (var pen = new Pen(Blend(c, Color.Black, 0.25), 1f))
                    {
                        float x0 = MapX(vMin, plot, vMin, vMax);
                        float x1 = MapX(Math.Max(values[i], vMin), plot, vMin, vMax);
                        var rect = new RectangleF(x0, y, Math.Max(1f, x1 - x0), barH);
                        g.FillRectangle(fill, rect);
                        g.DrawRectangle(pen, rect.X, rect.Y, rect.Width, rect.Height);
                    }
                    // 数值
                    string txt = valueLabel != null ? valueLabel(values[i]) : F4(values[i]);
                    Text(g, txt, FCnTick, Color.FromArgb(70, 70, 70),
                        MapX(values[i], plot, vMin, vMax) + 8f, cy - g.MeasureString(txt, FCnTick).Height / 2);
                }
                g.DrawLine(axis, plot.Left, plot.Top, plot.Left, plot.Bottom);
            }
        }

        /// <summary>指标明细表：表头 + 隔行浅底 + 灰色线</summary>
        public static void Table(Graphics g, RectangleF area, string[] headers, string[][] rows, float[] weights, float rowH = 30f)
        {
            if (headers == null || headers.Length == 0) return;
            if (weights == null)
            {
                weights = new float[headers.Length];
                for (int i = 0; i < weights.Length; i++) weights[i] = 1f;
            }
            float total = 0; foreach (var w in weights) total += w;
            var colX = new float[headers.Length + 1];
            colX[0] = area.Left;
            for (int i = 0; i < headers.Length; i++) colX[i + 1] = colX[i] + area.Width * weights[i] / total;

            using (var head = new SolidBrush(Color.FromArgb(238, 242, 248)))
            using (var alt = new SolidBrush(Color.FromArgb(250, 251, 253)))
            using (var line = new Pen(Color.FromArgb(205, 212, 222), 1f))
            using (var txt = new SolidBrush(Color.FromArgb(45, 50, 60)))
            using (var headTxt = new SolidBrush(Color.FromArgb(28, 48, 80)))
            {
                float y = area.Top;
                g.FillRectangle(head, area.Left, y, area.Width, rowH);
                for (int i = 0; i < headers.Length; i++)
                {
                    var sz = g.MeasureString(headers[i], FCn);
                    g.DrawString(headers[i], FCn, headTxt, colX[i] + (colX[i + 1] - colX[i] - sz.Width) / 2, y + (rowH - sz.Height) / 2);
                }
                g.DrawLine(line, area.Left, y + rowH, area.Right, y + rowH);
                y += rowH;

                for (int r = 0; r < rows.Length; r++)
                {
                    if (r % 2 == 1) g.FillRectangle(alt, area.Left, y, area.Width, rowH);
                    for (int c = 0; c < headers.Length && c < rows[r].Length; c++)
                    {
                        string s = rows[r][c] ?? "";
                        var sz = g.MeasureString(s, FCnTick);
                        float tx = c == 0 ? colX[c] + 10f : colX[c] + (colX[c + 1] - colX[c] - sz.Width) / 2;
                        g.DrawString(s, FCnTick, txt, tx, y + (rowH - sz.Height) / 2);
                    }
                    g.DrawLine(line, area.Left, y + rowH, area.Right, y + rowH);
                    y += rowH;
                    if (y > area.Bottom) break;
                }
                g.DrawRectangle(line, area.Left, area.Top, area.Width, y - area.Top);
            }
        }
    }
}
