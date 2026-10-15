using System;
using System.Collections.Generic;
using ProjectPenguin.Cards;
using ProjectPenguin.Cards.Enums;
using ProjectPenguin.Cards.ProcessorCardHelpers;

namespace MyGamaCards
{
    /// <summary>
    /// 激活函数卡片 —— 为紧邻上游的全连接层声明激活函数类型
    ///
    /// 支持的类型（不区分大小写）：
    /// - leaky_relu ：Leaky ReLU，负半轴斜率 0.01（课程设计默认，避免神经元死亡）
    /// - relu       ：标准 ReLU
    /// - sigmoid    ：Sigmoid
    /// - tanh       ：双曲正切
    /// - linear     ：恒等映射（回归任务输出层使用）
    ///
    /// 工作方式说明：
    /// 本卡不执行任何数学计算，只向下游输出一条 Layer 元数据；
    /// “激活函数卡 → 全连接层” 的归属关系由训练卡在解析模型结构时建立，
    /// 真正的激活运算在 SimpleNeuralNetwork 内部实现。
    /// </summary>
    public class ActivationCard : ProcessorCardBase
    {
        public ActivationCard() : base("激活函数", "激活函数", "为全连接层声明激活函数(leaky_relu/relu/sigmoid/tanh/linear)", "深度学习", "层") { }
        public override string EnglishAlias => "DL_Activation";
        private static readonly Guid sc_guid = Guid.Parse("{F2345678-9ABC-4DEF-8012-3456789ABCDE}");
        public override Guid CardGuid => sc_guid;

        protected override void AddInputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            // 上游张量占位（仅用于画布上的视觉连线，可不接）
            helper.AddWellKnownTypeOfDataCard(typeof(Tensor), "输入张量", "输入张量", "上游张量占位（可选）", DataProcessingMode.OneByOne).IsOptional = true;
            // 激活函数类型
            helper.AddWellKnownTypeOfDataCard(typeof(string), "激活类型", "激活类型", "leaky_relu(默认)/relu/sigmoid/tanh/linear", DataProcessingMode.OneByOne, "leaky_relu");
            // ↓ 新增（追加在末尾，不影响旧画布）：上游模型 —— 把上一张层卡的「模型」输出接进来，形成 1 对 1 的串联接线
            helper.AddWellKnownTypeOfDataCard(typeof(Model), "上游模型", "上游模型", "上一张层卡的「模型」输出（可选；用于把多层串成一条链，避免一个端口接多根线）", DataProcessingMode.OneByOne, new Model()).IsOptional = true;
        }

        protected override void AddOutputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(Tensor), "输出张量", "输出张量", "输出张量占位", DataProcessingMode.OneByOne);
            helper.AddWellKnownTypeOfDataCard(typeof(Layer), "层元数据", "层元数据", "激活层元数据（连接到模型构建卡）", DataProcessingMode.OneByOne);
            // ↓ 新增：链式模型 = 上游各层 + 本层（有序），可直接接到训练卡的「模型」端口
            helper.AddWellKnownTypeOfDataCard(typeof(Model), "模型", "模型", "上游各层 + 本层（有序）→ 直接接训练卡的「模型」端口", DataProcessingMode.OneByOne);
        }

        public override void Build(IDataDelivery idd)
        {
            Tensor input = null;
            string act = "leaky_relu";
            Model upstreamModel = null;
            idd.GetDataItem(0, ref input);
            idd.GetDataItem(1, ref act);
            idd.GetDataItem(2, ref upstreamModel);   // 第 3 个输入端口：上游模型（可为空）

            // 校验激活类型：归一化后不在支持列表中则警告并回退到 leaky_relu
            string normalized = SimpleNeuralNetwork.NormalizeActivation(act);
            if (normalized == null)
            {
                normalized = "leaky_relu";
                LogMessage(BuildLogLevel.Warning,
                    $"ActivationCard: 不支持的激活类型 '{act}'，已回退为 leaky_relu。" +
                    $"可选值：{string.Join("/", SimpleNeuralNetwork.SupportedActivations)}");
            }

            // 输出激活层元数据，供模型构建卡聚合
            var layer = new Layer
            {
                LayerType = "Activation",
                Config = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["activation"] = normalized
                }
            };

            // 张量端口仅做形状透传（保持画布视觉连贯，不携带真实数据）
            var output = new Tensor { Shape = input?.Shape, DType = "float32", Data = null };

            idd.SetDataItem(0, output);
            idd.SetDataItem(1, layer);

            // 累积模型（上游 + 本层）：串链接法用，直接接训练卡的「模型」端口
            var chain = new Model
            {
                Name = (upstreamModel != null && !string.IsNullOrWhiteSpace(upstreamModel.Name)) ? upstreamModel.Name : "ChainedModel",
                Layers = new List<Layer>()
            };
            if (upstreamModel != null && upstreamModel.Layers != null) chain.Layers.AddRange(upstreamModel.Layers);
            chain.Layers.Add(layer);
            idd.SetDataItem(2, chain);
        }
    }
}
