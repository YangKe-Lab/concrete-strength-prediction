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
    /// 结果对比卡片 —— 课程设计的“最终结论”一图看全。
    ///
    /// 把训练模型卡 / 线性模型卡 / 模型评估卡输出的 ModelReport 全部连到本卡的
    /// 「模型报告」端口（多连线：一根线一个模型，顺序无所谓，卡片会自动按 R² 排序），
    /// 运行后即可得到 5 页对比图：
    ///   ① 精度总览   ：R² / MSE / MAE 分组柱状图 + R²-MSE 定位图
    ///   ② 全模型对照 ：所有模型 预测-真实 散点叠在同一张图上
    ///   ③ 逐样本对比 ：每个样本上各模型预测值与真实值折线
    ///   ④ 训练损失对比：多模型损失曲线（对数轴）
    ///   ⑤ 指标明细表 ：排名 + 全部指标 + 自动结论
    ///
    /// 右键卡片 →「显示图表窗口」查看；→「导出全部图表 PNG」把所有对比图存到桌面\GAMA图表\。
    /// </summary>
    public class CompareCard : PreviewCardBase
    {
        public CompareCard() : base("结果对比", "结果对比", "多模型精度汇总对比（柱状图/散点对照/逐样本折线/损失曲线/明细表，可导出 PNG）", "深度学习", "评估") { }
        public override string EnglishAlias => "DL_Compare";
        private static readonly Guid sc_guid = Guid.Parse("{C0A1B2C3-D4E5-4F60-8123-4567890ABCDE}");
        public override Guid CardGuid => sc_guid;

        protected override StructurePreviewMode PreviewMode => StructurePreviewMode.ModelMode;
        protected override ElementType ElementTypeForPreview => ElementType.None;

        protected override void AddInputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            // 【路线① 最省事】5 个独立入口，每个接一个模型（每根线 1 对 1，不需要"多连线"）
            for (int i = 1; i <= 5; i++)
            {
                helper.AddWellKnownTypeOfDataCard(typeof(ModelReport), "模型报告" + i, "报告" + i,
                    "第 " + i + " 个模型的「模型报告」（一个端口接一个模型，直连最省事）",
                    DataProcessingMode.OneByOne, new ModelReport()).IsOptional = true;
            }
            // 【路线③】多连线入口：同一个端口接多根（需右键端口「添加多个数据连接」）
            helper.AddWellKnownTypeOfDataCard(typeof(ModelReport), "模型报告(多连线)", "多连线报告",
                "同一个端口接多根「模型报告」（需右键端口用「添加多个数据连接」）", DataProcessingMode.ListByList).IsOptional = true;
            // 【路线②】链式汇总：把模型卡的「报告汇总」链直接接进来（1 对 1）
            helper.AddWellKnownTypeOfDataCard(typeof(ModelSet), "模型集合", "模型集合",
                "任一模型卡的「报告汇总」输出（可选；链式汇总，替代多连线）", DataProcessingMode.OneByOne, new ModelSet()).IsOptional = true;
            helper.AddWellKnownTypeOfDataCard(typeof(string), "对比标题", "对比标题",
                "图表标题前缀（可选）", DataProcessingMode.OneByOne, "混凝土抗压强度预测");
        }

        protected override void AddOutputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(string), "对比摘要", "对比摘要",
                "各模型指标 + 排名结论的文字摘要", DataProcessingMode.OneByOne);
        }

        public override void Build(IDataDelivery idd)
        {
            // ---------- 收集三种入口的模型报告 ----------
            var all = new List<ModelReport>();

            // 路线①：5 个独立端口（报告1..报告5）
            for (int i = 0; i < 5; i++)
            {
                ModelReport r = null;
                idd.GetDataItem(i, ref r);
                if (r != null) all.Add(r);
            }
            // 路线③：多连线端口
            var many = new List<ModelReport>();
            try { idd.GetDataList(5, many); } catch { }
            if (many != null) all.AddRange(many);
            // 路线②：链式汇总（模型集合）
            ModelSet set = null;
            idd.GetDataItem(6, ref set);
            if (set != null && set.Items != null) all.AddRange(set.Items);

            string title = "混凝土抗压强度预测";
            idd.GetDataItem(7, ref title);

            var valid = new List<ModelReport>();
            foreach (var r in all)
                if (r != null && r.TrainCount > 0 && !valid.Contains(r)) valid.Add(r);

            if (valid.Count == 0)
            {
                const string hint = "(未有模型报告) 三条路线任选一条：① 把 5 张模型卡的「模型报告」分别接到本卡的「报告1…报告5」（最简单，一根线一个口）；" +
                                    "② 用「报告汇总」链串起来接到「模型集合」；③ 全部接到「多连线报告」端口（需右键端口「添加多个数据连接」）。";
                idd.SetDataItem(0, hint);
                LogMessage(BuildLogLevel.Error,
                    "CompareCard: 未收到任何模型报告。最省事的做法：把各模型卡的「模型报告」分别连到本卡的【报告1…报告5】五个入口（一模型一个口，不用多连线）。" +
                    "也可以用「报告汇总」链接到【模型集合】，或全部接【多连线报告】（需右键端口「添加多个数据连接」）。");

                var placeholder = new List<ChartFigure>
                {
                    new ChartFigure("等待数据", (g, w, h) =>
                    {
                        Mpl.FigureTitle(g, w, "结果对比 · 等待模型数据", string.Empty, true);
                        Mpl.TextCenter(g, "把各模型卡的「模型报告」分别连到本卡的【报告1…报告5】（一模型一个口，最省事）。",
                            Mpl.FCn, Color.FromArgb(120, 120, 120), w / 2f, h / 2f - 30);
                        Mpl.TextCenter(g, "也可以用「报告汇总」链串起来接到【模型集合】，或全部接到【多连线报告】端口。",
                            Mpl.FCnSmall, Color.FromArgb(150, 150, 150), w / 2f, h / 2f);
                        Mpl.TextCenter(g, "连好并运行后，这里会自动生成 5 页对比图，可一键导出 PNG。",
                            Mpl.FCnSmall, Color.FromArgb(150, 150, 150), w / 2f, h / 2f + 30);
                    })
                };
                SetFigures("结果对比", placeholder);
                return;
            }

            // ---------- 生成对比图表 ----------
            var figs = FigureFactory.CompareFigures(valid);
            SetFigures("结果对比 · " + title, figs);

            // ---------- 文字摘要（严格按测试集 R² 排序） ----------
            valid.Sort((a, b) => b.SortScore().CompareTo(a.SortScore()));
            var sb = new StringBuilder();
            sb.AppendFormat("模型对比（共 {0} 个，按 {1} R² 降序）：",
                valid.Count, valid[0].HasTest ? "测试集" : "训练集");
            int rank = 1;
            foreach (var r in valid)
            {
                sb.AppendFormat("\r\n  {0}. {1}", rank, r.SummaryText());
                rank++;
            }
            var best = valid[0];
            sb.AppendFormat("\r\n结论：{0} 综合表现最好（{1} R²={2}，MSE={3}，MAE={4}）。",
                best.DisplayName, best.HasTest ? "测试集" : "训练集",
                Mpl.F4(best.HasTest ? best.TestR2 : best.TrainR2),
                Mpl.F4(best.HasTest ? best.TestMSE : best.TrainMSE),
                Mpl.F4(best.HasTest ? best.TestMAE : best.TrainMAE));
            idd.SetDataItem(0, sb.ToString());

            LogMessage(BuildLogLevel.Warning, "CompareCard: " + sb.ToString().Replace("\r\n", " | ") +
                "（右键卡片可显示 5 页对比图并导出 PNG）");
        }
    }
}
