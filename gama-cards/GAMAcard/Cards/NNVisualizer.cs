using System;
using System.Collections.Generic;
using System.Drawing;
using NSGeo.DocObjects;
using NSGeo.Geometry;
using NSFramework.NSModel;
using NSFramework.NSElement;
using NSGraphicManager;
using NSStructurePreview;

namespace MyGamaCards
{
    /// <summary>
    /// 统一三维可视化工具 —— 供各卡片在 Build 末尾把结果渲染到 E-GAMA 右侧三维视口。
    ///
    /// 设计语言（全部用结构模型杆件表达）：
    /// - 坐标轴：三根细长杆（X 红意/Y 绿意/Z 高意，实际用长度区分）
    /// - 数据点：竖直短柱（立在地面上，高度 = 数值）
    /// - 连接/理想线：细杆 NSBrace
    /// - 拟合面：网格线组成的曲面（横竖网格杆）
    /// - 神经网络：层=竖直柱排，连接=斜杆
    ///
    /// 所有数值先经 Normalize() 归一化到毫米空间，避免数据量纲差异。
    /// </summary>
    public static class NNVisualizer
    {
        // 渲染空间：以原点为中心，最大 12000mm
        public const double SPACE = 12000.0;

        // 常用截面
        public static readonly RectangleSection AxisSection =
            new RectangleSection { Height = 60, Width = 60, SectionType = FrameSectionType.矩形, Material = MaterialType.钢材 };
        public static readonly RectangleSection PointSection =
            new RectangleSection { Height = 120, Width = 120, SectionType = FrameSectionType.矩形, Material = MaterialType.混凝土 };
        public static readonly RectangleSection LinkSection =
            new RectangleSection { Height = 30, Width = 30, SectionType = FrameSectionType.矩形, Material = MaterialType.钢材 };

        /// <summary>创建模型 + 单标准层（所有可视化构件挂在这一层）</summary>
        public static void Init(out NSModel model, out NSStdFloor floor)
        {
            model = new NSModel();
            model.BasePlane = Plane.WorldXY;
            model.ZeroPlane = Plane.WorldXY;
            floor = new NSStdFloor();
            floor.referencePlane = new Plane(new Point3d(0, 0, 0), Vector3d.ZAxis);
            floor.Height = 0;
            floor.Param = new NSStdFloorParam();
        }

        /// <summary>数值归一化到 [-half, +half] 毫米区间</summary>
        public static double Norm(double v, double min, double max, double half = 5000.0)
        {
            if (max - min < 1e-12) return 0;
            double t = (v - min) / (max - min);          // 0..1
            if (t < 0) t = 0; if (t > 1) t = 1;
            return (t - 0.5) * 2 * half;                  // -half..+half
        }

        /// <summary>添加一根柱（数据点/神经元）</summary>
        public static void AddColumn(NSModel model, NSStdFloor floor, double x, double y, double zBottom, double zTop, NSFrameSection section)
        {
            var col = new NSColumn();
            col.Parent = floor;
            col.Curve = new LineCurve(new Point3d(x, y, zBottom), new Point3d(x, y, zTop));
            col.Section = section ?? PointSection;
            model.AddElement(col);
        }

        /// <summary>添加一根连接杆</summary>
        public static void AddLink(NSModel model, NSStdFloor floor, double x1, double y1, double z1, double x2, double y2, double z2, NSFrameSection section)
        {
            var brace = new NSBrace();
            brace.Parent = floor;
            brace.Curve = new LineCurve(new Point3d(x1, y1, z1), new Point3d(x2, y2, z2));
            brace.Section = section ?? LinkSection;
            model.AddElement(brace);
        }

        /// <summary>三维坐标轴：X 长 9000 / Y 长 7500 / Z 高 6000，从原点出发</summary>
        public static void BuildAxes(NSModel model, NSStdFloor floor)
        {
            AddLink(model, floor, 0, 0, 0, 9000, 0, 0, AxisSection);   // X
            AddLink(model, floor, 0, 0, 0, 0, 7500, 0, AxisSection);   // Y
            AddLink(model, floor, 0, 0, 0, 0, 0, 6000, AxisSection);   // Z
        }

        /// <summary>散点数据：每个点一根竖直短柱，立于 (x, y)，高度 z（均需已归一化）</summary>
        public static void AddPointCloud(NSModel model, NSStdFloor floor, IList<double> xs, IList<double> ys, IList<double> zs, NSFrameSection section = null)
        {
            int n = xs.Count;
            for (int i = 0; i < n; i++)
                AddColumn(model, floor, xs[i], ys[i], 0, Math.Max(60, zs[i]), section ?? PointSection);
        }

        /// <summary>拟合面网格：在 x-y 网格上按 zFn 求高度，画横竖网格杆</summary>
        public static void AddSurfaceGrid(NSModel model, NSStdFloor floor,
                                          Func<double, double, double> zFn,
                                          int gridN = 10, double half = 4500.0)
        {
            for (int i = 0; i <= gridN; i++)
            {
                double t = (double)i / gridN;
                double x = -half + t * 2 * half;
                double y1 = -half, y2 = half;
                AddLink(model, floor, x, y1, zFn(x, y1), x, y2, zFn(x, y2), LinkSection);
            }
            for (int j = 0; j <= gridN; j++)
            {
                double t = (double)j / gridN;
                double y = -half + t * 2 * half;
                double x1 = -half, x2 = half;
                AddLink(model, floor, x1, y, zFn(x1, y), x2, y, zFn(x2, y), LinkSection);
            }
        }

        /// <summary>理想线 y=x：从 (min,min) 到 (max,max) 的斜杆（x-y 平面内）</summary>
        public static void AddDiagonal(NSModel model, NSStdFloor floor, double min, double max, double z = 0)
        {
            AddLink(model, floor, min, min, z, max, max, z, AxisSection);
        }

        /// <summary>神经网络拓扑：层=竖直柱排（输入混凝土/隐藏钢材/输出粗钢材），层间=斜杆连接</summary>
        public static void BuildNetworkTopology(NSModel model, NSStdFloor floor, int[] sizes,
                                                double neuronGap = 900.0, double layerGap = 3500.0, double pillarH = 800.0)
        {
            var hiddenSection = new RectangleSection { Height = 150, Width = 150, SectionType = FrameSectionType.矩形, Material = MaterialType.钢材 };
            var outputSection = new RectangleSection { Height = 260, Width = 260, SectionType = FrameSectionType.矩形, Material = MaterialType.钢材 };

            // 各层神经元 y 坐标（居中）
            var layerYs = new List<double[]>();
            for (int l = 0; l < sizes.Length; l++)
            {
                int n = sizes[l];
                var ys = new double[n];
                for (int j = 0; j < n; j++) ys[j] = (j - (n - 1) / 2.0) * neuronGap;
                layerYs.Add(ys);
            }
            double xL(int l) => (l - (sizes.Length - 1) / 2.0) * layerGap;

            // 神经元柱
            for (int l = 0; l < sizes.Length; l++)
            {
                var sec = l == 0 ? PointSection : (l == sizes.Length - 1 ? outputSection : hiddenSection);
                for (int j = 0; j < sizes[l]; j++)
                    AddColumn(model, floor, xL(l), layerYs[l][j], 0, pillarH, sec);
            }
            // 层间连接
            double zMid = pillarH * 0.55;
            for (int l = 0; l < sizes.Length - 1; l++)
                for (int j = 0; j < sizes[l]; j++)
                    for (int k = 0; k < sizes[l + 1]; k++)
                        AddLink(model, floor, xL(l), layerYs[l][j], zMid, xL(l + 1), layerYs[l + 1][k], zMid, LinkSection);
        }

        /// <summary>把三组数值坐标转成 Point3d 列表（用于 ListByList 输出端口，预览窗口自动渲染）</summary>
        public static List<Point3d> BuildPointList(IList<double> xs, IList<double> ys, IList<double> zs)
        {
            var pts = new List<Point3d>(xs.Count);
            for (int i = 0; i < xs.Count; i++)
                pts.Add(new Point3d(xs[i], ys[i], Math.Max(60, zs[i])));
            return pts;
        }

        /// <summary>坐标轴：X 9000 / Y 7500 / Z 6000 三根线（从原点出发）</summary>
        public static List<LineCurve> BuildAxisLines()
        {
            return new List<LineCurve>
            {
                new LineCurve(new Point3d(0, 0, 0), new Point3d(9000, 0, 0)),
                new LineCurve(new Point3d(0, 0, 0), new Point3d(0, 7500, 0)),
                new LineCurve(new Point3d(0, 0, 0), new Point3d(0, 0, 6000))
            };
        }

        /// <summary>神经网络拓扑：层=竖直柱线，层间=斜连线（全部 LineCurve）</summary>
        public static List<LineCurve> BuildTopologyLines(int[] sizes,
                                                         double neuronGap = 900.0, double layerGap = 3500.0, double pillarH = 800.0)
        {
            var lines = new List<LineCurve>();
            var layerYs = new List<double[]>();
            for (int l = 0; l < sizes.Length; l++)
            {
                int n = sizes[l];
                var ys = new double[n];
                for (int j = 0; j < n; j++) ys[j] = (j - (n - 1) / 2.0) * neuronGap;
                layerYs.Add(ys);
            }
            double xL(int l) => (l - (sizes.Length - 1) / 2.0) * layerGap;

            // 神经元柱线（0 → pillarH）
            for (int l = 0; l < sizes.Length; l++)
                for (int j = 0; j < sizes[l]; j++)
                    lines.Add(new LineCurve(new Point3d(xL(l), layerYs[l][j], 0), new Point3d(xL(l), layerYs[l][j], pillarH)));
            // 层间全连接斜线
            double zMid = pillarH * 0.55;
            for (int l = 0; l < sizes.Length - 1; l++)
                for (int j = 0; j < sizes[l]; j++)
                    for (int k = 0; k < sizes[l + 1]; k++)
                        lines.Add(new LineCurve(new Point3d(xL(l), layerYs[l][j], zMid), new Point3d(xL(l + 1), layerYs[l + 1][k], zMid)));
            return lines;
        }

        /// <summary>拟合面网格线：在 x-y 网格上按 zFn 求高度，返回横竖 LineCurve 列表</summary>
        public static List<LineCurve> BuildSurfaceGridLines(Func<double, double, double> zFn,
                                                            int gridN = 10, double half = 4500.0)
        {
            var lines = new List<LineCurve>();
            for (int i = 0; i <= gridN; i++)
            {
                double t = (double)i / gridN;
                double x = -half + t * 2 * half;
                double y1 = -half, y2 = half;
                lines.Add(new LineCurve(new Point3d(x, y1, zFn(x, y1)), new Point3d(x, y2, zFn(x, y2))));
            }
            for (int j = 0; j <= gridN; j++)
            {
                double t = (double)j / gridN;
                double y = -half + t * 2 * half;
                double x1 = -half, x2 = half;
                lines.Add(new LineCurve(new Point3d(x1, y, zFn(x1, y)), new Point3d(x2, y, zFn(x2, y))));
            }
            return lines;
        }

        /// <summary>理想线 y=x：从 (min,min) 到 (max,max) 的斜线（x-y 平面内，z 固定）</summary>
        public static List<LineCurve> BuildDiagonalLines(double min, double max, double z = 0)
        {
            return new List<LineCurve> { new LineCurve(new Point3d(min, min, z), new Point3d(max, max, z)) };
        }

        // ==================== 学术风彩色图元（预览窗口自动渲染） ====================
        // 原理：框架 DataCard 渲染时按运行时类型查 RenderingManager.RenderTypeDirectionCache（优先于 RenderMap），
        // 我们注册 VPoint/VLine/VText 的自定义处理器即可逐元素控制颜色/粗细/大小。
        // 学术风规范：坐标轴=黑色细线（非柱子），数据点=小尺寸深蓝，网格=细黑灰线。

        public static readonly Color CAxis = Color.FromArgb(255, 35, 35, 35);   // 黑色坐标轴
        public static readonly Color CPoint = Color.FromArgb(255, 31, 78, 121); // 学术深蓝（数据点）
        public static readonly Color CGrid = Color.FromArgb(255, 60, 60, 60);   // 深灰网格线

        private static bool s_handlers_registered;

        /// <summary>注册 VPoint/VLine/VText 的渲染处理器（幂等，Build 开头调用一次即可）</summary>
        public static void EnsureRenderHandlersRegistered()
        {
            if (s_handlers_registered) return;
            s_handlers_registered = true;
            try
            {
                var cache = ProjectPenguin.Managers.RenderingManager.RenderTypeDirectionCache;
                cache[typeof(VPoint)] = delegate (NSRender r, object o)
                {
                    var v = (VPoint)o;
                    var p = new NSRenderPointProperty { Color = v.Color, Size = (PointWeight)v.Size, Visibility = true, Transparency = 1.0 };
                    return new IntPtr[] { r.RenderPoint(v.Point, p, null) };
                };
                cache[typeof(VLine)] = delegate (NSRender r, object o)
                {
                    var v = (VLine)o;
                    var p = new NSRenderCurveProperty { Color = v.Color, LineWeight = (LineWeight)v.Weight, Visibility = true, Transparency = 1.0 };
                    return new IntPtr[] { r.RenderCurve(v.Curve, 100.0, p, null) };
                };
                cache[typeof(VText)] = delegate (NSRender r, object o)
                {
                    var v = (VText)o;
                    var p = new NSRenderTextProperty
                    {
                        TextContent = v.Text,
                        TextHeight = v.Height,
                        TextLocation = v.Location,
                        TextDirection = Vector3d.XAxis,
                        TextNormal = Vector3d.ZAxis,
                        HorizontalAlignment = TextHorizontalAlignment.Left,
                        VerticalAlignment = TextVerticalAlignment.Bottom,
                        Color = v.Color,
                        Visibility = true,
                        Transparency = 1.0
                    };
                    return new IntPtr[] { r.RenderText(v.Text, p, null) };
                };
            }
            catch { }
        }

        /// <summary>彩色点云（学术风：小尺寸深蓝点）</summary>
        public static List<VPoint> BuildColoredPoints(IList<double> xs, IList<double> ys, IList<double> zs,
                                                       Color color, int size = 320)
        {
            var pts = new List<VPoint>(xs.Count);
            for (int i = 0; i < xs.Count; i++)
                pts.Add(new VPoint { Point = new Point3d(xs[i], ys[i], Math.Max(60, zs[i])), Color = color, Size = size });
            return pts;
        }

        /// <summary>黑色细坐标轴：X 9000 / Y 7500 / Z 6000（学术风，线而非柱）</summary>
        public static List<VLine> BuildColoredAxes(double lenX = 9000, double lenY = 7500, double lenZ = 6000, int weight = 100)
        {
            return new List<VLine>
            {
                new VLine { Curve = new LineCurve(new Point3d(0, 0, 0), new Point3d(lenX, 0, 0)), Color = CAxis, Weight = weight },
                new VLine { Curve = new LineCurve(new Point3d(0, 0, 0), new Point3d(0, lenY, 0)), Color = CAxis, Weight = weight },
                new VLine { Curve = new LineCurve(new Point3d(0, 0, 0), new Point3d(0, 0, lenZ)), Color = CAxis, Weight = weight }
            };
        }

        /// <summary>坐标轴文字标注 X/Y/Z（学术风：黑色小字）</summary>
        public static List<VText> BuildAxisLabels(double lenX = 9000, double lenY = 7500, double lenZ = 6000, double height = 500)
        {
            return new List<VText>
            {
                new VText { Text = "X", Location = new Point3d(lenX + 400, 0, 0), Color = CAxis, Height = height },
                new VText { Text = "Y", Location = new Point3d(0, lenY + 400, 0), Color = CAxis, Height = height },
                new VText { Text = "Z", Location = new Point3d(0, 0, lenZ + 400), Color = CAxis, Height = height }
            };
        }

        /// <summary>把 LineCurve 列表染成统一颜色</summary>
        public static List<VLine> ColorLines(List<LineCurve> curves, Color color, int weight = 300)
        {
            var lines = new List<VLine>(curves.Count);
            foreach (var c in curves)
                lines.Add(new VLine { Curve = c, Color = color, Weight = weight });
            return lines;
        }

        /// <summary>神经网络拓扑：神经元柱=深蓝，层间连接=浅灰（预留，当前未用于卡片输出）</summary>
        public static List<VLine> BuildColoredTopology(int[] sizes,
                                                       double neuronGap = 900.0, double layerGap = 3500.0, double pillarH = 800.0)
        {
            var layerYs = new List<double[]>();
            for (int l = 0; l < sizes.Length; l++)
            {
                int n = sizes[l];
                var ys = new double[n];
                for (int j = 0; j < n; j++) ys[j] = (j - (n - 1) / 2.0) * neuronGap;
                layerYs.Add(ys);
            }
            double xL(int l) => (l - (sizes.Length - 1) / 2.0) * layerGap;

            var linkColor = Color.FromArgb(255, 160, 160, 160);
            var lines = new List<VLine>();
            for (int l = 0; l < sizes.Length; l++)
                for (int j = 0; j < sizes[l]; j++)
                    lines.Add(new VLine
                    {
                        Curve = new LineCurve(new Point3d(xL(l), layerYs[l][j], 0), new Point3d(xL(l), layerYs[l][j], pillarH)),
                        Color = CPoint, Weight = 100
                    });
            double zMid = pillarH * 0.55;
            for (int l = 0; l < sizes.Length - 1; l++)
                for (int j = 0; j < sizes[l]; j++)
                    for (int k = 0; k < sizes[l + 1]; k++)
                        lines.Add(new VLine
                        {
                            Curve = new LineCurve(new Point3d(xL(l), layerYs[l][j], zMid), new Point3d(xL(l + 1), layerYs[l + 1][k], zMid)),
                            Color = linkColor, Weight = 60
                        });
            return lines;
        }

        /// <summary>拟合面网格线（含坐标轴），深灰细线（学术风）</summary>
        public static List<VLine> BuildColoredSurfaceGridLines(Func<double, double, double> zFn,
                                                               int gridN = 10, double half = 4500.0)
        {
            var lines = new List<VLine>();
            for (int i = 0; i <= gridN; i++)
            {
                double t = (double)i / gridN;
                double x = -half + t * 2 * half;
                double y1 = -half, y2 = half;
                lines.Add(new VLine
                {
                    Curve = new LineCurve(new Point3d(x, y1, zFn(x, y1)), new Point3d(x, y2, zFn(x, y2))),
                    Color = CGrid, Weight = 60
                });
            }
            for (int j = 0; j <= gridN; j++)
            {
                double t = (double)j / gridN;
                double y = -half + t * 2 * half;
                double x1 = -half, x2 = half;
                lines.Add(new VLine
                {
                    Curve = new LineCurve(new Point3d(x1, y, zFn(x1, y)), new Point3d(x2, y, zFn(x2, y))),
                    Color = CGrid, Weight = 60
                });
            }
            return lines;
        }

        /// <summary>组装并推入三维视口（带容错，渲染失败不中断卡片流程）。</summary>
        /// <remarks>
        /// 注意：本平台的「预览窗口」自动渲染的是几何图元（Point3d/Line/Curve/Mesh 等，见 RenderingManager.RenderMap），
        /// NSModel 不走自动渲染（只能右键端口「查看模型」弹模态窗口）。
        /// 所以卡片要"自动显示"，必须额外输出 ListByList 的 Point3d/LineCurve 端口（见 BuildPointList/BuildLineList）。
        /// </remarks>
        public static bool Render(NSPreviewController previewController, NSModel model, Action<string> log = null)
        {
            if (model == null) return false;
            if (previewController == null)
            {
                log?.Invoke("三维预览未显示：请右键本卡输出端口（如「数据分布」）→「查看模型」打开三维视口，之后改参数重跑会自动刷新。");
                return false;
            }
            try
            {
                try { model.ClassifyFloorByTower(); }
                catch { /* 归并失败不阻塞渲染 */ }
                PreviewExportMethod.GetModelDataToRender(previewController, model);
                return true;
            }
            catch (Exception ex)
            {
                log?.Invoke("三维渲染失败（不影响卡片计算）: " + ex.Message);
                return false;
            }
        }
    }

    // ==================== 彩色渲染图元（输出端口类型） ====================
    // 框架 DataCard 渲染时按运行时类型查 RenderTypeDirectionCache（优先于 RenderMap），
    // 所以只要在 NNVisualizer.EnsureRenderHandlersRegistered() 注册过，这些类型就会被
    // 预览窗口自动渲染，且每个元素可单独控制颜色/粗细/大小（MATLAB 风格）。
    // 输出数据不会序列化进 .pga，仅在 Build→渲染之间瞬时存在，自定义类型安全。

    /// <summary>彩色点：Point3d + 颜色 + 大小</summary>
    [Serializable]
    public class VPoint
    {
        public Point3d Point { get; set; }
        public System.Drawing.Color Color { get; set; }
        public int Size { get; set; }
    }

    /// <summary>彩色线：LineCurve + 颜色 + 线宽</summary>
    [Serializable]
    public class VLine
    {
        public LineCurve Curve { get; set; }
        public System.Drawing.Color Color { get; set; }
        public int Weight { get; set; }
    }

    /// <summary>彩色文字标注：文本 + 位置 + 颜色 + 字高</summary>
    [Serializable]
    public class VText
    {
        public string Text { get; set; }
        public Point3d Location { get; set; }
        public System.Drawing.Color Color { get; set; }
        public double Height { get; set; }
    }
}
