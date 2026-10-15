using System;
using System.Linq;
using System.Text;
using ProjectPenguin.Cards;
using ProjectPenguin.Cards.Enums;
using ProjectPenguin.Cards.ProcessorCardHelpers;

namespace MyGamaCards
{
    /// <summary>
    /// 张量查看器卡片 —— 万能调试工具
    ///
    /// 功能：接收任意端口对象，输出一段可读的文字摘要，便于在画布上快速确认：
    /// - Dataset   ：样本数、特征维、标签维（检查数据是否加载正确）
    /// - Model     ：层列表内容（检查结构连线顺序）
    /// - TrainingResult：网络结构、训练轮数、最终 MSE（检查是否收敛）
    /// - double[][]/double[]：矩阵尺寸与前几行数值预览（检查预测结果）
    /// </summary>
    public class TensorViewerCard : ProcessorCardBase
    {
        public TensorViewerCard() : base("张量查看器", "张量查看器", "展示任意对象的摘要信息（调试利器）", "深度学习", "可视化") { }
        public override string EnglishAlias => "DL_TensorViewer";
        private static readonly Guid sc_guid = Guid.Parse("{F6789012-3ABC-4DEF-9456-789ABCDEF012}");
        public override Guid CardGuid => sc_guid;

        protected override void AddInputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(object), "对象", "对象", "任意对象：数据集/模型/训练结果/数组等", DataProcessingMode.OneByOne);
        }

        protected override void AddOutputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(string), "摘要", "摘要", "对象摘要文本", DataProcessingMode.OneByOne);
        }

        public override void Build(IDataDelivery idd)
        {
            object obj = null;
            idd.GetDataItem(0, ref obj);

            string summary = Describe(obj);
            idd.SetDataItem(0, summary);
        }

        /// <summary>按对象类型分派生成对应的文字摘要</summary>
        private static string Describe(object obj)
        {
            if (obj == null) return "(空)";

            // 张量占位：显示形状
            if (obj is Tensor t)
                return $"Tensor: 形状={(t.Shape == null ? "未知" : string.Join("x", t.Shape))}, dtype={t.DType}";

            // 层元数据：显示类型与全部配置键值
            if (obj is Layer l)
                return $"Layer: {l.LayerType}, 配置={{{string.Join(",", l.Config.Select(kv => $"{kv.Key}={kv.Value}"))}}}";

            // 模型结构：逐行列出各层
            if (obj is Model m)
            {
                var sb = new StringBuilder($"Model '{m.Name}': 共{m.Layers?.Count ?? 0}层");
                if (m.Layers != null)
                    foreach (var layer in m.Layers)
                        sb.Append($"\n  - {layer.LayerType}: {{{string.Join(",", layer.Config.Select(kv => $"{kv.Key}={kv.Value}"))}}}");
                return sb.ToString();
            }

            // 数据集：样本数与维度（课程设计最常查看的对象）
            if (obj is Dataset ds)
            {
                int inDim = ds.SampleCount > 0 && ds.X[0] != null ? ds.X[0].Length : 0;
                int outDim = ds.SampleCount > 0 && ds.Y != null && ds.Y.Length > 0 && ds.Y[0] != null ? ds.Y[0].Length : 0;
                return $"Dataset: {ds.Name}, 样本数={ds.SampleCount}, 特征维={inDim}, 标签维={outDim}";
            }

            // 训练结果：结构 + 收敛情况（写报告时截图用）
            if (obj is TrainingResult tr)
            {
                string final = double.IsNaN(tr.FinalLoss) ? "N/A" : tr.FinalLoss.ToString("F6");
                return $"TrainingResult: 结构={tr.Architecture ?? "?"}, 轮数={tr.LossHistory?.Count ?? 0}, 最终MSE={final}";
            }

            // 线性回归结果：类型 + 权重 + 指标
            if (obj is LinearResult lr)
            {
                string r2 = double.IsNaN(lr.R2) ? "N/A" : lr.R2.ToString("F4");
                return $"LinearResult: 类型={lr.FeatureType}, 系数数={lr.Weights?.Length ?? 0}, " +
                       $"训练集(n={lr.SampleCount}) R²={r2}, MSE={lr.MSE:F4}, MAE={lr.MAE:F4}";
            }

            // 神经网络实例：只显示结构
            if (obj is SimpleNeuralNetwork snn)
                return $"NeuralNetwork: 结构={string.Join("-", snn.Sizes)}";

            // 模型报告（结果对比卡的输入）：显示指标 + 测试集泛化情况
            if (obj is ModelReport rep)
            {
                var sb2 = new StringBuilder("ModelReport: " + rep.SummaryText());
                if (rep.LossHistory != null && rep.LossHistory.Count > 0)
                    sb2.AppendFormat("　最终MSE={0:F6}", rep.LastLoss());
                return sb2.ToString();
            }

            // 预测结果/一般矩阵：显示尺寸并预览前 3 行前 5 列
            if (obj is double[][] mat)
                return $"double[][{mat.Length}]: {PreviewMatrix(mat)}";

            if (obj is double[] vec)
                return $"double[{vec.Length}]: {PreviewMatrix(new[] { vec })}";

            // 其他类型兜底：调用 ToString
            return obj.ToString();
        }

        /// <summary>矩阵预览文本：最多 3 行 5 列，超出部分用省略号提示</summary>
        private static string PreviewMatrix(double[][] mat)
        {
            var sb = new StringBuilder("[");
            int showRows = Math.Min(3, mat.Length);
            for (int i = 0; i < showRows; i++)
            {
                if (i > 0) sb.Append("; ");
                var row = mat[i];
                if (row == null) { sb.Append("null"); continue; }
                int showCols = Math.Min(5, row.Length);
                for (int j = 0; j < showCols; j++)
                {
                    if (j > 0) sb.Append(',');
                    sb.Append(row[j].ToString("G4"));
                }
                if (row.Length > showCols) sb.Append(",...");
            }
            sb.Append(mat.Length > showRows ? "; ...]" : "]");
            return sb.ToString();
        }
    }
}
