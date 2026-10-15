using System;
using System.Collections.Generic;
using ProjectPenguin.Cards;
using ProjectPenguin.Cards.Enums;
using ProjectPenguin.Cards.ProcessorCardHelpers;

namespace MyGamaCards
{
    /// <summary>
    /// 全连接层卡片 —— 声明一个全连接层（Dense 层）的神经元个数
    ///
    /// 工作方式说明：
    /// 本卡不执行数学计算，只输出一条 Layer 元数据（LayerType="Dense"）；
    /// 多个“全连接层卡 + 激活函数卡”的元数据按连线顺序汇入“模型构建卡”，
    /// 由训练卡解析后创建真正的神经网络。
    /// 课程设计用法：
    /// 模型一 3-10-1 → 一个 Dense(10)；模型二 3-5-5-1 → 两个 Dense(5)
    /// （输入/输出维数由数据自动决定，无需声明）
    /// </summary>
    public class DenseLayerCard : ProcessorCardBase
    {
        public DenseLayerCard() : base("全连接层", "全连接层", "声明一个全连接层的单元数", "深度学习", "层") { }
        public override string EnglishAlias => "DL_Dense";
        private static readonly Guid sc_guid = Guid.Parse("{F1A2B3C4-5678-4DEF-9012-ABCDEF123456}");
        public override Guid CardGuid => sc_guid;

        protected override void AddInputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            // 上游张量占位（仅用于画布视觉连线，可不接）
            helper.AddWellKnownTypeOfDataCard(typeof(Tensor), "输入张量", "输入张量", "上游张量占位（可选）", DataProcessingMode.OneByOne).IsOptional = true;
            // 本层神经元个数
            helper.AddWellKnownTypeOfDataCard(typeof(int), "单元数", "单元数", "本层神经元个数（如模型一隐藏层填 10）", DataProcessingMode.OneByOne, 10);
            // 是否使用偏置项
            helper.AddWellKnownTypeOfDataCard(typeof(bool), "使用偏置", "使用偏置", "是否使用偏置 b", DataProcessingMode.OneByOne, true);
            // ↓ 新增（追加在末尾，不影响旧画布）：上游模型 —— 把上一张层卡的「模型」输出接进来，形成 1 对 1 的串联接线
            helper.AddWellKnownTypeOfDataCard(typeof(Model), "上游模型", "上游模型", "上一张层卡的「模型」输出（可选；用于把多层串成一条链，避免一个端口接多根线）", DataProcessingMode.OneByOne, new Model()).IsOptional = true;
        }

        protected override void AddOutputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(Tensor), "输出张量", "输出张量", "输出张量占位", DataProcessingMode.OneByOne);
            helper.AddWellKnownTypeOfDataCard(typeof(Layer), "层元数据", "层元数据", "Dense 层元数据（连接到模型构建卡）", DataProcessingMode.OneByOne);
            // ↓ 新增：链式模型 = 上游各层 + 本层（有序），可直接接到训练卡的「模型」端口
            helper.AddWellKnownTypeOfDataCard(typeof(Model), "模型", "模型", "上游各层 + 本层（有序）→ 直接接训练卡的「模型」端口", DataProcessingMode.OneByOne);
        }

        public override void Build(IDataDelivery idd)
        {
            Tensor input = null;
            int units = 10;
            bool useBias = true;
            Model upstreamModel = null;
            idd.GetDataItem(0, ref input);
            idd.GetDataItem(1, ref units);
            idd.GetDataItem(2, ref useBias);
            idd.GetDataItem(3, ref upstreamModel);   // 第 4 个输入端口：上游模型（可为空）

            // 单元数下限保护：至少 1 个神经元
            if (units < 1)
            {
                LogMessage(BuildLogLevel.Warning, $"DenseLayerCard: 单元数 {units} 非法，已按 1 处理。");
                units = 1;
            }

            // 输出 Dense 层元数据
            var layer = new Layer
            {
                LayerType = "Dense",
                Config = new System.Collections.Generic.Dictionary<string, object>
                {
                    ["units"] = units,
                    ["useBias"] = useBias
                }
            };

            // 张量端口仅做形状透传
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
