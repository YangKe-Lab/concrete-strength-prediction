using System;
using System.Collections.Generic;
using System.Text;

namespace MyGamaCards
{
    /// <summary>
    /// 模型报告 —— 「结果对比卡」的输入契约。
    ///
    /// 每张模型卡（训练模型卡 / 线性模型卡 / 模型评估卡）在算完后都输出一份 ModelReport：
    /// 模型名称、结构、训练集与测试集的 R²/MSE/MAE，以及逐样本的“真实值 / 预测值”数组。
    /// 把它们一起连到「结果对比卡」的多连线端口，即可一键生成对比图（柱状图 / 散点对照 /
    /// 逐样本折线 / 损失曲线叠加 / 指标明细表）。
    /// </summary>
    [Serializable]
    public class ModelReport
    {
        /// <summary>展示名称，如 "ANN Model1" / "Linear Interaction"</summary>
        public string Name { get; set; } = "模型";

        /// <summary>模型类别：ANN / Linear / 其它</summary>
        public string Kind { get; set; } = "";

        /// <summary>结构或特征说明，如 "3-10-1"、"x1+x2+x3+x1x2+x1x3+x2x3"</summary>
        public string Architecture { get; set; } = "";

        /// <summary>
        /// 主指标对应的数据集名称（训练集 / 测试集 / 评估集）。
        /// 由各卡自己声明：训练模型卡与线性模型卡填“训练集”，模型评估卡填“评估集”。
        /// </summary>
        public string SetLabel { get; set; } = "训练集";

        public int Epochs { get; set; }
        public double LearningRate { get; set; } = double.NaN;
        public int Seed { get; set; } = int.MinValue;

        // ---------- 训练集 ----------
        public int TrainCount { get; set; }
        public double TrainR2 { get; set; } = double.NaN;
        public double TrainMSE { get; set; } = double.NaN;
        public double TrainMAE { get; set; } = double.NaN;
        public double[] TrainTruth { get; set; }
        public double[] TrainPred { get; set; }

        // ---------- 测试集（可选） ----------
        public bool HasTest { get; set; }
        public int TestCount { get; set; }
        public double TestR2 { get; set; } = double.NaN;
        public double TestMSE { get; set; } = double.NaN;
        public double TestMAE { get; set; } = double.NaN;
        public double[] TestTruth { get; set; }
        public double[] TestPred { get; set; }

        // ---------- 训练过程（ANN 才有） ----------
        public List<double> LossHistory { get; set; } = new List<double>();

        /// <summary>用于图表横轴标签的显示名（空名兜底）</summary>
        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrWhiteSpace(Name)) return Name;
                if (!string.IsNullOrWhiteSpace(Kind) && !string.IsNullOrWhiteSpace(Architecture))
                    return Kind + " " + Architecture;
                return string.IsNullOrWhiteSpace(Kind) ? "模型" : Kind;
            }
        }

        /// <summary>一句话摘要（写日志/报告用）</summary>
        public string SummaryText()
        {
            var sb = new StringBuilder();
            sb.Append(DisplayName);
            if (!string.IsNullOrWhiteSpace(Architecture)) sb.Append(" [").Append(Architecture).Append("]");
            sb.AppendFormat("　训练集(n={0}) R²={1} MSE={2} MAE={3}", TrainCount, F(TrainR2), F(TrainMSE), F(TrainMAE));
            if (HasTest)
                sb.AppendFormat("　测试集(n={0}) R²={1} MSE={2} MAE={3}", TestCount, F(TestR2), F(TestMSE), F(TestMAE));
            return sb.ToString();
        }

        private static string F(double v) => double.IsNaN(v) ? "N/A" : v.ToString("F4");

        // ==================== 指标计算（与课程设计 Python 版公式一致） ====================

        /// <summary>
        /// 回归指标：R² = 1 - RSS/TSS，MSE = RSS/N，MAE = Σ|y-ŷ|/N。
        /// TSS≈0（标签恒定）时 R² 无定义，返回 NaN。
        /// </summary>
        public static void Metrics(double[] truth, double[] pred, out double r2, out double mse, out double mae)
        {
            r2 = double.NaN; mse = double.NaN; mae = double.NaN;
            if (truth == null || pred == null) return;
            int n = Math.Min(truth.Length, pred.Length);
            if (n == 0) return;

            double mean = 0;
            for (int i = 0; i < n; i++) mean += truth[i];
            mean /= n;

            double rss = 0, tss = 0, abs = 0;
            for (int i = 0; i < n; i++)
            {
                double d = truth[i] - pred[i];
                rss += d * d;
                abs += Math.Abs(d);
                double dm = truth[i] - mean;
                tss += dm * dm;
            }
            mse = rss / n;
            mae = abs / n;
            r2 = Math.Abs(tss) < 1e-12 ? double.NaN : 1 - rss / tss;
        }

        /// <summary>把 N×1 的标签矩阵压成 double[]（供图表使用）</summary>
        public static double[] Column(double[][] mat, int col = 0)
        {
            if (mat == null) return new double[0];
            var r = new double[mat.Length];
            for (int i = 0; i < mat.Length; i++)
                r[i] = (mat[i] != null && mat[i].Length > col) ? mat[i][col] : double.NaN;
            return r;
        }

        /// <summary>填充训练集评估字段</summary>
        public void SetTrain(double[] truth, double[] pred)
        {
            TrainTruth = truth; TrainPred = pred; TrainCount = truth == null ? 0 : truth.Length;
            Metrics(truth, pred, out double r2, out double mse, out double mae);
            TrainR2 = r2; TrainMSE = mse; TrainMAE = mae;
        }

        /// <summary>填充测试集评估字段</summary>
        public void SetTest(double[] truth, double[] pred)
        {
            TestTruth = truth; TestPred = pred; TestCount = truth == null ? 0 : truth.Length;
            HasTest = TestCount > 0;
            Metrics(truth, pred, out double r2, out double mse, out double mae);
            TestR2 = r2; TestMSE = mse; TestMAE = mae;
        }

        /// <summary>综合排序用的分数：优先测试集 R²，无测试集时用训练集 R²</summary>
        public double SortScore()
        {
            double v = HasTest ? TestR2 : TrainR2;
            return double.IsNaN(v) ? -999 : v;
        }

        /// <summary>最后一轮训练的损失（无历史时返回 NaN）</summary>
        public double LastLoss()
        {
            if (LossHistory == null || LossHistory.Count == 0) return double.NaN;
            return LossHistory[LossHistory.Count - 1];
        }
    }

    /// <summary>
    /// 模型汇总集合 —— 「结果对比卡」的另一种输入方式（链式汇总，绕开"一个端口只能接一根线"的限制）。
    ///
    /// 用法：每张模型卡（训练模型 / 线性模型 / 模型评估）都有
    ///   入「上游汇总」(ModelSet) + 出「报告汇总」(ModelSet = 上游 + 自己)
    /// 依次串起来：
    ///   ANN①.报告汇总 → ANN②.上游汇总 → … → 线性③.报告汇总 → 结果对比.模型集合
    /// 每根线 1 对 1，顺序由链条决定。
    ///
    /// 注意：合并时**必须新建集合**，不能直接往上游的集合里 Add ——
    /// 卡片之间传的是同一个对象引用，就地修改会污染上游卡片的输出。
    /// </summary>
    [Serializable]
    public class ModelSet
    {
        /// <summary>包含的模型报告（有序）</summary>
        public List<ModelReport> Items { get; set; } = new List<ModelReport>();

        /// <summary>报告数量</summary>
        public int Count { get { return Items == null ? 0 : Items.Count; } }

        /// <summary>把「上游汇总」与「本卡报告」合并成一个**新**集合（按引用去重，避免重复计入）</summary>
        public static ModelSet Combine(ModelSet upstream, ModelReport own)
        {
            var set = new ModelSet();
            if (upstream != null && upstream.Items != null)
                foreach (var r in upstream.Items)
                    if (r != null && !set.Items.Contains(r)) set.Items.Add(r);
            if (own != null && own.TrainCount > 0 && !set.Items.Contains(own)) set.Items.Add(own);
            return set;
        }

        /// <summary>各模型名称摘要（日志用）</summary>
        public string NamesText()
        {
            var sb = new StringBuilder();
            if (Items != null)
                for (int i = 0; i < Items.Count; i++)
                {
                    if (i > 0) sb.Append("、");
                    sb.Append(Items[i] == null ? "(空)" : Items[i].DisplayName);
                }
            return sb.Length == 0 ? "(空)" : sb.ToString();
        }
    }
}
