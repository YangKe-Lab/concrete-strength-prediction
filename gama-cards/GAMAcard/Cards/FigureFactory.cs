using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;

namespace MyGamaCards
{
    /// <summary>
    /// 图表工厂 —— 把计算结果变成一组「好看的图」（ChartFigure），供卡片提交给图表窗口显示并导出 PNG。
    ///
    /// 设计原则：
    /// 1. 风格对齐课程设计里的 matplotlib 图（蓝=训练集、绿=测试集、红虚线=y=x 理想线、
    ///    灰虚线网格、带框坐标轴、左上角图例、标题里带 R²）；
    /// 2. 不依赖 E-GAMA 任何类型（只有 System.Drawing），因此可以脱离 GAMA 单独渲染成 PNG 做视觉自检；
    /// 3. 所有绘制按传入的逻辑尺寸自适应，导出时放大 2 倍重绘，图片和屏幕一致且清晰。
    /// </summary>
    public static class FigureFactory
    {
        public static List<ChartFigure> L(params ChartFigure[] figs) => new List<ChartFigure>(figs);

        // ==================================================================
        //  一、输入数据卡：数据分布（直方图）+ 特征-强度散点
        // ==================================================================

        /// <summary>
        /// 数据集总览：① 四联直方图（复刻 preprocess.py 的 data.png）；② 特征×强度散点（带最小二乘趋势线）
        /// </summary>
        public static List<ChartFigure> DatasetFigures(string dsLabel, string[] featNames, double[][] X, double[] Y)
        {
            var figs = new List<ChartFigure>();
            if (featNames == null) featNames = new[] { "特征1", "特征2", "特征3" };

            // 收集各列
            var cols = new double[featNames.Length][];
            for (int j = 0; j < featNames.Length; j++) cols[j] = Column(X, j);

            // ---------- 页 1：四联直方图 ----------
            var histColors = new[] { Mpl.HistWater, Mpl.HistFlyAsh, Mpl.HistSilica, Mpl.HistStrength };
            figs.Add(new ChartFigure("数据分布(直方图)", (g, w, h) =>
            {
                Mpl.FigureTitle(g, w, "数据分布直方图", string.Format("{0}　样本数 = {1}　（复刻课程设计 data.png）", dsLabel, Y == null ? 0 : Y.Length), true);
                var area = new RectangleF(30, 62, w - 60, h - 82);
                double[][] series = new double[featNames.Length + 1][];
                string[] names = new string[featNames.Length + 1];
                for (int j = 0; j < featNames.Length; j++) { series[j] = cols[j]; names[j] = featNames[j]; }
                series[featNames.Length] = Y ?? new double[0];
                names[featNames.Length] = "抗压强度";

                for (int i = 0; i < series.Length; i++)
                {
                    int row = i / 2, col = i % 2;
                    var cell = Mpl.SubRect(area, 2, 2, row, col, 28f, 18f);
                    Mpl.HistPanel(g, cell, series[i] ?? new double[0], names[i], histColors[i % histColors.Length]);
                }
            }, "四个变量的频数分布，用于判断数据分布形态（缺失值填充策略的依据）"));

            // ---------- 页 2：特征 × 强度 散点（带趋势线） ----------
            if (Y != null && Y.Length > 1 && cols.Length > 0)
            {
                figs.Add(new ChartFigure("特征-强度散点", (g, w, h) =>
                {
                    Mpl.FigureTitle(g, w, "特征与抗压强度的关系", string.Format("{0}　散点为样本，黑虚线为最小二乘趋势线", dsLabel), true);
                    var area = new RectangleF(30, 62, w - 60, h - 82);
                    int show = Math.Min(2, cols.Length);
                    for (int j = 0; j < show; j++)
                    {
                        var cell = Mpl.SubRect(area, 1, show, 0, j, 34f, 0f);
                        ScatterWithTrend(g, cell, cols[j], Y, featNames[j] + "（标准化）", "抗压强度 (MPa)");
                    }
                }, "查看单特征与强度的线性相关趋势"));
            }
            return figs;
        }

        private static void ScatterWithTrend(Graphics g, RectangleF area, double[] xs, double[] ys, string xTitle, string yTitle)
        {
            var plot = Mpl.Inset(area, 62f, 34f, 22f, 46f);
            if (plot.Width < 40 || plot.Height < 40) return;

            double x0 = Min(xs), x1 = Max(xs), y0 = Min(ys), y1 = Max(ys);
            Pad(ref x0, ref x1, 0.06); Pad(ref y0, ref y1, 0.06);

            Mpl.Axes(g, plot, x0, x1, y0, y1, xTitle, yTitle, Mpl.FCn, false, true);
            Mpl.Scatter(g, plot, x0, x1, y0, y1, xs, ys, Mpl.CBlue, 6f);

            // 最小二乘趋势线 y = a + b x
            double sx = 0, sy = 0, sxx = 0, sxy = 0; int n = Math.Min(xs.Length, ys.Length);
            for (int i = 0; i < n; i++) { sx += xs[i]; sy += ys[i]; sxx += xs[i] * xs[i]; sxy += xs[i] * ys[i]; }
            double den = n * sxx - sx * sx;
            if (Math.Abs(den) > 1e-12 && n > 1)
            {
                double b = (n * sxy - sx * sy) / den, a = (sy - b * sx) / n;
                using (var pen = new Pen(Color.FromArgb(40, 40, 40), 1.8f))
                {
                    pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
                    pen.DashPattern = new float[] { 6f, 4f };
                    var p1 = new PointF(Mpl.MapX(x0, plot, x0, x1), Mpl.MapY(a + b * x0, plot, y0, y1));
                    var p2 = new PointF(Mpl.MapX(x1, plot, x0, x1), Mpl.MapY(a + b * x1, plot, y0, y1));
                    g.DrawLine(pen, p1, p2);
                }
                Mpl.Text(g, string.Format("y = {0:F3} {1} {2:F3}·x", a, b < 0 ? "-" : "+", Math.Abs(b)), Mpl.FCnSmall, Mpl.CNote, plot.Left + 10, plot.Top + 8);
            }
        }

        // ==================================================================
        //  二、训练卡：损失曲线（复刻 ANN_ModelX_Train_MSE.png）
        // ==================================================================

        public static ChartFigure LossFigure(string modelName, IList<double> loss, string subtitle)
        {
            var data = new double[loss == null ? 0 : loss.Count];
            if (loss != null) for (int i = 0; i < loss.Count; i++) data[i] = loss[i];

            return new ChartFigure("训练损失曲线", (g, w, h) =>
            {
                Mpl.FigureTitle(g, w, (modelName ?? "ANN") + " Train MSE", subtitle ?? "", false);
                var area = new RectangleF(30, 62, w - 60, h - 82);

                // 左：全量损失曲线（与课程设计图一致）
                var left = Mpl.SubRect(area, 1, 2, 0, 0, 40f, 0f);
                LossPanel(g, left, data, 0, data.Length, "全量曲线（共 " + data.Length + " 轮）", "Epochs", "MSE", false);

                // 右：后半程放大（看收敛细节）
                int tail = Math.Max(50, data.Length / 5);
                int start = Math.Max(0, data.Length - tail);
                var right = Mpl.SubRect(area, 1, 2, 0, 1, 40f, 0f);
                LossPanel(g, right, data, start, data.Length,
                    "收敛细节（后 " + (data.Length - start) + " 轮）", "Epochs", "MSE", false);
            }, "每轮训练的平均 MSE（蓝线），灰色虚线网格");
        }

        /// <summary>单条损失曲线面板（matplotlib 复刻：蓝色实线 + 灰色虚线网格 + 图例）</summary>
        public static void LossPanel(Graphics g, RectangleF area, double[] loss, int from, int to,
                                     string title, string xTitle, string yTitle, bool logY)
        {
            var plot = Mpl.Inset(area, 68f, 40f, 24f, 50f);
            if (plot.Width < 40 || plot.Height < 40) return;
            Mpl.TextCenter(g, title, Mpl.FTitle, Mpl.CLabel, area.Left + area.Width / 2, area.Top + 2);

            int n = to - from;
            var xs = new double[n];
            var ys = new double[n];
            for (int i = 0; i < n; i++) { xs[i] = from + i; ys[i] = loss[from + i]; }

            double yMax = 0, yMin = double.MaxValue;
            for (int i = 0; i < n; i++) { if (ys[i] > yMax) yMax = ys[i]; if (ys[i] < yMin) yMin = ys[i]; }
            if (yMax <= 0) yMax = 1;
            if (yMin > yMax) yMin = 0;

            if (logY)
            {
                Mpl.AxesLogY(g, plot, from, to, Math.Max(yMin * 0.6, 1e-10), yMax * 2.0, xTitle, yTitle, Mpl.FAxis, true);
                Mpl.Series(g, plot, from, to, Math.Max(yMin * 0.6, 1e-10), yMax * 2.0, xs, ys, Mpl.CBlue, 2.2f, 0f, true);
            }
            else
            {
                double yTop = yMin - (yMax - yMin) * 0.08;
                if (yTop < 0) yTop = 0;
                double yHi = yMax + (yMax - yMin) * 0.12;
                if (yHi <= yTop) yHi = yTop + 1;
                Mpl.Axes(g, plot, from, to, yTop, yHi, xTitle, yTitle, Mpl.FAxis, true);
                Mpl.Series(g, plot, from, to, yTop, yHi, xs, ys, Mpl.CBlue, 2.4f);
            }

            var items = new List<LegendItem> { new LegendItem("Training MSE", Mpl.CBlue, LegendMark.Line) };
            var sz = Mpl.LegendSize(g, items);
            Mpl.Legend(g, Mpl.BestLegendPosition(plot, xs, ys, from, to, 0, yMax, sz.Width, sz.Height), items);
        }

        // ==================================================================
        //  三、模型卡：预测 vs 真实（复刻 ANN_ModelX.png / LinearModelX.png）
        // ==================================================================

        /// <summary>
        /// 「预测 vs 真实」：左=训练集（蓝点）、右=测试集（绿点），红色虚线为理想线 y=x，标题带 R²。
        /// 与课程设计 ANN_Model1.png / LinearModel1.png 的 1×2 布局完全一致。
        /// </summary>
        public static ChartFigure ScatterFitFigure(ModelReport r)
        {
            return new ChartFigure("预测vs真实", (g, w, h) =>
            {
                string sub = string.Format("{0}{1}　测试集 R² = {2}（越接近 1 越好，点越贴近红虚线越准）",
                    r.DisplayName, string.IsNullOrEmpty(r.Architecture) ? "" : " [" + r.Architecture + "]",
                    Mpl.F4(r.HasTest ? r.TestR2 : r.TrainR2));
                Mpl.FigureTitle(g, w, "预测值与真实值对照（复刻课程设计图风格）", sub, true);

                var area = new RectangleF(24, 64, w - 48, h - 84);
                if (r.HasTest)
                {
                    var left = Mpl.SubRect(area, 1, 2, 0, 0, 46f, 0f);
                    var right = Mpl.SubRect(area, 1, 2, 0, 1, 46f, 0f);
                    Mpl.ScatterPanel(g, left, r.TrainTruth, r.TrainPred,
                        string.Format("{0} Training: R² = {1}", Named(r), Mpl.F4(r.TrainR2)), Mpl.CBlue);
                    Mpl.ScatterPanel(g, right, r.TestTruth, r.TestPred,
                        string.Format("{0} Testing: R² = {1}", Named(r), Mpl.F4(r.TestR2)), Mpl.CGreen);
                }
                else
                {
                    Mpl.ScatterPanel(g, area, r.TrainTruth, r.TrainPred,
                        string.Format("{0} {1}: R² = {2}", Named(r), r.SetLabel, Mpl.F4(r.TrainR2)), Mpl.CBlue);
                    Mpl.TextCenter(g, "（未连接测试集：仅显示" + r.SetLabel + "；在训练卡上连接测试集后可自动生成右图的测试集对照）",
                        Mpl.FCnSmall, Mpl.CNote, area.Left + area.Width / 2, area.Bottom - 20);
                }
            }, "训练集/测试集预测值与真实值对照，红线为理想线 y=x");
        }

        private static string Named(ModelReport r)
        {
            return string.IsNullOrWhiteSpace(r.DisplayName) ? "模型" : r.DisplayName.Trim();
        }

        /// <summary>逐样本对比：真实值（黑点连线）vs 模型预测（彩色线），训练/测试各一栏</summary>
        public static ChartFigure PerSampleFigure(ModelReport r)
        {
            return new ChartFigure("逐样本对比", (g, w, h) =>
            {
                Mpl.FigureTitle(g, w, "逐样本预测对比", string.Format("{0}　黑点=真实抗压强度，彩线=模型预测", r.DisplayName), true);
                var area = new RectangleF(30, 64, w - 60, h - 84);

                bool hasTest = r.HasTest && r.TestTruth != null && r.TestTruth.Length > 0;
                int panels = hasTest ? 2 : 1;
                var pTrain = Mpl.SubRect(area, 1, panels, 0, 0, 40f, 0f);
                PerSamplePanel(g, pTrain, r.TrainTruth, r.TrainPred,
                    string.Format("{0}（n={1}，R²={2}）", r.SetLabel, r.TrainCount, Mpl.F4(r.TrainR2)));
                if (hasTest)
                    PerSamplePanel(g, Mpl.SubRect(area, 1, panels, 0, 1, 40f, 0f), r.TestTruth, r.TestPred,
                        string.Format("测试集（n={0}，R²={1}）", r.TestCount, Mpl.F4(r.TestR2)));
            }, "每根样本的预测偏差一眼可见，用于找出误差大的样本");
        }

        private static void PerSamplePanel(Graphics g, RectangleF area, double[] truth, double[] pred, string title)
        {
            var plot = Mpl.Inset(area, 68f, 38f, 26f, 50f);
            if (plot.Width < 40 || plot.Height < 40) return;
            Mpl.TextCenter(g, title, Mpl.FCn, Mpl.CLabel, area.Left + area.Width / 2, area.Top + 2);

            if (truth == null || pred == null || truth.Length == 0) return;
            int n = Math.Min(truth.Length, pred.Length);
            double lo = Math.Min(Min(truth), Min(pred)), hi = Math.Max(Max(truth), Max(pred));
            Pad(ref lo, ref hi, 0.12);

            var xs = new double[n];
            for (int i = 0; i < n; i++) xs[i] = i + 1;
            double xStep = Math.Max(1, Math.Round(n / 8.0));

            Mpl.Axes(g, plot, 1, Math.Max(2, n), lo, hi, "样本序号", "抗压强度 (MPa)", Mpl.FCn, true, true, xStep);
            Mpl.Series(g, plot, 1, Math.Max(2, n), lo, hi, xs, pred, Mpl.CBlue, 1.6f, 4.5f);
            Mpl.Series(g, plot, 1, Math.Max(2, n), lo, hi, xs, truth, Color.FromArgb(30, 30, 30), 1.2f, 5f);

            var items = new List<LegendItem>
            {
                new LegendItem("真实值", Color.FromArgb(30, 30, 30), LegendMark.Point, 7f),
                new LegendItem("模型预测", Mpl.CBlue, LegendMark.Line)
            };
            var lsz = Mpl.LegendSize(g, items, Mpl.FCnSmall, true);
            Mpl.Legend(g, Mpl.BestLegendPosition(plot, truth, pred, 1, Math.Max(2, n), lo, hi, lsz.Width, lsz.Height),
                items, Mpl.FCnSmall, true);
        }

        // ==================================================================
        //  四、结果对比卡：多模型可视化对比
        // ==================================================================

        public static List<ChartFigure> CompareFigures(IList<ModelReport> input)
        {
            var reports = new List<ModelReport>();
            if (input != null)
                foreach (var r in input)
                    if (r != null && r.TrainCount > 0) reports.Add(r);

            var figs = new List<ChartFigure>();
            if (reports.Count == 0) return figs;

            // 排序：测试集 R² 降序（无测试集用训练集 R²）
            var ranked = new List<ModelReport>(reports);
            ranked.Sort((a, b) => b.SortScore().CompareTo(a.SortScore()));

            string[] names = new string[reports.Count];
            for (int i = 0; i < reports.Count; i++) names[i] = reports[i].DisplayName;

            // 主指标槽的数据集名（训练卡/线性卡=“训练集”，评估卡=“评估集”）
            string trainLabel = string.IsNullOrWhiteSpace(reports[0].SetLabel) ? "训练集" : reports[0].SetLabel;

            bool anyTest = false;
            foreach (var r in reports) if (r.HasTest) anyTest = true;

            // ---------- 页 1：精度总览（R² / MSE / MAE 三组柱状 + R²-MSE 定位图） ----------
            figs.Add(new ChartFigure("精度总览", (g, w, h) =>
            {
                var best = ranked[0];
                Mpl.FigureTitle(g, w, "模型精度总览对比",
                    string.Format("共 {0} 个模型　|　最优：{1}（{2} R² = {3}）　|　{4}",
                        reports.Count, best.DisplayName, best.HasTest ? "测试集" : "训练集",
                        Mpl.F4(best.HasTest ? best.TestR2 : best.TrainR2),
                        anyTest ? "含测试集指标" : "未连接测试集，仅训练集指标"), true);

                var area = new RectangleF(28, 62, w - 56, h - 82);
                var seriesNames = anyTest ? new[] { trainLabel, "测试集" } : new[] { trainLabel };
                int ns = seriesNames.Length;

                // R²
                var r2 = new double[ns][];
                for (int s = 0; s < ns; s++)
                {
                    r2[s] = new double[reports.Count];
                    for (int i = 0; i < reports.Count; i++) r2[s][i] = s == 0 ? reports[i].TrainR2 : reports[i].TestR2;
                }
                double r2lo = 0, r2hi = 1.0;
                foreach (var v in r2[0]) if (!double.IsNaN(v) && v < r2lo) r2lo = v;
                for (int s = 1; s < ns; s++) foreach (var v in r2[s]) if (!double.IsNaN(v) && v < r2lo) r2lo = v;

                // MSE / MAE
                var mse = new double[ns][];
                var mae = new double[ns][];
                for (int s = 0; s < ns; s++)
                {
                    mse[s] = new double[reports.Count];
                    mae[s] = new double[reports.Count];
                    for (int i = 0; i < reports.Count; i++)
                    {
                        mse[s][i] = s == 0 ? reports[i].TrainMSE : reports[i].TestMSE;
                        mae[s][i] = s == 0 ? reports[i].TrainMAE : reports[i].TestMAE;
                    }
                }

                var cellR2 = Mpl.SubRect(area, 2, 2, 0, 0, 34f, 26f);
                Mpl.GroupedBars(g, Mpl.Inset(cellR2, 62f, 30f, 16f, 56f), r2lo * 1.05, r2hi * 1.08,
                    names, seriesNames, r2, "决定系数 R²（越接近 1 越好）", v => v.ToString("F4"));

                var cellMse = Mpl.SubRect(area, 2, 2, 0, 1, 34f, 26f);
                Mpl.GroupedBars(g, Mpl.Inset(cellMse, 62f, 30f, 16f, 56f), 0, Max2(mse) * 1.18,
                    names, seriesNames, mse, "均方误差 MSE（越小越好）", v => v.ToString("F2"));

                var cellMae = Mpl.SubRect(area, 2, 2, 1, 0, 34f, 26f);
                Mpl.GroupedBars(g, Mpl.Inset(cellMae, 62f, 30f, 16f, 56f), 0, Max2(mae) * 1.18,
                    names, seriesNames, mae, "平均绝对误差 MAE（MPa，越小越好）", v => v.ToString("F2"));

                // 第 4 格：R²-MSE 定位图（越靠左上越好）
                var cellMap = Mpl.SubRect(area, 2, 2, 1, 1, 34f, 26f);
                R2MseMap(g, cellMap, reports);

            }, "R²/MSE/MAE 柱状对比 + R²-MSE 定位图（R² 越高、MSE 越低 = 越好）"));

            // ---------- 页 2：全模型 预测 vs 真实 对照 ----------
            figs.Add(new ChartFigure("全模型对照", (g, w, h) =>
            {
                Mpl.FigureTitle(g, w, "全模型 预测 vs 真实 对照",
                    anyTest ? "左=训练集，右=测试集；颜色区分模型，红虚线为理想线 y=x" : "训练集；红虚线为理想线 y=x", true);
                var area = new RectangleF(24, 62, w - 48, h - 82);

                if (anyTest)
                {
                    OverlayPanel(g, Mpl.SubRect(area, 1, 2, 0, 0, 46f, 0f), reports, false, trainLabel);
                    OverlayPanel(g, Mpl.SubRect(area, 1, 2, 0, 1, 46f, 0f), reports, true, "测试集");
                }
                else
                {
                    OverlayPanel(g, area, reports, false, trainLabel);
                }
            }, "多个模型的散点叠在同一张对照图上，谁更贴近理想线一目了然"));

            // ---------- 页 3：逐样本对比 ----------
            figs.Add(new ChartFigure("逐样本对比", (g, w, h) =>
            {
                bool test = anyTest;
                Mpl.FigureTitle(g, w, "逐样本预测对比（" + (test ? "测试集" : trainLabel) + "）",
                    "黑点 = 真实抗压强度，彩线 = 各模型预测值；线与黑点贴合程度即精度", true);
                var plot = Mpl.Inset(new RectangleF(30, 62, w - 60, h - 82), 72f, 40f, 30f, 56f);

                double lo = double.MaxValue, hi = double.MinValue;
                int n = 0;
                foreach (var r in reports)
                {
                    var t = test ? r.TestTruth : r.TrainTruth;
                    var p = test ? r.TestPred : r.TrainPred;
                    if (t == null || p == null) continue;
                    n = Math.Max(n, Math.Min(t.Length, p.Length));
                    foreach (var v in t) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
                    foreach (var v in p) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
                }
                if (n == 0 || hi <= lo) { lo = 0; hi = 1; }
                Pad(ref lo, ref hi, 0.12);
                var xs = new double[n];
                for (int i = 0; i < n; i++) xs[i] = i + 1;
                double xStep = Math.Max(1, Math.Round(n / 9.0));

                Mpl.Axes(g, plot, 1, Math.Max(2, n), lo, hi, "样本序号", "抗压强度 (MPa)", Mpl.FCn, true, true, xStep);

                // 先画各模型预测线，最后把真实值画在最上层，避免被彩色线盖住
                int k = 0;
                foreach (var r in ranked)
                {
                    var p = test ? r.TestPred : r.TrainPred;
                    if (p == null || p.Length == 0) continue;
                    Mpl.Series(g, plot, 1, Math.Max(2, n), lo, hi, xs, p,
                        Mpl.Palette[k % Mpl.Palette.Length], 1.8f, 3f);
                    k++;
                }

                // 真实值：黑点 + 细线，画在最上层（清晰可辨）
                double[] truthSeries = null;
                foreach (var r in ranked)
                {
                    var t = test ? r.TestTruth : r.TrainTruth;
                    if (t != null && t.Length > 0) { truthSeries = t; break; }
                }
                if (truthSeries != null)
                    Mpl.Series(g, plot, 1, Math.Max(2, n), lo, hi, xs, truthSeries,
                        Color.FromArgb(15, 15, 15), 1.4f, 6.5f);

                var items = new List<LegendItem>();
                k = 0;
                foreach (var r in ranked)
                {
                    var p = test ? r.TestPred : r.TrainPred;
                    if (p == null || p.Length == 0) continue;
                    double r2 = test ? r.TestR2 : r.TrainR2;
                    items.Add(new LegendItem(string.Format("{0} (R²={1})", r.DisplayName, Mpl.F4(r2)),
                        Mpl.Palette[k % Mpl.Palette.Length], LegendMark.Line));
                    k++;
                }
                items.Insert(0, new LegendItem("真实值", Color.FromArgb(25, 25, 25), LegendMark.Point, 8f));
                var sz = Mpl.LegendSize(g, items, Mpl.FCnSmall, true);
                Mpl.Legend(g, new PointF(plot.Right - sz.Width - 8, plot.Top + 8), items, Mpl.FCnSmall, true);
            }, "每个样本上各模型的预测值与真实值对比"));

            // ---------- 页 4：训练损失对比（仅 ANN 有） ----------
            var withLoss = new List<ModelReport>();
            foreach (var r in ranked)
                if (r.LossHistory != null && r.LossHistory.Count > 1) withLoss.Add(r);

            if (withLoss.Count > 0)
            {
                figs.Add(new ChartFigure("训练损失对比", (g, w, h) =>
                {
                    Mpl.FigureTitle(g, w, "训练损失曲线对比",
                        "对数纵轴；下降更快、更低者为更优的训练过程", true);
                    var plot = Mpl.Inset(new RectangleF(30, 62, w - 60, h - 82), 86f, 40f, 34f, 56f);

                    double lo = double.MaxValue, hi = double.MinValue;
                    int xMax = 1;
                    foreach (var r in withLoss)
                    {
                        xMax = Math.Max(xMax, r.LossHistory.Count);
                        foreach (var v in r.LossHistory) { if (v > 0 && v < lo) lo = v; if (v > hi) hi = v; }
                    }
                    if (lo <= 0) lo = 1e-6;
                    if (hi <= lo) hi = lo * 10;

                    Mpl.AxesLogY(g, plot, 0, xMax, lo * 0.7, hi * 1.6, "Epochs", "MSE（对数轴）", Mpl.FAxis, true);
                    var items = new List<LegendItem>();
                    int k = 0;
                    foreach (var r in withLoss)
                    {
                        var xs = new double[r.LossHistory.Count];
                        for (int i = 0; i < xs.Length; i++) xs[i] = i;
                        var c = Mpl.Palette[k % Mpl.Palette.Length];
                        Mpl.Series(g, plot, 0, xMax, lo * 0.7, hi * 1.6, xs, r.LossHistory.ToArray(), c, 2f, 0f, true);
                        items.Add(new LegendItem(string.Format("{0}（最终 {1:F4}）", r.DisplayName, r.LastLoss()), c, LegendMark.Line));
                        k++;
                    }
                    var sz = Mpl.LegendSize(g, items, Mpl.FCnSmall, true);
                    Mpl.Legend(g, new PointF(plot.Right - sz.Width - 8, plot.Top + 8), items, Mpl.FCnSmall, true);
                }, "多模型损失曲线叠加（对数轴），对比收敛速度与最终精度"));
            }

            // ---------- 页 5：指标明细表 ----------
            figs.Add(new ChartFigure("指标明细表", (g, w, h) =>
            {
                Mpl.FigureTitle(g, w, "模型指标明细表",
                    string.Format("按{0} R² 降序排列", anyTest ? "测试集" : "训练集"), true);

                var headers = new[] { "排名", "模型", "结构/特征", trainLabel + " R²", "测试集 R²", trainLabel + " MSE", "测试集 MSE", trainLabel + " MAE", "测试集 MAE" };
                var rows = new List<string[]>();
                int rank = 1;
                foreach (var r in ranked)
                {
                    rows.Add(new[]
                    {
                        rank.ToString(), r.DisplayName, r.Architecture,
                        Mpl.F4(r.TrainR2), r.HasTest ? Mpl.F4(r.TestR2) : "—",
                        F2(r.TrainMSE), r.HasTest ? F2(r.TestMSE) : "—",
                        F2(r.TrainMAE), r.HasTest ? F2(r.TestMAE) : "—"
                    });
                    rank++;
                }
                var area = new RectangleF(60, 96, w - 120, Math.Min(h - 170, 40f * (rows.Count + 1)));
                Mpl.Table(g, area, headers, rows.ToArray(),
                    new float[] { 0.5f, 1.5f, 1.6f, 1f, 1f, 1f, 1f, 1f, 1f }, 40f);

                var best = ranked[0];
                string conclusion = string.Format(
                    "结论：{0} 表现最好（{1} R² = {2}，MSE = {3}，MAE = {4}），已按指标自动排序。",
                    best.DisplayName, best.HasTest ? "测试集" : "训练集",
                    Mpl.F4(best.HasTest ? best.TestR2 : best.TrainR2),
                    F2(best.HasTest ? best.TestMSE : best.TrainMSE),
                    F2(best.HasTest ? best.TestMAE : best.TrainMAE));
                Mpl.Text(g, conclusion, Mpl.FCn, Color.FromArgb(30, 70, 130), 60, area.Bottom + 16);
                Mpl.Text(g, "★ 判断标准：R² 越接近 1、MSE 与 MAE 越小，模型精度越高；测试集指标更能反映泛化能力。",
                    Mpl.FCnSmall, Mpl.CNote, 60, area.Bottom + 42);

                // 表格下方补一张横向排名条形图，把“谁最好”一眼看清（也避免页面留有大量空白）
                var barArea = new RectangleF(60, area.Bottom + 74, w - 160, h - area.Bottom - 110);
                if (barArea.Height > 90)
                {
                    var labels = new string[ranked.Count];
                    var vals = new double[ranked.Count];
                    for (int i = 0; i < ranked.Count; i++)
                    {
                        labels[i] = ranked[i].DisplayName;
                        vals[i] = ranked[i].HasTest ? ranked[i].TestR2 : ranked[i].TrainR2;
                    }
                    Mpl.HBars(g, barArea, string.Format("{0} R² 排名（横向条形，越长越好）", anyTest ? "测试集" : "训练集"),
                        labels, vals, v => Mpl.F4(v), null, 0, 1.05);
                }
            }, "所有模型的指标汇总表 + 自动排序 + 结论"));
            return figs;
        }

        /// <summary>多模型叠加散点面板</summary>
        private static void OverlayPanel(Graphics g, RectangleF area, IList<ModelReport> reports, bool useTest, string title)
        {
            var plot = Mpl.Inset(area, 70f, 40f, 24f, 52f);
            if (plot.Width < 40 || plot.Height < 40) return;
            Mpl.TextCenter(g, title, Mpl.FCnTitle, Mpl.CLabel, area.Left + area.Width / 2, area.Top + 2);

            double lo = double.MaxValue, hi = double.MinValue;
            bool any = false;
            foreach (var r in reports)
            {
                var t = useTest ? r.TestTruth : r.TrainTruth;
                var p = useTest ? r.TestPred : r.TrainPred;
                if (t == null || p == null || t.Length == 0) continue;
                any = true;
                foreach (var v in t) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
                foreach (var v in p) { lo = Math.Min(lo, v); hi = Math.Max(hi, v); }
            }
            if (!any) { Mpl.TextCenter(g, "（无数据）", Mpl.FCn, Mpl.CNote, area.Left + area.Width / 2, area.Top + area.Height / 2); return; }
            Pad(ref lo, ref hi, 0.06);

            Mpl.Axes(g, plot, lo, hi, lo, hi, "真实抗压强度 (MPa)", "预测抗压强度 (MPa)", Mpl.FCn, false, true);
            Mpl.IdealLine(g, plot, lo, hi);

            var items = new List<LegendItem>();
            var allX = new List<double>();
            var allY = new List<double>();
            int k = 0;
            foreach (var r in reports)
            {
                var t = useTest ? r.TestTruth : r.TrainTruth;
                var p = useTest ? r.TestPred : r.TrainPred;
                if (t == null || p == null || t.Length == 0) continue;
                var c = Mpl.Palette[k % Mpl.Palette.Length];
                Mpl.Scatter(g, plot, lo, hi, lo, hi, t, p, Mpl.Alpha(c, 200), 6.5f);
                for (int i = 0; i < t.Length; i++) { allX.Add(t[i]); allY.Add(p[i]); }
                double r2 = useTest ? r.TestR2 : r.TrainR2;
                items.Add(new LegendItem(string.Format("{0} (R²={1})", r.DisplayName, Mpl.F4(r2)), c, LegendMark.Point, 7f));
                k++;
            }
            items.Add(new LegendItem("理想线 y=x", Mpl.IdealLineColor, LegendMark.DashedLine));
            var sz = Mpl.LegendSize(g, items, Mpl.FCnSmall, true);
            var pos = Mpl.BestLegendPosition(plot, allX.ToArray(), allY.ToArray(), lo, hi, lo, hi, sz.Width, sz.Height);
            Mpl.Legend(g, pos, items, Mpl.FCnSmall, true);
        }

        /// <summary>R²-MSE 定位图：每个模型一个点，标注名称（越靠左上越好）</summary>
        private static void R2MseMap(Graphics g, RectangleF area, IList<ModelReport> reports)
        {
            var plot = Mpl.Inset(area, 62f, 34f, 18f, 56f);
            if (plot.Width < 40 || plot.Height < 40) return;
            Mpl.TextCenter(g, "R²-MSE 定位图（右上 = 精度高，越靠右下越好）", Mpl.FCn, Mpl.CLabel, area.Left + area.Width / 2, area.Top + 2);

            double rLo = 1, rHi = 0, mLo = double.MaxValue, mHi = 0;
            foreach (var r in reports)
            {
                double r2 = r.HasTest ? r.TestR2 : r.TrainR2;
                double m = r.HasTest ? r.TestMSE : r.TrainMSE;
                if (double.IsNaN(r2) || double.IsNaN(m)) continue;
                rLo = Math.Min(rLo, r2); rHi = Math.Max(rHi, r2);
                mLo = Math.Min(mLo, m); mHi = Math.Max(mHi, m);
            }
            if (mLo > mHi) { mLo = 0; mHi = 1; }
            Pad(ref rLo, ref rHi, 0.15); Pad(ref mLo, ref mHi, 0.2);
            if (rHi <= rLo) rHi = rLo + 0.05;

            Mpl.Axes(g, plot, rLo, rHi, mLo, mHi, "R²", "MSE", Mpl.FCn, true, true);

            int k = 0;
            foreach (var r in reports)
            {
                double r2 = r.HasTest ? r.TestR2 : r.TrainR2;
                double m = r.HasTest ? r.TestMSE : r.TrainMSE;
                if (double.IsNaN(r2) || double.IsNaN(m)) { k++; continue; }
                var c = Mpl.Palette[k % Mpl.Palette.Length];
                float px = Mpl.MapX(r2, plot, rLo, rHi), py = Mpl.MapY(m, plot, mLo, mHi);
                using (var b = new SolidBrush(c))
                    g.FillEllipse(b, px - 6f, py - 6f, 12f, 12f);
                Mpl.Text(g, r.DisplayName, Mpl.FCnSmall, Color.FromArgb(60, 60, 60), px + 9f, py - 8f);
                k++;
            }
        }

        // ==================== 小工具 ====================

        public static double[] Column(double[][] mat, int col)
        {
            if (mat == null) return new double[0];
            var r = new double[mat.Length];
            for (int i = 0; i < mat.Length; i++)
                r[i] = (mat[i] != null && mat[i].Length > col) ? mat[i][col] : double.NaN;
            return r;
        }

        private static double Min(double[] a)
        {
            double m = double.MaxValue;
            if (a != null) foreach (var v in a) if (!double.IsNaN(v) && v < m) m = v;
            return m == double.MaxValue ? 0 : m;
        }

        private static double Max(double[] a)
        {
            double m = double.MinValue;
            if (a != null) foreach (var v in a) if (!double.IsNaN(v) && v > m) m = v;
            return m == double.MinValue ? 1 : m;
        }

        private static double Max2(double[][] v)
        {
            double m = 0;
            foreach (var row in v) foreach (var x in row) if (!double.IsNaN(x) && x > m) m = x;
            return m <= 0 ? 1 : m;
        }

        private static void Pad(ref double lo, ref double hi, double frac)
        {
            if (double.IsNaN(lo) || double.IsNaN(hi) || hi < lo) { lo = 0; hi = 1; return; }
            double span = hi - lo;
            if (span < 1e-9) span = Math.Abs(hi) > 0 ? Math.Abs(hi) : 1;
            lo -= span * frac;
            hi += span * frac;
        }

        private static string F2(double v) => double.IsNaN(v) ? "—" : v.ToString("F2");
    }
}
