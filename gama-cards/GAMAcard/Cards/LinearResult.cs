using System;

namespace MyGamaCards
{
    /// <summary>
    /// 线性回归结果：由“线性模型”卡片输出。
    /// 携带正规方程求解得到的权重、特征构造方式与训练集评估指标；
    /// 可通过 Predict() 对任意特征矩阵做预测（内部按 FeatureType 构造设计矩阵）。
    /// 与课程设计 linear_model.py 的三个模型对应：
    ///   simple      : y = b0 + b1·x1 + b2·x2 + b3·x3
    ///   interaction : + b4·x1x2 + b5·x1x3 + b6·x2x3
    ///   poly        : + b7·x1² + b8·x2² + b9·x3²
    /// </summary>
    [Serializable]
    public class LinearResult
    {
        /// <summary>回归系数（含常数项，位于数组末尾）</summary>
        public double[] Weights { get; set; }

        /// <summary>特征构造方式：simple / interaction / poly</summary>
        public string FeatureType { get; set; }

        /// <summary>设计矩阵各列的名称（展示用）</summary>
        public string[] FeatureNames { get; set; }

        /// <summary>训练样本数</summary>
        public int SampleCount { get; set; }

        // ---- 训练集评估指标 ----
        public double R2 { get; set; }
        public double MSE { get; set; }
        public double MAE { get; set; }

        /// <summary>对特征矩阵批量预测（每行一个样本）</summary>
        public double[][] Predict(double[][] X)
        {
            int n = X.Length;
            var result = new double[n][];
            for (int i = 0; i < n; i++)
            {
                double[] design = BuildDesign(X[i], FeatureType);
                result[i] = new double[] { Dot(design, Weights) };
            }
            return result;
        }

        /// <summary>单样本预测</summary>
        public double PredictOne(double[] x)
        {
            return Dot(BuildDesign(x, FeatureType), Weights);
        }

        /// <summary>构造设计向量（与训练时完全一致）</summary>
        public static double[] BuildDesign(double[] x, string featureType)
        {
            double x1 = x.Length > 0 ? x[0] : 0;
            double x2 = x.Length > 1 ? x[1] : 0;
            double x3 = x.Length > 2 ? x[2] : 0;

            switch (featureType)
            {
                case "simple":
                    return new double[] { x1, x2, x3, 1.0 };                       // 4 参数
                case "poly":
                    return new double[] {                                          // 10 参数
                        x1, x2, x3,
                        x1 * x2, x1 * x3, x2 * x3,
                        x1 * x1, x2 * x2, x3 * x3,
                        1.0 };
                default: // interaction                                          // 7 参数
                    return new double[] { x1, x2, x3, x1 * x2, x1 * x3, x2 * x3, 1.0 };
            }
        }

        /// <summary>设计矩阵列名（展示用）</summary>
        public static string[] DesignNames(string featureType)
        {
            switch (featureType)
            {
                case "simple": return new[] { "x1", "x2", "x3", "b0" };
                case "poly": return new[] { "x1", "x2", "x3", "x1x2", "x1x3", "x2x3", "x1²", "x2²", "x3²", "b0" };
                default: return new[] { "x1", "x2", "x3", "x1x2", "x1x3", "x2x3", "b0" };
            }
        }

        private static double Dot(double[] a, double[] b)
        {
            double s = 0;
            for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
            return s;
        }
    }
}
