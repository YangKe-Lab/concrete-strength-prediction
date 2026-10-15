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
    /// 模型评估卡片 —— 计算回归任务的三大评估指标（课程设计必需）
    ///
    /// 指标公式（与 Python 版 ANN_model.py 完全一致）：
    /// - 决定系数 R² = 1 - RSS/TSS
    ///     RSS = Σ(y-ŷ)²  残差平方和
    ///     TSS = Σ(y-ȳ)²  总平方和（ȳ 为真实值均值）
    ///     R² 越接近 1 说明拟合越好
    /// - 均方误差 MSE = Σ(y-ŷ)²/N，越接近 0 越好
    /// - 平均绝对误差 MAE = Σ|y-ŷ|/N，量纲与原数据一致
    ///
    /// 课程设计典型用法：
    /// 把同一个 Dataset 接到两处：Dataset → 预测卡（自动取X做预测），
    /// Dataset → 本卡“真实标签”（自动取Y）；预测卡的输出接本卡“预测结果”。
    /// </summary>
    public class EvaluateCard : PreviewCardBase
    {
        public EvaluateCard() : base("模型评估", "模型评估", "计算回归指标 R²/MSE/MAE（右侧三维视口显示预测 vs 真实散点）", "深度学习", "评估") { }
        public override string EnglishAlias => "DL_Evaluate";
        private static readonly Guid sc_guid = Guid.Parse("{7C9F1A2B-3D4E-4F50-A1B2-C3D4E5F60718}");
        public override Guid CardGuid => sc_guid;

        protected override StructurePreviewMode PreviewMode => StructurePreviewMode.ModelMode;
        protected override ElementType ElementTypeForPreview => ElementType.None;

        protected override void AddInputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            // 真实标签：可直接接输入数据卡的 Dataset（内部自动取 Y）
            helper.AddWellKnownTypeOfDataCard(typeof(object), "真实标签", "真实标签", "真实值：Dataset（自动取Y）/ double[][] / double[]", DataProcessingMode.OneByOne);
            // 预测结果：接推理卡的输出
            helper.AddWellKnownTypeOfDataCard(typeof(object), "预测结果", "预测结果", "预测值：double[][]（推荐接模型推理卡输出）", DataProcessingMode.OneByOne);
            // ↓ 新增：模型名称（可选）—— 图表标题与对比图例使用
            helper.AddWellKnownTypeOfDataCard(typeof(string), "模型名称", "模型名称", "模型名称（可选，如 ANN Model1 测试集；留空自动命名）", DataProcessingMode.OneByOne, "");
            // ↓ 新增：上游汇总 —— 上一张模型卡的「报告汇总」（可选；链式汇总用）
            helper.AddWellKnownTypeOfDataCard(typeof(ModelSet), "上游汇总", "上游汇总", "上一张模型卡的「报告汇总」（可选；链式汇总用）", DataProcessingMode.OneByOne, new ModelSet()).IsOptional = true;
        }

        protected override void AddOutputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(double), "决定系数R2", "R²", "决定系数 R²，越接近 1 越好", DataProcessingMode.OneByOne);
            helper.AddWellKnownTypeOfDataCard(typeof(double), "均方误差MSE", "MSE", "均方误差 MSE，越小越好", DataProcessingMode.OneByOne);
            helper.AddWellKnownTypeOfDataCard(typeof(double), "平均绝对误差MAE", "MAE", "平均绝对误差 MAE，越小越好", DataProcessingMode.OneByOne);
            helper.AddWellKnownTypeOfDataCard(typeof(string), "评估摘要", "评估摘要", "评估结果文字摘要（可截图写进报告）", DataProcessingMode.OneByOne);
            helper.AddWellKnownTypeOfDataCard(typeof(NSModel), "散点分布", "散点分布", "预测 vs 真实三维散点 + 理想线 y=x（越贴线越准）", DataProcessingMode.OneByOne);
            // ↓ 新增：模型报告 —— 供「结果对比卡」汇总对比
            helper.AddWellKnownTypeOfDataCard(typeof(ModelReport), "模型报告", "模型报告", "本次评估的指标与逐样本预测（连到结果对比卡）", DataProcessingMode.OneByOne);
            // ↓ 新增：报告汇总 —— 上游各模型 + 本模型（有序）
            helper.AddWellKnownTypeOfDataCard(typeof(ModelSet), "报告汇总", "报告汇总", "上游各模型报告 + 本模型报告 → 接下一张模型卡的「上游汇总」或结果对比卡", DataProcessingMode.OneByOne);
            // 注：评估卡不做图元输出——散点坐标系与数据/拟合场景不同，叠加必乱；R²/MSE/MAE 用数字端口表达
        }

        public override void Build(IDataDelivery idd)
        {
            NNVisualizer.EnsureRenderHandlersRegistered();   // 注册彩色图元渲染处理器（幂等）

            object truthObj = null;
            object predObj = null;
            string modelName = "";
            ModelSet upstreamSet = null;
            idd.GetDataItem(0, ref truthObj);
            idd.GetDataItem(1, ref predObj);
            idd.GetDataItem(2, ref modelName);
            idd.GetDataItem(3, ref upstreamSet);   // 上游汇总（可选）

            // ---------- 第 1 步：把两个端口的对象统一转成 N×M 矩阵 ----------
            double[][] truth = ResolveMatrix(truthObj, out string err1, "真实标签");
            if (truth == null)
            {
                SetInvalid(idd);
                LogMessage(BuildLogLevel.Error, "EvaluateCard: " + err1);
                return;
            }
            double[][] pred = ResolveMatrix(predObj, out string err2, "预测结果");
            if (pred == null)
            {
                SetInvalid(idd);
                LogMessage(BuildLogLevel.Error, "EvaluateCard: " + err2);
                return;
            }

            // ---------- 第 2 步：校验形状一致 ----------
            int n = truth.Length; // 样本数
            if (pred.Length != n)
            {
                SetInvalid(idd);
                LogMessage(BuildLogLevel.Error,
                    $"EvaluateCard: 真实值样本数({n})与预测值样本数({pred.Length})不一致。");
                return;
            }
            int m = truth[0].Length; // 标签维数
            for (int i = 0; i < n; i++)
            {
                if (truth[i] == null || truth[i].Length != m || pred[i] == null || pred[i].Length != m)
                {
                    SetInvalid(idd);
                    LogMessage(BuildLogLevel.Error, $"EvaluateCard: 第{i + 1}条样本的数据维度不一致。");
                    return;
                }
            }

            // ---------- 第 3 步：计算指标 ----------
            double total = n * m;   // 元素总数
            double mean = 0;        // 真实值均值 ȳ
            for (int i = 0; i < n; i++)
                for (int t = 0; t < m; t++) mean += truth[i][t];
            mean /= total;

            double rss = 0;  // 残差平方和 Σ(y-ŷ)²
            double tss = 0;  // 总平方和 Σ(y-ȳ)²
            double absSum = 0; // 绝对误差和 Σ|y-ŷ|
            for (int i = 0; i < n; i++)
            {
                for (int t = 0; t < m; t++)
                {
                    double d = truth[i][t] - pred[i][t];
                    rss += d * d;
                    absSum += Math.Abs(d);
                    double dm = truth[i][t] - mean;
                    tss += dm * dm;
                }
            }

            double mse = rss / total;
            double mae = absSum / total;
            // TSS≈0 时（标签几乎恒定）R² 无定义，返回 NaN 并在摘要中注明
            double r2 = Math.Abs(tss) < 1e-12 ? double.NaN : 1 - rss / tss;

            // ---------- 第 4 步：输出到各端口 ----------
            idd.SetDataItem(0, r2);
            idd.SetDataItem(1, mse);
            idd.SetDataItem(2, mae);
            idd.SetDataItem(3, $"样本数={n} | R²={Fmt(r2)} | MSE={mse:F4} | MAE={mae:F4}");

            // ---------- 第 5 步：模型报告（供结果对比卡汇总） ----------
            var truths = new double[n];
            var preds = new double[n];
            double tMin = double.MaxValue, tMax = double.MinValue;
            for (int i = 0; i < n; i++)
            {
                truths[i] = truth[i][0];
                preds[i] = pred[i][0];
                tMin = Math.Min(tMin, Math.Min(truths[i], preds[i]));
                tMax = Math.Max(tMax, Math.Max(truths[i], preds[i]));
            }
            if (tMax - tMin < 1e-12) tMax = tMin + 1;

            var report = new ModelReport
            {
                Name = string.IsNullOrWhiteSpace(modelName) ? "评估模型" : modelName.Trim(),
                Kind = "评估",
                Architecture = "已训练模型（外部预测值）",
                SetLabel = "评估集"
            };
            report.SetTrain(truths, preds);
            idd.SetDataItem(5, report);
            idd.SetDataItem(6, ModelSet.Combine(upstreamSet, report));   // 报告汇总（上游 + 本模型）

            // 渲染预测 vs 真实散点 + 理想线（换数据集/模型重跑时实时更新）
            RenderScatter(idd, truth, pred);

            // ---------- 第 6 步：2D 图表（matplotlib 复刻风格，可导出 PNG） ----------
            var figs = new List<ChartFigure>();
            figs.Add(FigureFactory.ScatterFitFigure(report));
            figs.Add(FigureFactory.PerSampleFigure(report));
            SetFigures("模型评估 · " + report.DisplayName, figs);

            LogMessage(BuildLogLevel.Warning,
                $"EvaluateCard: 评估完成。{report.DisplayName}，样本数={n}，R²={Fmt(r2)}，MSE={mse:F4}，MAE={mae:F4}" +
                "　→ 右键卡片「显示图表窗口」看预测vs真实/逐样本对比图，或「导出全部图表 PNG」。");
        }

        /// <summary>校验失败时把所有输出口置为无效值</summary>
        private static void SetInvalid(IDataDelivery idd)
        {
            idd.SetDataItem(0, double.NaN);
            idd.SetDataItem(1, double.NaN);
            idd.SetDataItem(2, double.NaN);
            idd.SetDataItem(3, "(无效)");
            try { idd.SetDataItem(5, new ModelReport()); } catch { }
        }

        /// <summary>数字格式化：NaN 显示为 N/A</summary>
        private static string Fmt(double v) => double.IsNaN(v) ? "N/A" : v.ToString("F4");

        /// <summary>
        /// 把端口对象统一转换成 double[][]（N×M）矩阵：
        /// - Dataset → 取其标签矩阵 Y
        /// - double[][] → 直接使用
        /// - double[] → 视为 N 个标量，转成 N×1 单列矩阵
        /// 其他类型报错。
        /// </summary>
        private static double[][] ResolveMatrix(object o, out string err, string portName)
        {
            err = null;
            if (o == null) { err = $"{portName}端口没有收到数据。"; return null; }

            if (o is Dataset ds)
            {
                if (ds.Y == null || ds.Y.Length == 0 || ds.Y[0] == null)
                { err = $"{portName}端口收到的 Dataset 中没有标签数据。"; return null; }
                return ds.Y;
            }
            if (o is double[][] mat)
            {
                if (mat.Length == 0 || mat[0] == null)
                { err = $"{portName}端口收到的数组为空。"; return null; }
                return mat;
            }
            if (o is double[] vec)
            {
                var m = new double[vec.Length][];
                for (int i = 0; i < vec.Length; i++) m[i] = new double[] { vec[i] };
                return m;
            }

            err = $"{portName}端口的数据类型不支持({o.GetType().Name})，需要 Dataset / double[][] / double[]。";
            return null;
        }

        /// <summary>
        /// 渲染预测 vs 真实三维散点 + 理想线 y=x：
        /// X=真实值，Y=预测值（归一化），柱高一致表示"点"；红色斜杆为理想线，点越贴线越准。
        /// </summary>
        private void RenderScatter(IDataDelivery idd, double[][] truth, double[][] pred)
        {
            if (truth == null || pred == null || truth.Length == 0) return;
            try
            {
                int n = truth.Length;
                double tMin = double.MaxValue, tMax = double.MinValue;
                for (int i = 0; i < n; i++)
                {
                    double t = truth[i][0], p = pred[i][0];
                    tMin = Math.Min(tMin, Math.Min(t, p)); tMax = Math.Max(tMax, Math.Max(t, p));
                }
                if (tMax - tMin < 1e-12) tMax = tMin + 1;

                NNVisualizer.Init(out var model, out var floor);
                NNVisualizer.BuildAxes(model, floor);

                var xs = new List<double>(); var ys = new List<double>(); var zs = new List<double>();
                for (int i = 0; i < n; i++)
                {
                    xs.Add(NNVisualizer.Norm(truth[i][0], tMin, tMax, 5000));
                    ys.Add(NNVisualizer.Norm(pred[i][0], tMin, tMax, 5000));
                    zs.Add(400.0); // 统一柱高 = 平面上的"点"
                }
                NNVisualizer.AddPointCloud(model, floor, xs, ys, zs);

                // 理想线 y=x（对角斜杆）
                double dMin = NNVisualizer.Norm(tMin, tMin, tMax, 5000);
                double dMax = NNVisualizer.Norm(tMax, tMin, tMax, 5000);
                NNVisualizer.AddDiagonal(model, floor, dMin, dMax, 400);

                NNVisualizer.Render(previewController, model, s => LogMessage(BuildLogLevel.Warning, s));
                idd.SetDataItem(4, model);
            }
            catch (Exception ex)
            {
                LogMessage(BuildLogLevel.Warning, "散点三维渲染失败（不影响评估结果）: " + ex.Message);
            }
        }
    }
}
