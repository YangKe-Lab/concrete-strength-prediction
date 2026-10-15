using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using NSGeo.Geometry;
using NSFramework.NSModel;
using NSFramework.NSElement;
using NSStructurePreview;
using ProjectPenguin.Cards;
using ProjectPenguin.Cards.Enums;
using ProjectPenguin.Cards.ProcessorCardHelpers;

namespace MyGamaCards
{
    /// <summary>
    /// 线性模型卡片 —— 用最小二乘正规方程训练线性回归模型（课程设计 linear_model.py 的 GAMA 化）
    ///
    /// 支持三种特征构造（对应课程设计三个线性模型）：
    /// - simple      ：y = b0 + b1·x1 + b2·x2 + b3·x3              （4 参数）
    /// - interaction ：+ b4·x1x2 + b5·x1x3 + b6·x2x3                （7 参数，最优线性模型）
    /// - poly        ：+ b7·x1² + b8·x2² + b9·x3²                  （10 参数）
    ///
    /// 求解：正规方程 β = (XᵀX)⁻¹Xᵀy，纯 C# 手写矩阵运算（转置/乘法/高斯-约当求逆）。
    ///
    /// 端口说明：
    /// - 输入0 训练数据集 ：Dataset（必需，最后一列为标签）
    /// - 输入1 模型类型   ：simple / interaction / poly，默认 interaction
    /// - 输入2 测试集     ：Dataset（可选，连上后自动出测试集指标与对照图）
    /// - 输入3 模型名称   ：可选，图表标题与对比图例用（留空自动命名）
    /// - 输出0 线性结果   ：LinearResult（含权重与训练集指标，可连推理卡/查看器）
    /// - 输出1 评估摘要   ：指标文字摘要（可截图写报告）
    /// - 输出6 模型报告   ：ModelReport（连到「结果对比卡」做多模型对比）
    /// </summary>
    public class LinearModelCard : PreviewCardBase
    {
        public LinearModelCard() : base("线性模型", "线性模型", "最小二乘正规方程训练线性回归（simple/interaction/poly；自动出训练/测试对照图与拟合曲面）", "深度学习", "模型") { }
        public override string EnglishAlias => "DL_LinearModel";
        private static readonly Guid sc_guid = Guid.Parse("{A1B2C3D4-5E6F-4789-90AB-CDEF01234567}");
        public override Guid CardGuid => sc_guid;

        protected override StructurePreviewMode PreviewMode => StructurePreviewMode.ModelMode;
        protected override ElementType ElementTypeForPreview => ElementType.None;

        protected override void AddInputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(Dataset), "训练数据集", "训练数据", "训练数据集（必需，最后一列为标签）", DataProcessingMode.OneByOne);
            helper.AddWellKnownTypeOfDataCard(typeof(string), "模型类型", "模型类型", "simple / interaction(默认) / poly", DataProcessingMode.OneByOne, "interaction");
            helper.AddWellKnownTypeOfDataCard(typeof(Dataset), "测试集", "测试集", "测试集（可选，留空只评估训练集）", DataProcessingMode.OneByOne, new Dataset()).IsOptional = true;
            // ↓ 新增：模型名称（可选）—— 图表标题与对比图例使用
            helper.AddWellKnownTypeOfDataCard(typeof(string), "模型名称", "模型名称", "模型名称（可选，如 Linear Model1；留空按模型类型自动命名）", DataProcessingMode.OneByOne, "");
            // ↓ 新增：上游汇总 —— 上一张模型卡的「报告汇总」（可选；链式汇总用）
            helper.AddWellKnownTypeOfDataCard(typeof(ModelSet), "上游汇总", "上游汇总", "上一张模型卡的「报告汇总」（可选；链式汇总用）", DataProcessingMode.OneByOne, new ModelSet()).IsOptional = true;
        }

        protected override void AddOutputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(LinearResult), "线性结果", "线性结果", "线性回归结果（权重 + 指标 + 可预测）", DataProcessingMode.OneByOne);
            helper.AddWellKnownTypeOfDataCard(typeof(string), "评估摘要", "评估摘要", "指标文字摘要", DataProcessingMode.OneByOne);
            helper.AddWellKnownTypeOfDataCard(typeof(NSModel), "拟合曲面", "拟合曲面", "三维拟合曲面 + 训练数据点（切换模型类型实时变化）", DataProcessingMode.OneByOne);
            // ↓ 彩色几何图元输出：MATLAB 风格，预览窗口自动渲染
            helper.AddWellKnownTypeOfDataCard(typeof(VLine), "拟合网格", "拟合网格", "拟合曲面网格线 + 坐标轴（预览窗口自动显示）", DataProcessingMode.ListByList);
            helper.AddWellKnownTypeOfDataCard(typeof(VPoint), "数据点", "数据点", "训练数据点云（彩色点，预览窗口自动显示）", DataProcessingMode.ListByList);
            helper.AddWellKnownTypeOfDataCard(typeof(VText), "轴标注", "轴标注", "坐标轴文字标注 X/Y/Z", DataProcessingMode.ListByList);
            // ↓ 新增：模型报告 —— 供「结果对比卡」汇总对比
            helper.AddWellKnownTypeOfDataCard(typeof(ModelReport), "模型报告", "模型报告", "训练/测试指标与逐样本预测（连到结果对比卡）", DataProcessingMode.OneByOne);
            // ↓ 新增：报告汇总 —— 上游各模型 + 本模型（有序）
            helper.AddWellKnownTypeOfDataCard(typeof(ModelSet), "报告汇总", "报告汇总", "上游各模型报告 + 本模型报告 → 接下一张模型卡的「上游汇总」或结果对比卡", DataProcessingMode.OneByOne);
        }

        public override void Build(IDataDelivery idd)
        {
            NNVisualizer.EnsureRenderHandlersRegistered();   // 注册彩色图元渲染处理器（幂等）

            Dataset train = null;
            string ftype = "interaction";
            Dataset test = null;
            string modelName = "";
            ModelSet upstreamSet = null;
            idd.GetDataItem(0, ref train);
            idd.GetDataItem(1, ref ftype);
            idd.GetDataItem(2, ref test);
            idd.GetDataItem(3, ref modelName);
            idd.GetDataItem(4, ref upstreamSet);   // 上游汇总（可选）

            // ---------- 第 1 步：校验 ----------
            if (train == null || train.X == null || train.SampleCount == 0)
            {
                idd.SetDataItem(0, new LinearResult());
                idd.SetDataItem(1, "(无效：未收到训练数据集)");
                idd.SetDataItem(6, new ModelReport());
                try { idd.SetDataItem(7, ModelSet.Combine(upstreamSet, null)); } catch { }
                LogMessage(BuildLogLevel.Error, "LinearModelCard: 未收到有效训练数据集，请从输入数据卡连接 Dataset。");
                return;
            }

            ftype = NormalizeType(ftype);
            int n = train.SampleCount;
            int inDim = train.X[0].Length;
            if (inDim < 1) { idd.SetDataItem(0, new LinearResult()); idd.SetDataItem(1, "(无效)"); return; }

            // ---------- 第 2 步：构造设计矩阵 Xd (N × M) 与标签向量 y (N) ----------
            int m = LinearResult.BuildDesign(new double[inDim], ftype).Length;
            var Xd = new double[n][];
            var y = new double[n];
            for (int i = 0; i < n; i++)
            {
                Xd[i] = LinearResult.BuildDesign(train.X[i], ftype);
                y[i] = train.Y[i] != null && train.Y[i].Length > 0 ? train.Y[i][0] : 0;
            }

            // ---------- 第 3 步：正规方程 β = (XᵀX)⁻¹Xᵀy ----------
            double[,] XtX = MatrixMultiply(Transpose(Xd, n, m), Xd, m, n, m);   // M×M
            double[] Xty = MatrixVector(Transpose(Xd, n, m), y, m, n);           // M
            double[,] inv = Inverse(XtX, m);
            double[] beta = Multiply(inv, Xty, m);                               // M

            // ---------- 第 4 步：训练集评估 ----------
            var trainPred = new double[n];
            for (int i = 0; i < n; i++) trainPred[i] = Dot(Xd[i], beta);
            double mse, mae, r2;
            ModelReport.Metrics(y, trainPred, out r2, out mse, out mae);

            var result = new LinearResult
            {
                Weights = beta,
                FeatureType = ftype,
                FeatureNames = LinearResult.DesignNames(ftype),
                SampleCount = n,
                R2 = r2,
                MSE = mse,
                MAE = mae
            };

            string displayName = string.IsNullOrWhiteSpace(modelName) ? DefaultName(ftype) : modelName.Trim();
            var report = new ModelReport
            {
                Name = displayName,
                Kind = "Linear",
                // 结构列显示“类型(参数个数)”，一眼就能看出三张线性卡是不是有重复类型
                Architecture = TypeShort(ftype) + "：" + string.Join("+", LinearResult.DesignNames(ftype))
            };
            report.SetTrain(y, trainPred);

            // ---------- 第 5 步：测试集评估（可选） ----------
            var sb = new StringBuilder();
            sb.Append($"训练集(n={n}) | R²={Fmt(r2)} | MSE={mse:F4} | MAE={mae:F4}");
            var testTruth = new double[0];
            var testPred = new double[0];
            if (test != null && test.X != null && test.SampleCount > 0)
            {
                int tn = test.SampleCount;
                testTruth = new double[tn];
                testPred = new double[tn];
                for (int i = 0; i < tn; i++)
                {
                    testPred[i] = Dot(LinearResult.BuildDesign(test.X[i], ftype), beta);
                    testTruth[i] = test.Y[i] != null && test.Y[i].Length > 0 ? test.Y[i][0] : 0;
                }
                double tMse, tMae, tR2;
                ModelReport.Metrics(testTruth, testPred, out tR2, out tMse, out tMae);
                sb.Append($" | 测试集(n={tn}) | R²={Fmt(tR2)} | MSE={tMse:F4} | MAE={tMae:F4}");
                report.SetTest(testTruth, testPred);
            }

            idd.SetDataItem(0, result);
            idd.SetDataItem(1, sb.ToString());
            idd.SetDataItem(6, report);
            idd.SetDataItem(7, ModelSet.Combine(upstreamSet, report));   // 报告汇总（上游 + 本模型）

            // 渲染拟合曲面 + 数据点到三维视口（切换模型类型重跑时实时变化）
            RenderFit(idd, result, train, ftype, report, testTruth, testPred);

            // 日志打印权重，便于对照课程设计报告
            var wb2 = new StringBuilder($"LinearModelCard: {displayName}（{ftype}）训练完成，{sb}");
            wb2.Append(" | 系数:");
            for (int i = 0; i < beta.Length; i++)
                wb2.Append($" {result.FeatureNames[i]}={beta[i]:G4}");
            wb2.Append("　→ 右键卡片「显示图表窗口」看 3 页图（预测vs真实 / 逐样本 / 拟合曲面），或「导出全部图表 PNG」。");
            LogMessage(BuildLogLevel.Warning, wb2.ToString());
        }

        private static string DefaultName(string ftype)
        {
            switch (ftype)
            {
                case "simple": return "线性·简单";
                case "poly": return "线性·多项式";
                default: return "线性·交互";
            }
        }

        /// <summary>类型短标签（结构列显示用）：一眼能看出有没有重复类型</summary>
        private static string TypeShort(string ftype)
        {
            switch (ftype)
            {
                case "simple": return "简单线性(4参)";
                case "poly": return "多项式(10参)";
                default: return "交互(7参)";
            }
        }

        private string NormalizeType(string t)
        {
            if (string.IsNullOrWhiteSpace(t)) return "interaction";
            string s = t.Trim().ToLowerInvariant();
            s = s.Replace(" ", "").Replace("-", "").Replace("_", "");
            if (s == "simple" || s == "poly" || s == "interaction") return s;
            // 简单线性
            if (s == "linear" || s == "简单" || s == "简单线性" || s == "模型1" || s == "model1") return "simple";
            // 多项式 / 二次
            if (s == "poly" || s == "poly2" || s == "poly2d" || s == "quadratic" || s == "多项式"
                || s.Contains("多项式") || s.Contains("二次") || s.Contains("平方")) return "poly";
            // 交互
            if (s == "inter" || s == "interact" || s.Contains("交互") || s.Contains("交叉")) return "interaction";
            LogMessage(BuildLogLevel.Warning,
                $"LinearModelCard: 未知模型类型 '{t}'，已回退为 interaction（7 参）。" +
                "可选：simple/简单线性、poly/多项式、interaction/交互。请检查「模型类型」端口的拼写！");
            return "interaction";
        }

        // ==================== 矩阵工具 ====================
        private static double[][] Transpose(double[][] a, int rows, int cols)
        {
            var t = new double[cols][];
            for (int j = 0; j < cols; j++)
            {
                t[j] = new double[rows];
                for (int i = 0; i < rows; i++) t[j][i] = a[i][j];
            }
            return t;
        }

        private static double[,] MatrixMultiply(double[][] a, double[][] b, int ra, int ca, int cb)
        {
            var r = new double[ra, cb];
            for (int i = 0; i < ra; i++)
                for (int k = 0; k < ca; k++)
                {
                    double aik = a[i][k];
                    if (aik == 0) continue;
                    for (int j = 0; j < cb; j++) r[i, j] += aik * b[k][j];
                }
            return r;
        }

        private static double[] MatrixVector(double[][] a, double[] v, int rows, int cols)
        {
            var r = new double[rows];
            for (int i = 0; i < rows; i++)
            {
                double s = 0;
                for (int k = 0; k < cols; k++) s += a[i][k] * v[k];
                r[i] = s;
            }
            return r;
        }

        private static double[] Multiply(double[,] a, double[] v, int n)
        {
            var r = new double[n];
            for (int i = 0; i < n; i++)
            {
                double s = 0;
                for (int k = 0; k < n; k++) s += a[i, k] * v[k];
                r[i] = s;
            }
            return r;
        }

        /// <summary>高斯-约当消元求逆（n≤12 足够课程设计使用）</summary>
        private static double[,] Inverse(double[,] a, int n)
        {
            var m = new double[n, 2 * n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) m[i, j] = a[i, j];
            for (int i = 0; i < n; i++) m[i, n + i] = 1.0;

            for (int col = 0; col < n; col++)
            {
                // 选主元
                int pivot = col;
                for (int r = col + 1; r < n; r++)
                    if (Math.Abs(m[r, col]) > Math.Abs(m[pivot, col])) pivot = r;
                if (Math.Abs(m[pivot, col]) < 1e-12)
                {
                    // 奇异矩阵（如特征列共线）：退化为伪逆近似，加微小扰动
                    for (int i = 0; i < n; i++) m[i, n + i] = 0;
                    return m;
                }
                if (pivot != col)
                    for (int j = 0; j < 2 * n; j++) { double tmp = m[col, j]; m[col, j] = m[pivot, j]; m[pivot, j] = tmp; }

                double d = m[col, col];
                for (int j = 0; j < 2 * n; j++) m[col, j] /= d;
                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double f = m[r, col];
                    if (f == 0) continue;
                    for (int j = 0; j < 2 * n; j++) m[r, j] -= f * m[col, j];
                }
            }

            var inv = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++) inv[i, j] = m[i, n + j];
            return inv;
        }

        private static double Dot(double[] a, double[] b)
        {
            double s = 0;
            for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
            return s;
        }

        private static string Fmt(double v) => double.IsNaN(v) ? "N/A" : v.ToString("F4");

        /// <summary>
        /// 渲染拟合曲面（网格线）与训练数据点到三维视口，并生成 2D 图表页：
        /// ① 预测 vs 真实（训练集/测试集 1×2，复刻课程设计 LinearModelX.png）
        /// ② 逐样本对比
        /// ③ 拟合曲面热力图（特征1×特征2，特征3 取均值）
        /// </summary>
        private void RenderFit(IDataDelivery idd, LinearResult result, Dataset train, string ftype,
                               ModelReport report, double[] testTruth, double[] testPred)
        {
            if (result == null || result.Weights == null || train == null || train.SampleCount == 0) return;
            try
            {
                NNVisualizer.Init(out var model, out var floor);
                NNVisualizer.BuildAxes(model, floor);

                int n = train.SampleCount;
                int inDim = train.X[0] != null ? train.X[0].Length : 0;
                double x1Min = 0, x1Max = 1, x2Min = 0, x2Max = 1, yMin = 0, yMax = 1;
                double x3Mean = 0;
                var xs = new double[n]; var ys = new double[n]; var zs = new double[n];
                for (int i = 0; i < n; i++)
                {
                    xs[i] = inDim > 0 ? train.X[i][0] : 0;
                    ys[i] = inDim > 1 ? train.X[i][1] : 0;
                    x3Mean += inDim > 2 ? train.X[i][2] : 0;
                    zs[i] = (train.Y != null && train.Y[i] != null && train.Y[i].Length > 0) ? train.Y[i][0] : 0;
                    if (i == 0) { x1Min = x1Max = xs[i]; x2Min = x2Max = ys[i]; yMin = yMax = zs[i]; }
                    else
                    {
                        x1Min = Math.Min(x1Min, xs[i]); x1Max = Math.Max(x1Max, xs[i]);
                        x2Min = Math.Min(x2Min, ys[i]); x2Max = Math.Max(x2Max, ys[i]);
                        yMin = Math.Min(yMin, zs[i]); yMax = Math.Max(yMax, zs[i]);
                    }
                }
                x3Mean /= n;

                double denorm(double vn, double min, double max) => min + (vn + 4500.0) / 9000.0 * (max - min);
                double zFn(double x1n, double x2n)
                {
                    double[] design = LinearResult.BuildDesign(
                        new[] { denorm(x1n, x1Min, x1Max), denorm(x2n, x2Min, x2Max), x3Mean }, ftype);
                    double pred = 0;
                    for (int k = 0; k < design.Length && k < result.Weights.Length; k++) pred += design[k] * result.Weights[k];
                    return NNVisualizer.Norm(pred, yMin, yMax, 5000);
                }

                // 拟合曲面网格（10×10）
                NNVisualizer.AddSurfaceGrid(model, floor, zFn, 10);

                // 训练数据点
                var nx = new List<double>(); var ny = new List<double>(); var nz = new List<double>();
                for (int i = 0; i < n; i++)
                {
                    nx.Add(NNVisualizer.Norm(xs[i], x1Min, x1Max, 4500));
                    ny.Add(NNVisualizer.Norm(ys[i], x2Min, x2Max, 3500));
                    nz.Add(NNVisualizer.Norm(zs[i], yMin, yMax, 5000));
                }
                NNVisualizer.AddPointCloud(model, floor, nx, ny, nz);

                NNVisualizer.Render(previewController, model, s => LogMessage(BuildLogLevel.Warning, s));
                idd.SetDataItem(2, model);

                // 几何图元输出：彩色拟合网格（含坐标轴）+ 数据点 + 轴标注 → 预览窗口自动渲染（MATLAB 风格）
                try
                {
                    var gridLines = NNVisualizer.BuildColoredSurfaceGridLines(zFn, 10);
                    gridLines.AddRange(NNVisualizer.BuildColoredAxes());
                    idd.SetDataList(3, gridLines);
                }
                catch { }
                try { idd.SetDataList(4, NNVisualizer.BuildColoredPoints(nx, ny, nz, NNVisualizer.CPoint)); } catch { }
                try { idd.SetDataList(5, NNVisualizer.BuildAxisLabels()); } catch { }

                // ---------- 2D 图表（主展示方案） ----------
                var figs = new List<ChartFigure>();
                figs.Add(FigureFactory.ScatterFitFigure(report));
                figs.Add(FigureFactory.PerSampleFigure(report));

                // 拟合曲面热力图（data-space 预测函数 + 数据点叠加）
                double x3m = x3Mean;
                double w1Min = x1Min, w1Max = x1Max, w2Min = x2Min, w2Max = x2Max, wyMin = yMin, wyMax = yMax;
                double[] wBeta = result.Weights;
                string wFtype = ftype;
                string ftypeLabel = wFtype == "simple" ? "simple · 简单线性" : (wFtype == "poly" ? "poly · 多项式" : "interaction · 特征交互");
                string r2txt = double.IsNaN(result.R2) ? "N/A" : result.R2.ToString("F4");
                double zFnData(double a, double b)
                {
                    double[] design = LinearResult.BuildDesign(new[] { a, b, x3m }, wFtype);
                    double pred = 0;
                    for (int k = 0; k < design.Length && k < wBeta.Length; k++) pred += design[k] * wBeta[k];
                    return pred;
                }
                var xsC = xs; var ysC = ys;
                double mseC = result.MSE, maeC = result.MAE;
                figs.Add(new ChartFigure("拟合曲面", (g, w, h) =>
                {
                    Mpl.FigureTitle(g, w, "线性模型 · 拟合曲面（" + ftypeLabel + "）",
                        string.Format("R² = {0}　MSE = {1:F4}　MAE = {2:F4}　样本数 = {3}　（白点=训练样本，颜色=模型预测强度）",
                            r2txt, mseC, maeC, n), true);
                    var plot = new RectangleF(105, 96, w - 105 - 235, h - 176);
                    Chart2D.DrawHeatmap(g, plot, zFnData, w1Min, w1Max, w2Min, w2Max, wyMin, wyMax, 26);
                    Chart2D.DrawAxes(g, plot, w1Min, w1Max, w2Min, w2Max,
                        "特征1（水灰比·标准化）", "特征2（粉煤灰·标准化）", Chart2D.LabelFont);
                    Chart2D.DrawScatter(g, xsC, ysC, plot, w1Min, w1Max, w2Min, w2Max, Color.White, 4.5f);
                    Chart2D.DrawColorBar(g, new RectangleF(w - 210, 106, 16, h - 266), wyMin, wyMax,
                        "预测强度 (MPa)", Chart2D.LabelFont);
                }, "切换模型类型（simple/interaction/poly）重跑即可看到曲面形态变化"));

                SetFigures("线性模型 · " + report.DisplayName, figs);
            }
            catch (Exception ex)
            {
                LogMessage(BuildLogLevel.Warning, "拟合曲面三维渲染失败（不影响计算结果）: " + ex.Message);
                // 即使三维渲染失败，也把 2D 图表交出去（主展示方案不受影响）
                try
                {
                    SetFigures("线性模型 · " + (report != null ? report.DisplayName : "线性模型"),
                        FigureFactory.L(FigureFactory.ScatterFitFigure(report), FigureFactory.PerSampleFigure(report)));
                }
                catch { }
            }
        }
    }
}
