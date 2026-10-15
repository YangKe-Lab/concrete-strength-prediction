using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using NSStructurePreview;

namespace MyGamaCards
{
    /// <summary>
    /// 带图表窗口的可视化卡片基类 —— 课程设计演示的主可视化方案。
    ///
    /// 用法：卡片 Build 计算完成后调用 SetFigures(窗口标题, 图表列表) 提交图表：
    /// - 图表窗口已打开 → 自动刷新（改参数重跑即联动，直播核心）
    /// - 窗口未打开 → 缓存图表，右键卡片 →「显示图表窗口」弹出
    /// 窗口里每个 ChartFigure 是一个标签页，可一键导出 2 倍分辨率 PNG（默认存到桌面\GAMA图表\）。
    ///
    /// 完全独立于 E-GAMA 三维预览（previewController），不依赖预览窗口，100% 可控、可复现。
    /// </summary>
    public abstract class PreviewCardBase : AddingStructurePreviewCardBase
    {
        protected PreviewCardBase(string name, string nameShort, string description, string category, string subCategory)
            : base(name, nameShort, description, category, subCategory) { }

        private ChartWindow _chart;
        private string _chartTitle = "";
        private List<ChartFigure> _figures = new List<ChartFigure>();

        /// <summary>本卡最近一次运行生成的全部图表（供导出使用）</summary>
        public IList<ChartFigure> CurrentFigures { get { return _figures; } }

        /// <summary>
        /// 提交/更新图表。
        /// show=false：窗口已开则刷新，未开则缓存（等菜单弹出）；
        /// show=true（菜单触发，UI 线程）：窗口不存在则创建，并置前显示。
        /// </summary>
        protected void SetFigures(string windowTitle, IList<ChartFigure> figures, bool show = false)
        {
            _chartTitle = windowTitle;
            _figures = figures == null ? new List<ChartFigure>() : new List<ChartFigure>(figures);

            if (_chart == null || _chart.IsDisposed)
            {
                if (!show) return;
                _chart = new ChartWindow(windowTitle);
                _chart.FormClosed += (s, e) => _chart = null;
            }
            if (show)
            {
                if (!_chart.Visible) _chart.Show();
                _chart.BringToFront();
            }
            if (_chart.Visible)
                _chart.SetFigures(_figures, windowTitle);   // 线程安全：内部 InvokeRequired 处理
        }

        /// <summary>把本卡的全部图表导出为 PNG（无需打开窗口）</summary>
        protected void ExportFiguresToPng()
        {
            if (_figures == null || _figures.Count == 0)
            {
                MessageBox.Show("还没有图表可导出：请先在画布上运行本卡片，再执行导出。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            string msg;
            int ok = ChartWindow.ExportFigures(_chartTitle, _figures, out msg);
            MessageBox.Show(string.Format("成功导出 {0} / {1} 张图表。\r\n\r\n{2}", ok, _figures.Count, msg),
                "导出图表 PNG", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>在卡片右键菜单追加图表相关入口（基类菜单保留）</summary>
        public override void FillAdditionalCardMenuStripItem(ToolStripDropDown menu)
        {
            base.FillAdditionalCardMenuStripItem(menu);

            var item = new ToolStripMenuItem("📊 显示图表窗口（可导出 PNG）");
            item.Click += (s, e) => SetFigures(_chartTitle, _figures, true);
            menu.Items.Add(item);

            var exp = new ToolStripMenuItem("💾 导出全部图表 PNG");
            exp.Click += (s, e) => ExportFiguresToPng();
            menu.Items.Add(exp);
        }
    }
}
