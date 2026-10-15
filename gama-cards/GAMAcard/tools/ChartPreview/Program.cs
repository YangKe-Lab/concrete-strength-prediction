using System;
using System.Collections.Generic;
using System.IO;
using MyGamaCards;

namespace FigTest
{
    /// <summary>
    /// 图表自检程序：用与 GAMAcard 完全相同的 FigureFactory / ChartMpl / ChartWindow 源码，
    /// 在 GAMA 之外把课程设计的全部图渲染成 PNG，用于肉眼/视觉模型核查排版。
    /// </summary>
    internal static class Program
    {
        private static string OutDir = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "out");

        [STAThread]
        private static void Main(string[] args)
        {
            Directory.CreateDirectory(OutDir);
            string tag = args.Length > 0 ? args[0] : "v1";
            string dir = Path.Combine(OutDir, tag);
            Directory.CreateDirectory(dir);

            var reports = new List<ModelReport>();

            // ---------- ANN Model1: 3-10-1 ----------
            var net1 = new SimpleNeuralNetwork(new[] { 3, 10, 1 }, new[] { "leaky_relu", "linear" }, 42);
            var loss1 = net1.Train(DemoData.TrainX, DemoData.TrainY, 10000, 0.0001);
            var r1 = Ann("ANN Model1", "3-10-1", loss1, net1);
            reports.Add(r1);

            // ---------- ANN Model2: 3-5-5-1 ----------
            var net2 = new SimpleNeuralNetwork(new[] { 3, 5, 5, 1 }, new[] { "leaky_relu", "leaky_relu", "linear" }, 42);
            var loss2 = net2.Train(DemoData.TrainX, DemoData.TrainY, 10000, 0.0001);
            var r2 = Ann("ANN Model2", "3-5-5-1", loss2, net2);
            reports.Add(r2);

            // ---------- 三个线性模型 ----------
            reports.Add(Linear("线性·简单", "simple"));
            reports.Add(Linear("线性·交互", "interaction"));
            reports.Add(Linear("线性·多项式", "poly"));

            foreach (var r in reports)
                Console.WriteLine(r.SummaryText());

            // ---------- 输入数据卡图表 ----------
            Render(FigureFactory.DatasetFigures("train.xlsx", new[] { "水灰比", "粉煤灰", "硅灰" },
                DemoData.TrainX, ModelReport.Column(DemoData.TrainY)), dir, "01_input", 1);

            // ---------- 训练模型卡图表 ----------
            Render(FigureFactory.L(
                FigureFactory.LossFigure(r1.DisplayName, loss1, "结构 3-10-1　epochs=10000　lr=0.0001　seed=42　最终 MSE = " + r1.LastLoss().ToString("F6")),
                FigureFactory.ScatterFitFigure(r1),
                FigureFactory.PerSampleFigure(r1),
                NetworkFigure(r1.DisplayName, new[] { 3, 10, 1 })), dir, "02_train_ann1", 1);

            Render(FigureFactory.L(
                FigureFactory.LossFigure(r2.DisplayName, loss2, "结构 3-5-5-1　epochs=10000　lr=0.0001"),
                FigureFactory.ScatterFitFigure(r2),
                NetworkFigure(r2.DisplayName, new[] { 3, 5, 5, 1 })), dir, "03_train_ann2", 1);

            // ---------- 结果对比卡图表 ----------
            var compare = FigureFactory.CompareFigures(reports);
            Render(compare, dir, "04_compare", 1);

            // 2 倍导出：验证高清渲染路径（导出时用的就是这条）
            RenderSingle(compare[0], Path.Combine(dir, "04_compare_1_2x.png"), 1600, 950, 2);
            RenderSingle(FigureFactory.ScatterFitFigure(r1), Path.Combine(dir, "02_ann1_scatter_2x.png"), 1600, 950, 2);

            Console.WriteLine("图表已输出到 " + dir);
        }

        /// <summary>复刻训练卡里的「网络结构」页（用的是卡片同一份 Chart2D.DrawNetwork）</summary>
        private static ChartFigure NetworkFigure(string name, int[] sizes)
        {
            string arch = string.Join("-", sizes);
            int[] copy = sizes;
            return new ChartFigure("网络结构", (g, w, h) =>
            {
                Mpl.FigureTitle(g, w, "神经网络结构", name + "　结构 " + arch + "　（输入层 → 隐藏层 → 输出层）", true);
                var area = new System.Drawing.RectangleF(40, 80, w - 80, h - 120);
                Chart2D.DrawNetwork(g, area, copy, Chart2D.LabelFont);
            }, "网络拓扑：层=一列神经元，层间=全连接");
        }

        private static ModelReport Ann(string name, string arch, List<double> loss, SimpleNeuralNetwork net)
        {
            var r = new ModelReport
            {
                Name = name,
                Kind = "ANN",
                Architecture = arch,
                Epochs = 10000,
                LearningRate = 0.0001,
                Seed = 42,
                LossHistory = loss
            };
            r.SetTrain(ModelReport.Column(DemoData.TrainY), ModelReport.Column(net.PredictBatch(DemoData.TrainX)));
            r.SetTest(ModelReport.Column(DemoData.TestY), ModelReport.Column(net.PredictBatch(DemoData.TestX)));
            return r;
        }

        private static ModelReport Linear(string name, string ftype)
        {
            int n = DemoData.TrainX.Length;
            int m = BuildDesign(DemoData.TrainX[0], ftype).Length;
            var Xd = new double[n][];
            var y = new double[n];
            for (int i = 0; i < n; i++)
            {
                Xd[i] = BuildDesign(DemoData.TrainX[i], ftype);
                y[i] = DemoData.TrainY[i][0];
            }
            var beta = Solve(Xd, y, m, n);

            var r = new ModelReport { Name = name, Kind = "Linear", Architecture = string.Join("+", DesignNames(ftype)) };
            var pred = new double[n];
            for (int i = 0; i < n; i++) { double s = 0; for (int k = 0; k < m; k++) s += Xd[i][k] * beta[k]; pred[i] = s; }
            r.SetTrain(y, pred);

            int tn = DemoData.TestX.Length;
            var tpred = new double[tn];
            var ty = new double[tn];
            for (int i = 0; i < tn; i++)
            {
                var d = BuildDesign(DemoData.TestX[i], ftype);
                double s = 0; for (int k = 0; k < m; k++) s += d[k] * beta[k];
                tpred[i] = s; ty[i] = DemoData.TestY[i][0];
            }
            r.SetTest(ty, tpred);
            return r;
        }

        private static double[] BuildDesign(double[] x, string ftype)
        {
            double x1 = x[0], x2 = x[1], x3 = x[2];
            if (ftype == "simple") return new[] { x1, x2, x3, 1.0 };
            if (ftype == "poly") return new[] { x1, x2, x3, x1 * x2, x1 * x3, x2 * x3, x1 * x1, x2 * x2, x3 * x3, 1.0 };
            return new[] { x1, x2, x3, x1 * x2, x1 * x3, x2 * x3, 1.0 };
        }

        private static string[] DesignNames(string ftype)
        {
            if (ftype == "simple") return new[] { "x1", "x2", "x3", "b0" };
            if (ftype == "poly") return new[] { "x1", "x2", "x3", "x1x2", "x1x3", "x2x3", "x1²", "x2²", "x3²", "b0" };
            return new[] { "x1", "x2", "x3", "x1x2", "x1x3", "x2x3", "b0" };
        }

        private static double[] Solve(double[][] X, double[] y, int m, int n)
        {
            var A = new double[m, m];
            var b = new double[m];
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < m; j++)
                {
                    double s = 0; for (int k = 0; k < n; k++) s += X[k][i] * X[k][j];
                    A[i, j] = s;
                }
                double t = 0; for (int k = 0; k < n; k++) t += X[k][i] * y[k];
                b[i] = t;
            }
            var inv = Inverse(A, m);
            var beta = new double[m];
            for (int i = 0; i < m; i++) { double s = 0; for (int j = 0; j < m; j++) s += inv[i, j] * b[j]; beta[i] = s; }
            return beta;
        }

        private static double[,] Inverse(double[,] a, int n)
        {
            var m = new double[n, 2 * n];
            for (int i = 0; i < n; i++) { for (int j = 0; j < n; j++) m[i, j] = a[i, j]; m[i, n + i] = 1; }
            for (int col = 0; col < n; col++)
            {
                int piv = col;
                for (int r = col + 1; r < n; r++) if (Math.Abs(m[r, col]) > Math.Abs(m[piv, col])) piv = r;
                if (piv != col) for (int j = 0; j < 2 * n; j++) { double t = m[col, j]; m[col, j] = m[piv, j]; m[piv, j] = t; }
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
            for (int i = 0; i < n; i++) for (int j = 0; j < n; j++) inv[i, j] = m[i, n + j];
            return inv;
        }

        private static void Render(IList<ChartFigure> figs, string dir, string prefix, int scale)
        {
            for (int i = 0; i < figs.Count; i++)
                RenderSingle(figs[i], Path.Combine(dir, string.Format("{0}_{1}.png", prefix, i + 1)), 1600, 950, scale);
        }

        private static void RenderSingle(ChartFigure fig, string path, int w, int h, int scale)
        {
            ChartWindow.RenderToPng(fig, path, w, h, scale);
            Console.WriteLine("  -> " + path);
        }
    }
}
