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
    /// 训练模型卡片 —— 课程设计的核心卡片
    ///
    /// 功能：根据“模型构建卡”输出的结构创建 ANN 网络，用全批量梯度下降（MSE 损失）
    ///       在训练集上训练，输出训练结果（网络实例 + 每轮损失历史）。
    ///       训练算法与课程设计 Python 版 ANN_model.py 完全一致：
    ///       He 初始化 / Leaky ReLU 隐藏层 / 线性输出层 / 每轮全样本梯度求和后更新一次。
    ///
    /// 端口说明：
    /// - 输入0 模型   ：ModelBuilder 输出（可选；不接则自动生成默认三层结构）
    /// - 输入1 数据集 ：InputData 卡输出的训练数据（必需）
    /// - 输入2 迭代轮数：epochs，默认 10000（与 Python 版一致）
    /// - 输入3 学习率 ：learning_rate，默认 0.0001（与 Python 版一致）
    /// - 输入4 随机种子：固定种子保证每次训练结果可复现，默认 42
    /// - 输入5 测试集 ：可选；连上后自动计算测试集 R²/MSE/MAE 并画出测试集对照图
    /// - 输入6 模型名称：可选；用于图表标题与对比图例（如 “ANN Model1”）
    /// - 输出0 训练结果：TrainingResult（可接推理卡/查看器）
    /// - 输出2 模型报告：ModelReport（连到「结果对比卡」做多模型对比）
    /// </summary>
    public class TrainModelCard : PreviewCardBase
    {
        public TrainModelCard() : base("训练模型", "训练模型", "搭建并训练 ANN（全批量梯度下降 + MSE 损失；可连测试集自动出训练/测试对照图）", "深度学习", "训练") { }
        public override string EnglishAlias => "DL_TrainModel";
        private static readonly Guid sc_guid = Guid.Parse("{F4567890-1ABC-4DEF-9234-56789ABCDEF0}");
        public override Guid CardGuid => sc_guid;

        protected override StructurePreviewMode PreviewMode => StructurePreviewMode.ModelMode;
        protected override ElementType ElementTypeForPreview => ElementType.None;

        protected override void AddInputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(Model), "模型", "模型", "模型结构（ModelBuilder 输出，可选；不连=内置默认3-10-1）", DataProcessingMode.OneByOne, new Model()).IsOptional = true;
            helper.AddWellKnownTypeOfDataCard(typeof(Dataset), "数据集", "数据集", "训练数据集（必需，最后一列为抗压强度等标签）", DataProcessingMode.OneByOne);
            helper.AddWellKnownTypeOfDataCard(typeof(int), "迭代轮数", "迭代轮数", "训练轮数 epochs（默认 10000）", DataProcessingMode.OneByOne, 10000);
            helper.AddWellKnownTypeOfDataCard(typeof(double), "学习率", "学习率", "梯度下降学习率（默认 0.0001）", DataProcessingMode.OneByOne, 0.0001);
            helper.AddWellKnownTypeOfDataCard(typeof(int), "随机种子", "随机种子", "随机种子（相同种子训练结果可复现，默认 42）", DataProcessingMode.OneByOne, 42);
            // ↓ 新增：测试集（可选）—— 连上后自动出“训练集/测试集 1×2 对照图”（复刻课程设计 ANN_ModelX.png）
            helper.AddWellKnownTypeOfDataCard(typeof(Dataset), "测试集", "测试集", "测试数据集（可选，连上后自动计算测试集指标并画对照图）", DataProcessingMode.OneByOne, new Dataset()).IsOptional = true;
            // ↓ 新增：模型名称（可选）—— 图表标题与对比图例使用，如 “ANN Model1”
            helper.AddWellKnownTypeOfDataCard(typeof(string), "模型名称", "模型名称", "模型名称（可选，如 ANN Model1；留空则用结构自动命名）", DataProcessingMode.OneByOne, "");
            // ↓ 新增：上游汇总 —— 上一张模型卡的「报告汇总」（可选；把多个模型串成一条链，避免一个端口接多根线）
            helper.AddWellKnownTypeOfDataCard(typeof(ModelSet), "上游汇总", "上游汇总", "上一张模型卡的「报告汇总」（可选；链式汇总用）", DataProcessingMode.OneByOne, new ModelSet()).IsOptional = true;
        }

        protected override void AddOutputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(TrainingResult), "训练结果", "训练结果", "训练结果（网络 + 每轮 MSE 历史）", DataProcessingMode.OneByOne);
            helper.AddWellKnownTypeOfDataCard(typeof(NSModel), "网络结构", "网络结构", "神经网络拓扑三维可视化（神经元=柱，连接=杆）", DataProcessingMode.OneByOne);
            // ↓ 新增：模型报告 —— 供「结果对比卡」汇总对比
            helper.AddWellKnownTypeOfDataCard(typeof(ModelReport), "模型报告", "模型报告", "训练/测试指标与逐样本预测（连到结果对比卡）", DataProcessingMode.OneByOne);
            // ↓ 新增：报告汇总 —— 上游各模型 + 本模型（有序），接下一张模型卡或结果对比卡
            helper.AddWellKnownTypeOfDataCard(typeof(ModelSet), "报告汇总", "报告汇总", "上游各模型报告 + 本模型报告 → 接下一张模型卡的「上游汇总」或结果对比卡", DataProcessingMode.OneByOne);
        }

        public override void Build(IDataDelivery idd)
        {
            NNVisualizer.EnsureRenderHandlersRegistered();   // 注册彩色图元渲染处理器（幂等）

            Model modelObj = null;
            Dataset ds = null;
            int epochs = 10000;
            double lr = 0.0001;
            int seed = 42;
            Dataset testDs = null;
            string modelName = "";
            ModelSet upstreamSet = null;
            idd.GetDataItem(0, ref modelObj);
            idd.GetDataItem(1, ref ds);
            idd.GetDataItem(2, ref epochs);
            idd.GetDataItem(3, ref lr);
            idd.GetDataItem(4, ref seed);
            idd.GetDataItem(5, ref testDs);
            idd.GetDataItem(6, ref modelName);
            idd.GetDataItem(7, ref upstreamSet);   // 上游汇总（可选）

            // ---------- 第 1 步：校验数据集 ----------
            if (ds == null || ds.X == null || ds.Y == null || ds.SampleCount == 0)
            {
                idd.SetDataItem(0, new TrainingResult());
                idd.SetDataItem(2, new ModelReport());
                try { idd.SetDataItem(3, ModelSet.Combine(upstreamSet, null)); } catch { }
                LogMessage(BuildLogLevel.Error, "TrainModelCard: 未收到有效数据集，请从输入数据卡连接 Dataset。");
                return;
            }

            int inputDim = ds.X[0].Length;   // 特征维数（本课设为 3）
            int outputDim = ds.Y[0].Length;  // 标签维数（本课设为 1）

            // ---------- 第 2 步：解析画布上的网络结构 ----------
            int[] sizes;
            string[] acts;
            ParseArchitecture(modelObj, inputDim, outputDim, out sizes, out acts);

            // 把结构拼成文本（如 "3-10-1"），用于日志与结果展示
            var sb = new StringBuilder();
            for (int i = 0; i < sizes.Length; i++)
            {
                if (i > 0) sb.Append('-');
                sb.Append(sizes[i]);
            }
            string archText = sb.ToString();

            // ---------- 第 3 步：创建网络并训练 ----------
            var net = new SimpleNeuralNetwork(sizes, acts, seed);
            var lossHistory = net.Train(ds.X, ds.Y, Math.Max(1, epochs), lr);

            double finalLoss = lossHistory.Count > 0 ? lossHistory[lossHistory.Count - 1] : double.NaN;
            var result = new TrainingResult
            {
                TrainedModel = net,
                LossHistory = lossHistory,
                Architecture = archText,
                FinalLoss = finalLoss
            };
            idd.SetDataItem(0, result);

            // ---------- 第 4 步：训练集/测试集指标 + 模型报告 ----------
            var trainTruth = ModelReport.Column(ds.Y);
            var trainPred = ModelReport.Column(net.PredictBatch(ds.X));
            string displayName = string.IsNullOrWhiteSpace(modelName) ? ("ANN " + archText) : modelName.Trim();

            var report = new ModelReport
            {
                Name = displayName,
                Kind = "ANN",
                Architecture = archText + "（" + string.Join("/", acts) + "）",
                Epochs = epochs,
                LearningRate = lr,
                Seed = seed,
                LossHistory = lossHistory
            };
            report.SetTrain(trainTruth, trainPred);

            bool hasTest = testDs != null && testDs.X != null && testDs.SampleCount > 0;
            if (hasTest)
            {
                var testTruth = ModelReport.Column(testDs.Y);
                var testPred = ModelReport.Column(net.PredictBatch(testDs.X));
                report.SetTest(testTruth, testPred);
            }
            idd.SetDataItem(2, report);
            idd.SetDataItem(3, ModelSet.Combine(upstreamSet, report));   // 报告汇总（上游 + 本模型）

            // 渲染网络拓扑到三维视口（参数变化重跑时实时更新）
            RenderTopology(idd, sizes);

            // ---------- 第 5 步：生成图表（损失曲线 / 预测vs真实 / 逐样本 / 网络结构） ----------
            var figs = new List<ChartFigure>();
            figs.Add(FigureFactory.LossFigure(displayName, lossHistory,
                string.Format("结构 {0}　epochs={1}　lr={2}　seed={3}　最终 MSE = {4}",
                    archText, epochs, lr, seed, double.IsNaN(finalLoss) ? "N/A" : finalLoss.ToString("F6"))));
            figs.Add(FigureFactory.ScatterFitFigure(report));
            figs.Add(FigureFactory.PerSampleFigure(report));

            var sizeCopy = sizes;
            figs.Add(new ChartFigure("网络结构", (g, w, h) =>
            {
                Mpl.FigureTitle(g, w, "神经网络结构", displayName + "　结构 " + archText + "　（输入层 → 隐藏层 → 输出层）", true);
                var area = new RectangleF(40, 80, w - 80, h - 120);
                Chart2D.DrawNetwork(g, area, sizeCopy, Chart2D.LabelFont);
            }, "网络拓扑：层=一列神经元，层间=全连接"));
            SetFigures("训练模型 · " + displayName, figs);

            // ---------- 第 6 步：日志 ----------
            var msg = new StringBuilder();
            msg.AppendFormat("TrainModelCard: 训练完成。模型={0}，结构={1}（激活:{2}），epochs={3}，lr={4}，seed={5}，最终MSE={6:F6}",
                displayName, archText, string.Join("/", acts), epochs, lr, seed, finalLoss);
            msg.AppendFormat("　训练集 R²={0} MSE={1:F4} MAE={2:F4}", Mpl.F4(report.TrainR2), report.TrainMSE, report.TrainMAE);
            if (hasTest)
                msg.AppendFormat("　测试集 R²={0} MSE={1:F4} MAE={2:F4}", Mpl.F4(report.TestR2), report.TestMSE, report.TestMAE);
            else
                msg.Append("　（未连测试集：连上测试数据卡可自动生成测试集对照图）");
            msg.Append("　→ 右键卡片「显示图表窗口」看 4 页图，或「导出全部图表 PNG」。");
            LogMessage(BuildLogLevel.Warning, msg.ToString());
        }

        /// <summary>
        /// 把画布上的层元数据翻译成网络结构与激活函数序列。
        /// 解析规则：
        /// 1. 每个 Dense 层贡献一个权重层（神经元数即单元数端口值）；
        /// 2. Dense 之后紧跟的 Activation 归属于该 Dense（重复声明时只取第一个）；
        /// 3. 未声明激活的层：非末层默认 leaky_relu，末层默认 linear（与 Python 回归模型一致）；
        /// 4. 末层单元数 ≠ 标签维数时，自动追加一个 linear 输出层并警告；
        /// 5. 完全没有接 Dense 时，退化为 {输入维, max(4,2×输入维), 输出维} 的默认结构。
        /// </summary>
        private void ParseArchitecture(Model modelObj, int inputDim, int outputDim,
                                       out int[] sizes, out string[] acts)
        {
            var units = new List<int>();     // 每个 Dense 层的单元数
            var declared = new List<string>(); // 对应层的显式激活声明（null=未声明）

            if (modelObj != null && modelObj.Layers != null)
            {
                foreach (var layer in modelObj.Layers)
                {
                    if (layer == null) continue;

                    if (layer.LayerType == "Dense")
                    {
                        // 取出单元数配置，非法值兜底为 1
                        int u = 1;
                        if (layer.Config != null && layer.Config.ContainsKey("units"))
                        {
                            try { u = Convert.ToInt32(layer.Config["units"]); } catch { u = 1; }
                        }
                        if (u < 1) u = 1;
                        units.Add(u);
                        declared.Add(null); // 尚未绑定激活函数
                    }
                    else if (layer.LayerType == "Activation")
                    {
                        // 激活归属最近一个还没绑定激活的 Dense 层
                        string actName = SimpleNeuralNetwork.NormalizeActivation(
                            layer.Config != null && layer.Config.TryGetValue("activation", out object o)
                                ? Convert.ToString(o)
                                : null);

                        int idx = -1;
                        for (int i = declared.Count - 1; i >= 0; i--)
                            if (declared[i] == null) { idx = i; break; }

                        if (idx >= 0)
                        {
                            if (actName == null)
                            {
                                actName = "leaky_relu";
                                LogMessage(BuildLogLevel.Warning,
                                    $"TrainModelCard: 未知的激活类型，已按 leaky_relu 处理。" +
                                    $"可选值：{string.Join("/", SimpleNeuralNetwork.SupportedActivations)}");
                            }
                            declared[idx] = actName; // 绑定到对应 Dense 层
                        }
                        else
                        {
                            LogMessage(BuildLogLevel.Warning,
                                "TrainModelCard: 有激活函数卡未找到可归属的全连接层（可能重复声明或顺序颠倒），已忽略。");
                        }
                    }
                    else
                    {
                        LogMessage(BuildLogLevel.Warning, $"TrainModelCard: 忽略不支持的层类型 {layer.LayerType}。");
                    }
                }
            }

            // 情况一：画布上没连任何 Dense → 使用默认结构（日志里说清是哪一种断点）
            if (units.Count == 0)
            {
                sizes = new int[] { inputDim, 10, outputDim }; // 对齐课程设计模型一 3-10-1
                acts = SimpleNeuralNetwork.DefaultActivations(sizes.Length);

                bool builderEmpty = modelObj == null || modelObj.Layers == null || modelObj.Layers.Count == 0;
                string reason = builderEmpty
                    ? "未收到任何层元数据（模型构建卡的「层列表」是空的，或「模型构建卡.模型」没连到本卡「模型」端口）"
                    : "模型构建卡里没有有效的「全连接层」卡（只收到了激活函数层？）";
                LogMessage(BuildLogLevel.Warning,
                    $"TrainModelCard: {reason}，已回退到课程设计默认结构 {inputDim}-{sizes[1]}-{outputDim}（3-10-1）。" +
                    "自查三步：①各层卡的【层元数据】端口是否连到模型构建卡的【层列表】（不是「输出张量」端口）；" +
                    "②模型构建卡的【模型】端口是否连到本卡的【模型】端口；③层顺序是否 Dense→激活→Dense。");
                return;
            }

            // 情况二：按规则补齐未声明的激活（末层 linear，其余 leaky_relu）
            for (int i = 0; i < declared.Count; i++)
            {
                if (declared[i] == null)
                    declared[i] = (i == declared.Count - 1) ? "linear" : "leaky_relu";
            }

            // 末层单元数与标签维数不符 → 追加 linear 输出层（修复旧版多算一层的 bug）
            if (units[units.Count - 1] != outputDim)
            {
                LogMessage(BuildLogLevel.Warning,
                    $"TrainModelCard: 最后一个全连接层单元数({units[units.Count - 1]})与标签维数({outputDim})不一致，" +
                    "已自动追加线性输出层。");
                units.Add(outputDim);
                declared.Add("linear");
            }

            // 组装最终结构：sizes[0]=输入维，其后依次是各 Dense 层
            sizes = new int[units.Count + 1];
            sizes[0] = inputDim;
            for (int i = 0; i < units.Count; i++) sizes[i + 1] = units[i];
            acts = declared.ToArray();
        }

        /// <summary>把网络结构渲染为右侧三维视口的神经网络拓扑图</summary>
        private void RenderTopology(IDataDelivery idd, int[] sizes)
        {
            if (sizes == null || sizes.Length < 2) return;
            try
            {
                NNVisualizer.Init(out var model, out var floor);
                NNVisualizer.BuildNetworkTopology(model, floor, sizes);
                NNVisualizer.Render(previewController, model, s => LogMessage(BuildLogLevel.Warning, s));
                idd.SetDataItem(1, model);
            }
            catch (Exception ex)
            {
                LogMessage(BuildLogLevel.Warning, "网络结构三维渲染失败（不影响训练结果）: " + ex.Message);
            }
        }
    }
}
