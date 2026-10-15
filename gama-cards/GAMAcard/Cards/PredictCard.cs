using System;
using System.Collections.Generic;
using ProjectPenguin.Cards;
using ProjectPenguin.Cards.Enums;
using ProjectPenguin.Cards.ProcessorCardHelpers;

namespace MyGamaCards
{
    /// <summary>
    /// 模型推理卡片 —— 用训练好的 ANN 对新样本做预测
    ///
    /// 课程设计典型用法：
    /// - 测试集预测：把 test.csv 的 Dataset 同时接到“输入数据”端口，
    ///   本卡自动取其特征矩阵 X（16行×3列）批量预测，输出 16×1 预测值；
    /// - 训练集自评：同理接 train.csv 的 Dataset。
    ///
    /// 端口说明：
    /// - 输入0 模型  ：训练卡输出的 TrainingResult（自动解包出网络），也可直接接网络对象
    /// - 输入1 输入数据：double[] 单样本 / double[][] 批量 / Dataset（自动取 X）
    /// - 输出0 预测结果：double[] 或 double[][]
    /// </summary>
    public class PredictCard : ProcessorCardBase
    {
        public PredictCard() : base("模型推理", "模型推理", "使用训练后的模型进行单样本或批量预测", "深度学习", "推理") { }
        public override string EnglishAlias => "DL_Predict";
        private static readonly Guid sc_guid = Guid.Parse("{F5678901-2ABC-4DEF-9345-6789ABCDEF01}");
        public override Guid CardGuid => sc_guid;

        protected override void AddInputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(object), "模型", "模型", "训练卡输出的 TrainingResult（推荐）或网络对象", DataProcessingMode.OneByOne);
            helper.AddWellKnownTypeOfDataCard(typeof(object), "输入数据", "输入数据", "待预测数据：Dataset（自动取X）/ double[][] / double[]", DataProcessingMode.OneByOne);
        }

        protected override void AddOutputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(object), "预测结果", "预测结果", "预测值 double[][]（每行一个样本的输出）", DataProcessingMode.OneByOne);
        }

        public override void Build(IDataDelivery idd)
        {
            object modelObj = null;
            object inputObj = null;
            idd.GetDataItem(0, ref modelObj);
            idd.GetDataItem(1, ref inputObj);

            // ---------- 第 1 步：取出模型（支持 ANN 网络 或 线性回归结果） ----------
            // 允许两种接法：直接连网络实例；或连整个 TrainingResult（自动解包 TrainedModel）
            SimpleNeuralNetwork net = null;
            LinearResult lr = null;
            if (modelObj is SimpleNeuralNetwork direct) net = direct;
            else if (modelObj is TrainingResult tr) net = tr.TrainedModel as SimpleNeuralNetwork;
            else if (modelObj is LinearResult linear) lr = linear;

            if (net == null && lr == null)
            {
                idd.SetDataItem(0, null);
                LogMessage(BuildLogLevel.Error,
                    "PredictCard: 模型无效。请连接“训练模型”卡或“线性模型”卡的输出。");
                return;
            }

            // ---------- 第 2 步：按输入类型分派预测 ----------
            // 情况一：单样本向量 → 返回 double[]
            if (inputObj is double[] single)
            {
                idd.SetDataItem(0, net != null ? net.Predict(single) : new double[] { lr.PredictOne(single) });
                return;
            }

            // 情况二：批量矩阵 → 返回 double[][]
            if (inputObj is double[][] batch)
            {
                idd.SetDataItem(0, net != null ? net.PredictBatch(batch) : lr.Predict(batch));
                return;
            }

            // 情况三：Dataset → 自动取特征矩阵 X 做批量预测（课程设计最常用）
            if (inputObj is Dataset ds)
            {
                if (ds.X == null || ds.SampleCount == 0)
                {
                    idd.SetDataItem(0, null);
                    LogMessage(BuildLogLevel.Error, "PredictCard: 收到的数据集中没有样本，请检查输入数据卡。");
                    return;
                }
                idd.SetDataItem(0, net != null ? net.PredictBatch(ds.X) : lr.Predict(ds.X));
                return;
            }

            // 其他类型一律明确报错，不静默失败
            idd.SetDataItem(0, null);
            LogMessage(BuildLogLevel.Error, "PredictCard: 输入数据类型不支持，需要 Dataset / double[][] / double[]。");
        }
    }
}
