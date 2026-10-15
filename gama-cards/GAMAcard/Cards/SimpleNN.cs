using System;
using System.Collections.Generic;

namespace MyGamaCards
{
    /// <summary>
    /// 纯 C# 手写的多层全连接神经网络（ANN），用于在 E-GAMA 画布中完成
    /// “基于 ANN 的混凝土抗压强度预测”课程设计任务。
    ///
    /// 网络行为与课程设计 Python 版 ANN_model.py 逐项对齐：
    /// 1. 结构   ：任意多层全连接网络，如模型一 3-10-1、模型二 3-5-5-1；
    /// 2. 初始化 ：权重采用 He 初始化（正态分布，标准差 = sqrt(2/上一层神经元数)），
    ///            偏置全部初始化为 0；
    /// 3. 激活函数：隐藏层默认 Leaky ReLU（负半轴斜率 α=0.01，避免神经元死亡问题），
    ///            输出层默认恒等映射（linear），满足回归任务需求；
    /// 4. 损失函数：均方误差 MSE；
    /// 5. 训练方式：全批量梯度下降——每一轮遍历全部样本累加“梯度和”，
    ///            每轮只更新一次权重与偏置（对应 Python 版 forward→backward→update 的顺序，
    ///            注意梯度是求和而非求平均，学习率需与之匹配，默认 0.0001）。
    /// </summary>
    [Serializable]
    public class SimpleNeuralNetwork
    {
        /// <summary>Leaky ReLU 负半轴斜率 α，与 Python 版 relu(x, alpha=0.01) 一致</summary>
        public const double LeakyAlpha = 0.01;

        /// <summary>本项目支持的全部激活函数名（小写），供各卡片做合法性校验</summary>
        public static readonly string[] SupportedActivations =
            { "leaky_relu", "relu", "sigmoid", "tanh", "linear" };

        // 网络各层神经元个数。例如模型一为 {3,10,1}：输入层3个、隐含层10个、输出层1个
        private readonly int[] sizes;

        // 每个权重层的激活函数名（数组长度 = 权重层层数）
        private readonly string[] activations;

        // 偏置：biases[l][j] 表示第 l 个权重层中第 j 个神经元的偏置 b
        private readonly double[][] biases;

        // 权重：weights[l][j][k] 表示第 l 层第 j 个神经元与上一层第 k 个神经元的连接权重 w
        private readonly double[][][] weights;

        /// <summary>只读获取网络结构（各层神经元个数副本），供张量查看器显示</summary>
        public int[] Sizes { get { return (int[])sizes.Clone(); } }

        /// <summary>
        /// 构造神经网络并完成参数初始化。
        /// </summary>
        /// <param name="sizes">各层神经元数，如 new[]{3,10,1}</param>
        /// <param name="activations">每个权重层的激活函数；传 null 时自动取“隐藏层 leaky_relu + 输出层 linear”</param>
        /// <param name="seed">随机种子；固定种子可让每次训练结果完全可复现</param>
        public SimpleNeuralNetwork(int[] sizes, string[] activations, int seed)
        {
            this.sizes = sizes;
            int L = sizes.Length - 1; // 权重层层数（输入层不涉及参数）

            // 激活函数缺省规则：非末层 leaky_relu，末层 linear（恒等输出，适配回归）
            if (activations == null || activations.Length != L)
                activations = DefaultActivations(sizes.Length);
            this.activations = activations;

            var rnd = new Random(seed);

            biases = new double[L][];
            weights = new double[L][][];
            for (int l = 0; l < L; l++)
            {
                biases[l] = new double[sizes[l + 1]];
                weights[l] = new double[sizes[l + 1]][];
                for (int j = 0; j < sizes[l + 1]; j++)
                {
                    // 偏置初始化为 0（对齐 Python 版 np.zeros）
                    biases[l][j] = 0.0;

                    weights[l][j] = new double[sizes[l]];
                    // He 初始化标准差 = sqrt(2/上一层神经元数)，对齐 Python 版 he_std = np.sqrt(2/input_size)
                    double heStd = Math.Sqrt(2.0 / sizes[l]);
                    for (int k = 0; k < sizes[l]; k++)
                        weights[l][j][k] = GaussianRandom(rnd) * heStd;
                }
            }
        }

        /// <summary>生成缺省激活序列：中间层 leaky_relu，最后一层 linear</summary>
        public static string[] DefaultActivations(int totalLayerCount)
        {
            var arr = new string[totalLayerCount - 1];
            for (int i = 0; i < arr.Length; i++)
                arr[i] = (i == arr.Length - 1) ? "linear" : "leaky_relu";
            return arr;
        }

        /// <summary>
        /// 把用户输入的激活函数名归一化为受支持的标准名（忽略大小写、兼容连字符与 identity 别名）；
        /// 不支持的名字返回 null，由调用方决定回退策略。
        /// </summary>
        public static string NormalizeActivation(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            string s = raw.Trim().ToLowerInvariant().Replace("-", "_");
            if (s == "identity") s = "linear"; // 恒等映射别名
            foreach (var a in SupportedActivations)
                if (a == s) return s;
            return null;
        }

        /// <summary>Box-Muller 变换：把均匀分布随机数转换为标准正态分布随机数</summary>
        private static double GaussianRandom(Random rnd)
        {
            double u1 = 1.0 - rnd.NextDouble();
            double u2 = 1.0 - rnd.NextDouble();
            return Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2);
        }

        /// <summary>按激活函数名计算激活值 f(z)</summary>
        internal static double Activate(string act, double z)
        {
            switch (act)
            {
                case "leaky_relu": return z > 0 ? z : LeakyAlpha * z;      // Leaky ReLU：负半轴乘 α
                case "relu": return z > 0 ? z : 0.0;                       // 标准 ReLU
                case "sigmoid": return 1.0 / (1.0 + Math.Exp(-z));         // Sigmoid
                case "tanh": return Math.Tanh(z);                          // 双曲正切
                default: return z;                                          // linear 恒等映射
            }
        }

        /// <summary>按激活函数名计算导数 f'(z)。sigmoid/tanh 直接由激活值 a 反推更快。</summary>
        internal static double ActivateDerivative(string act, double z, double a)
        {
            switch (act)
            {
                case "leaky_relu": return z > 0 ? 1.0 : LeakyAlpha;        // x>0 时梯度为 1，否则为 α
                case "relu": return z > 0 ? 1.0 : 0.0;
                case "sigmoid": return a * (1.0 - a);                      // sigmoid'(z) = a(1-a)
                case "tanh": return 1.0 - a * a;                           // tanh'(z) = 1-a²
                default: return 1.0;                                        // linear 导数为 1
            }
        }

        /// <summary>
        /// 单样本预测：从输入层到输出层完整走一遍前向传播，返回输出层激活值。
        /// </summary>
        public double[] Predict(double[] input)
        {
            double[] a = (double[])input.Clone(); // 拷贝一份作为第 0 层的输出，避免修改调用方数组
            int L = weights.Length;
            for (int l = 0; l < L; l++)
            {
                var next = new double[sizes[l + 1]];
                for (int j = 0; j < sizes[l + 1]; j++)
                {
                    // 线性部分：z = W·x + b
                    double z = biases[l][j];
                    for (int k = 0; k < sizes[l]; k++) z += weights[l][j][k] * a[k];
                    // 非线性部分：a = f(z)
                    next[j] = Activate(activations[l], z);
                }
                a = next; // 本层输出作为下一层输入
            }
            return a;
        }

        /// <summary>批量预测：对每个样本逐一执行单样本前向传播。</summary>
        public double[][] PredictBatch(double[][] X)
        {
            var result = new double[X.Length][];
            for (int i = 0; i < X.Length; i++) result[i] = Predict(X[i]);
            return result;
        }

        /// <summary>
        /// 训练网络（全批量梯度下降），返回每一轮的平均 MSE 损失历史。
        /// 与 Python 版一致：先对全部样本做前向传播并累加梯度，之后统一更新一次参数。
        /// 注意：梯度是各样本之和（对应 numpy 的矩阵乘法口径），不是平均值。
        /// </summary>
        /// <param name="X">特征矩阵（N×输入维）</param>
        /// <param name="Y">标签矩阵（N×输出维）</param>
        /// <param name="epochs">训练轮数</param>
        /// <param name="learningRate">学习率</param>
        public List<double> Train(double[][] X, double[][] Y, int epochs, double learningRate)
        {
            int n = X.Length;       // 样本总数
            int L = weights.Length; // 权重层层数
            int outDim = sizes[L];  // 输出维数

            // 梯度缓冲区：gradW/gradB 与权重/偏置形状完全相同，用于累加全批量的梯度和
            var gradW = new double[L][][];
            var gradB = new double[L][];
            for (int l = 0; l < L; l++)
            {
                gradW[l] = new double[sizes[l + 1]][];
                for (int j = 0; j < sizes[l + 1]; j++) gradW[l][j] = new double[sizes[l]];
                gradB[l] = new double[sizes[l + 1]];
            }

            var history = new List<double>(epochs);

            // ================= 外层循环：epoch =================
            for (int epoch = 0; epoch < epochs; epoch++)
            {
                // ---- 第 1 步：清零本轮的梯度累积器 ----
                for (int l = 0; l < L; l++)
                {
                    for (int j = 0; j < sizes[l + 1]; j++) Array.Clear(gradW[l][j], 0, sizes[l]);
                    Array.Clear(gradB[l], 0, sizes[l + 1]);
                }

                double mseSum = 0.0; // 本轮所有样本误差平方之和（最后换算成平均 MSE）

                // ---- 第 2 步：遍历全部样本，前向传播 + 反向传播累加梯度 ----
                for (int i = 0; i < n; i++)
                {
                    double[] x = X[i];
                    double[] y = Y[i];

                    // ======== 前向传播 ========
                    // zs[l]     ：第 l 权重层的线性输出 z（反向传播求导要用）
                    // actsOut[m]：第 m 层的激活输出（actsOut[0] 即输入 x）
                    var zs = new double[L][];
                    var actsOut = new double[L + 1][];
                    actsOut[0] = x;
                    for (int l = 0; l < L; l++)
                    {
                        var z = new double[sizes[l + 1]];
                        var a = new double[sizes[l + 1]];
                        for (int j = 0; j < sizes[l + 1]; j++)
                        {
                            double zi = biases[l][j]; // z = b + Σ w·x
                            for (int k = 0; k < sizes[l]; k++) zi += weights[l][j][k] * actsOut[l][k];
                            z[j] = zi;
                            a[j] = Activate(activations[l], zi); // a = f(z)
                        }
                        zs[l] = z;
                        actsOut[l + 1] = a;
                    }

                    double[] output = actsOut[L];

                    // 累加本样本误差平方（MSE 在循环外统一除以样本总元素数）
                    for (int t = 0; t < outDim; t++)
                    {
                        double diff = output[t] - y[t];
                        mseSum += diff * diff;
                    }

                    // ======== 反向传播 ========
                    // 输出层误差项 δ = (a - y) ⊙ f'(z)
                    // （MSE 对输出的偏导是 (a-y)，再逐元素乘上输出层激活函数的导数）
                    var delta = new double[outDim];
                    for (int j = 0; j < outDim; j++)
                        delta[j] = (output[j] - y[j]) *
                                   ActivateDerivative(activations[L - 1], zs[L - 1][j], output[j]);

                    // 从最后一个权重层向前逐层回传误差并累加梯度
                    for (int l = L - 1; l >= 0; l--)
                    {
                        // 本层梯度：∂L/∂b = δ（直接累加）；∂L/∂w = δ × 上一层输出（外积累加）
                        for (int j = 0; j < sizes[l + 1]; j++)
                        {
                            gradB[l][j] += delta[j];
                            for (int k = 0; k < sizes[l]; k++)
                                gradW[l][j][k] += delta[j] * actsOut[l][k];
                        }

                        if (l > 0)
                        {
                            // 把误差继续向前传一层：δ_prev = (Wᵀ·δ) ⊙ f'(z_prev)
                            // （链式法则：上一层的误差等于后一层权重的转置加权再乘其激活导数）
                            var prev = new double[sizes[l]];
                            for (int j = 0; j < sizes[l]; j++)
                            {
                                double sum = 0.0;
                                for (int jj = 0; jj < sizes[l + 1]; jj++)
                                    sum += weights[l][jj][j] * delta[jj];
                                prev[j] = sum * ActivateDerivative(activations[l - 1], zs[l - 1][j], actsOut[l][j]);
                            }
                            delta = prev;
                        }
                    }
                }

                // ---- 第 3 步：参数更新（全批量模式每轮只更新一次，与 Python 版 update() 一致）----
                for (int l = 0; l < L; l++)
                {
                    for (int j = 0; j < sizes[l + 1]; j++)
                    {
                        biases[l][j] -= learningRate * gradB[l][j];   // b ← b - lr·∂L/∂b
                        for (int k = 0; k < sizes[l]; k++)
                            weights[l][j][k] -= learningRate * gradW[l][j][k]; // w ← w - lr·∂L/∂w
                    }
                }

                // ---- 第 4 步：记录本轮平均 MSE（口径与 np.mean((pred-y)**2) 一致）----
                history.Add(mseSum / (n * outDim));
            }

            return history;
        }
    }
}
