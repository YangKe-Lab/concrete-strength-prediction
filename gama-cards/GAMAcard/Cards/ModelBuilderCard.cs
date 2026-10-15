using System;
using System.Collections.Generic;
using System.Text;
using ProjectPenguin.Cards;
using ProjectPenguin.Cards.Enums;
using ProjectPenguin.Cards.ProcessorCardHelpers;

namespace MyGamaCards
{
    /// <summary>
    /// 模型构建卡片 —— 把画布上多个“全连接层/激活函数”卡的元数据按顺序聚合为一个 Model 对象
    ///
    /// 重要：多条层连线的先后顺序就是网络结构的顺序，
    /// 请务必按照 从输入侧到输出侧 的顺序依次连接（例如模型二 3-5-5-1：
    /// Dense(5) → Activation(leaky_relu) → Dense(5) → Activation(leaky_relu) → Dense(1)）。
    /// </summary>
    public class ModelBuilderCard : ProcessorCardBase
    {
        public ModelBuilderCard() : base("模型构建", "模型构建", "将多层元数据按顺序组合成模型结构", "深度学习", "模型") { }
        public override string EnglishAlias => "DL_ModelBuilder";
        private static readonly Guid sc_guid = Guid.Parse("{F3456789-0ABC-4DEF-9123-456789ABCDEF}");
        public override Guid CardGuid => sc_guid;

        protected override void AddInputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            // ListByList 模式：把所有连进来的 Layer 元数据收集成一个有序列表
            helper.AddWellKnownTypeOfDataCard(typeof(Layer), "层列表", "层列表", "按顺序输入的层元数据列表", DataProcessingMode.ListByList).IsOptional = true;
            helper.AddWellKnownTypeOfDataCard(typeof(string), "模型名", "模型名", "模型名称（仅作展示标识）", DataProcessingMode.OneByOne, "MyModel");
        }

        protected override void AddOutputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(Model), "模型", "模型", "聚合后的模型结构对象", DataProcessingMode.OneByOne);
        }

        public override void Build(IDataDelivery idd)
        {
            var layers = new List<Layer>();
            idd.GetDataList(0, layers);          // 收集全部上游层元数据（保持连线顺序）
            string name = "MyModel";
            idd.GetDataItem(1, ref name);

            // 空结构警告：没有接任何层时提示用户检查连线
            if (layers == null || layers.Count == 0)
            {
                LogMessage(BuildLogLevel.Warning,
                    "ModelBuilderCard: 未收到任何层元数据，请从全连接层卡/激活函数卡的【层元数据】输出端口连线到本卡的【层列表】端口。");
                idd.SetDataItem(0, new Model { Name = name, Layers = new List<Layer>() });
                return;
            }

            // 成功聚合：把层序打印出来，便于核对网络结构（与训练卡日志的 结构= 对照）
            var desc = new StringBuilder();
            for (int i = 0; i < layers.Count; i++)
            {
                var ly = layers[i];
                if (i > 0) desc.Append(" → ");
                if (ly == null) { desc.Append("(空层)"); continue; }
                if (ly.LayerType == "Dense")
                {
                    object u = null;
                    if (ly.Config != null) ly.Config.TryGetValue("units", out u);
                    desc.Append("Dense(").Append(u ?? "?").Append(")");
                }
                else if (ly.LayerType == "Activation")
                {
                    object a = null;
                    if (ly.Config != null) ly.Config.TryGetValue("activation", out a);
                    desc.Append("激活(").Append(a ?? "?").Append(")");
                }
                else desc.Append(ly.LayerType);
            }
            LogMessage(BuildLogLevel.Warning,
                $"ModelBuilderCard: 模型「{name}」已聚合 {layers.Count} 层：{desc}（该顺序就是网络结构顺序，请与训练卡日志的 结构= 核对）。");

            var model = new Model { Name = name, Layers = layers };
            idd.SetDataItem(0, model);
        }
    }
}
