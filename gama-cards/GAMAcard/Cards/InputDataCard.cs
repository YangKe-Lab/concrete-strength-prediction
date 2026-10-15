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
    /// 输入数据卡片 —— 把磁盘上的 CSV 数据文件装载成 Dataset 对象
    ///
    /// 课程设计典型用法：
    /// 加载经 GAMA 内置 Python 卡片预处理（缺失值填充 + Z-Score 标准化 + 数据集划分）后导出的
    /// train.csv（66行×4列）与 test.csv（16行×4列）：
    /// 前 3 列为特征（水灰比/粉煤灰/硅灰），最后 1 列为标签（抗压强度）。
    /// 数据路径留空时自动加载内置的课程设计真实数据（train.xlsx 的 66 个样本，Z-Score 标准化后），
    /// 演示时无需准备任何文件，拖卡即可训练。
    ///
    /// 编码兼容：自动识别 UTF-8（含 BOM）；若不是合法 UTF-8 则按 GB18030（中文 Windows 常见编码）读取，
    /// 避免用 Excel“另存为 CSV”产生的中文乱码问题。
    /// </summary>
    public class InputDataCard : PreviewCardBase
    {
        public InputDataCard() : base("输入数据", "输入数据", "加载 CSV 训练/测试数据（路径留空=内置混凝土 demo 66 样本；右侧三维视口实时显示数据分布）", "深度学习", "数据") { }
        public override string EnglishAlias => "DL_InputData";
        private static readonly Guid sc_guid = Guid.Parse("{D1A6E2B1-3C4D-4E5F-8123-ABCDEF012345}");
        public override Guid CardGuid => sc_guid;

        protected override StructurePreviewMode PreviewMode => StructurePreviewMode.ModelMode;
        protected override ElementType ElementTypeForPreview => ElementType.None;

        protected override void AddInputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(string), "数据路径", "路径", "CSV 文件完整路径，留空=内置混凝土抗压强度 demo 数据", DataProcessingMode.OneByOne, "");
            // ↓ 以下两个端口仅为兼容旧画布保留，当前版本不生效；批大小与验证比例请在训练卡按需设置
            helper.AddWellKnownTypeOfDataCard(typeof(int), "批大小", "批大小", "【当前版本不生效】本项目采用全批量梯度下降", DataProcessingMode.OneByOne, 32);
            helper.AddWellKnownTypeOfDataCard(typeof(double), "验证集比例", "验证比例", "【当前版本不生效】请预先在 Python 卡片中划分好训练/测试集", DataProcessingMode.OneByOne, 0.2);
            // 指定特征列：逗号分隔的列名或 0 起始列索引；留空默认取除最后一列外的所有列
            helper.AddWellKnownTypeOfDataCard(typeof(string), "特征列", "特征列", "特征列（列名或0起始索引，逗号分隔），留空=除最后一列外全部", DataProcessingMode.OneByOne, "");
            // 指定标签列：留空默认取最后一列（本课设即抗压强度）
            helper.AddWellKnownTypeOfDataCard(typeof(string), "标签列", "标签列", "标签列（列名或0起始索引），留空=最后一列", DataProcessingMode.OneByOne, "");
            // CSV 是否含表头行（第一行是列名则勾选）
            helper.AddWellKnownTypeOfDataCard(typeof(bool), "包含表头", "表头", "文件首行是否为表头（列名）", DataProcessingMode.OneByOne, true);
            // CSV 分隔符，默认英文逗号
            helper.AddWellKnownTypeOfDataCard(typeof(string), "分隔符", "分隔符", "CSV 分隔符（单字符），默认英文逗号 ,", DataProcessingMode.OneByOne, ",");
            // ↓ 新增：内置数据集选择（路径留空时生效）：train=课程设计训练集(66样本) / test=课程设计测试集(16样本)
            helper.AddWellKnownTypeOfDataCard(typeof(string), "内置数据集", "内置数据集", "路径留空时加载哪份内置数据：train(66样本,默认) / test(16样本)", DataProcessingMode.OneByOne, "train");
        }

        protected override void AddOutputSideDataCards(IDataCardEmbedmentHelper helper)
        {
            helper.AddWellKnownTypeOfDataCard(typeof(Dataset), "数据集", "数据集", "输出的数据集对象（包含特征 X 与标签 Y）", DataProcessingMode.OneByOne);
            helper.AddWellKnownTypeOfDataCard(typeof(NSModel), "数据分布", "数据分布", "三维数据分布可视化（特征1-特征2-标签）", DataProcessingMode.OneByOne);
            // ↓ 彩色几何图元输出：MATLAB 风格，预览窗口自动渲染
            helper.AddWellKnownTypeOfDataCard(typeof(VPoint), "点云", "点云", "三维数据点云（彩色点，预览窗口自动显示）", DataProcessingMode.ListByList);
            helper.AddWellKnownTypeOfDataCard(typeof(VLine), "坐标轴", "坐标轴", "三维坐标轴（X红/Y绿/Z蓝，预览窗口自动显示）", DataProcessingMode.ListByList);
            helper.AddWellKnownTypeOfDataCard(typeof(VText), "轴标注", "轴标注", "坐标轴文字标注 X/Y/Z", DataProcessingMode.ListByList);
        }

        public override void Build(IDataDelivery idd)
        {
            NNVisualizer.EnsureRenderHandlersRegistered();   // 注册彩色图元渲染处理器（幂等）

            string path = ""; int batch = 32; double vs = 0.2;
            string featureCols = ""; string labelCols = ""; bool hasHeader = true; string delimiter = ",";
            string builtinSet = "train";
            idd.GetDataItem(0, ref path);
            idd.GetDataItem(1, ref batch);   // 仅保留端口占位，不参与计算
            idd.GetDataItem(2, ref vs);      // 同上
            idd.GetDataItem(3, ref featureCols);
            idd.GetDataItem(4, ref labelCols);
            idd.GetDataItem(5, ref hasHeader);
            idd.GetDataItem(6, ref delimiter);
            idd.GetDataItem(7, ref builtinSet);

            // ---------- 情况一：路径为空 → 输出内置“混凝土抗压强度”示例数据（课程设计真实数据） ----------
            if (string.IsNullOrWhiteSpace(path))
            {
                // 内置数据集选择：默认 train（66 样本，与课程设计 train.xlsx 一致）；
                // 填 test 则加载课程设计 test.xlsx 的 16 个测试样本（用于训练/测试对照）
                bool useTest = !string.IsNullOrWhiteSpace(builtinSet) && builtinSet.Trim().ToLowerInvariant().StartsWith("test");
                if (useTest)
                {
                    // 课程设计 test.xlsx：3 列特征（水灰比/粉煤灰/硅灰，Z-Score 标准化）+ 抗压强度(MPa)
                    var demoTest = new Dataset
                    {
                        Name = "ConcreteDemo_TEST_16x4",
                        X = new double[][] {
                    new double[]{1.226110278247664,-0.9009732954600966,-1.00168372800325},
                    new double[]{1.380160075088467,-1.3591156801326,-1.360688134423001},
                    new double[]{-1.395511940061145,1.390838609618319,1.411546664185121},
                    new double[]{0.09502393207419597,-0.6969266871605787,1.052542257765369},
                    new double[]{-0.007675932486339105,0.01586146474285957,1.411546664185121},
                    new double[]{-0.007675932486339105,0.2193580821844274,0.2305746399780608},
                    new double[]{1.380160075088467,-1.3591156801326,1.411546664185121},
                    new double[]{-0.4184753907284818,-1.206218221622449,0.7448241951198675},
                    new double[]{-0.007675932486339105,0.01586146474285957,-1.360688134423001},
                    new double[]{-1.395511940061145,-1.3591156801326,0.02542926488105988},
                    new double[]{0.6085232548768745,-1.002721604180881,-0.3848614853129422},
                    new double[]{1.123410413687127,1.237941151108168,-0.1797161102159412},
                    new double[]{1.380160075088467,1.390838609618319,0.2735442793564868},
                    new double[]{-1.138762278659806,0.7286496166462977,-1.309401790648751},
                    new double[]{0.1380468483090156,-1.3591156801326,0.16542712221077},
                    new double[]{-0.7279628204176632,0.3216563817631616,-0.8991110404547488}
                        },
                        Y = new double[][] {
                    new double[]{22.384},
                    new double[]{19.96},
                    new double[]{18.092},
                    new double[]{27.872},
                    new double[]{20.684},
                    new double[]{22.664},
                    new double[]{19.844},
                    new double[]{33.456},
                    new double[]{22.024},
                    new double[]{43.28},
                    new double[]{25.856},
                    new double[]{8.38},
                    new double[]{5.636},
                    new double[]{25.52},
                    new double[]{33.94},
                    new double[]{23.292}
                        }
                    };
                    idd.SetDataItem(0, demoTest);
                    LogMessage(BuildLogLevel.Warning,
                        "InputDataCard: 未填写路径，已加载内置【测试集】demo 数据（课程设计 test.xlsx，16 样本×3 特征，Z-Score 标准化）。");
                    RenderDistribution(idd, demoTest);
                    return;
                }

                // 课程设计 train.xlsx 前 3 列特征（水灰比/粉煤灰/硅灰，已 Z-Score 标准化），最后一列抗压强度(MPa)
                var demo = new Dataset
                {
                    Name = "ConcreteDemo_66x4",
                    X = new double[][] {
                    new double[]{0.50027205, -0.50112994, -0.69812402},
                    new double[]{-1.3955119, -0.9911718, -1.3565298},
                    new double[]{-1.3955119, 1.3908386, 0.78086325},
                    new double[]{0.91801068, 0.83039793, 1.1551149},
                    new double[]{1.3801601, -1.3591157, 0.025429265},
                    new double[]{0.38646949, -0.14418587, 1.3727354},
                    new double[]{-1.3955119, 0.015861465, -1.3606881},
                    new double[]{-0.31577553, -0.39168176, 1.2576876},
                    new double[]{-0.1103758, 0.62690131, -0.2822888},
                    new double[]{1.1483915, 0.86889729, 1.4115467},
                    new double[]{-0.83066268, -0.59517838, 0.94996957},
                    new double[]{-0.043759669, 1.3908386, -1.3606881},
                    new double[]{-1.3955119, 1.3908386, 0.025429265},
                    new double[]{-0.93336255, -1.3079665, 0.025429265},
                    new double[]{-0.17005275, -1.3591157, -1.3606881},
                    new double[]{-0.77376141, 0.44375435, 1.4115467},
                    new double[]{-0.0076759325, 1.3908386, -1.3606881},
                    new double[]{0, 0, 1.9236243e-16},
                    new double[]{1.3593425, 1.3908386, -1.1957402},
                    new double[]{-1.3955119, 0.015861465, 1.4115467},
                    new double[]{-1.3955119, 0.051610871, -0.62743203},
                    new double[]{-0.0076759325, -1.3591157, 0.025429265},
                    new double[]{0.30042366, -0.28993345, -1.2068291},
                    new double[]{-1.3955119, -1.3591157, 1.4115467},
                    new double[]{0.10335095, 1.3908386, 0.7531409},
                    new double[]{1.3801601, 0.015861465, -1.3606881},
                    new double[]{1.3801601, 0.015861465, 1.4115467},
                    new double[]{-1.0360624, 1.3396895, 0.43572002},
                    new double[]{-1.3955119, 1.3908386, -1.3606881},
                    new double[]{-0.12286632, -1.3030166, 1.4115467},
                    new double[]{1.3801601, 1.3908386, -1.3606881},
                    new double[]{1.0207105, -0.49343007, -0.79653835},
                    new double[]{-0.7002061, 1.0877936, -0.27813045},
                    new double[]{-1.3955119, -1.3591157, 1.3436269},
                    new double[]{0.40312353, 0.015861465, -0.59000686},
                    new double[]{0.71261096, 1.1361928, -0.69396567},
                    new double[]{-1.3871849, -0.34493254, 0.58264846},
                    new double[]{-0.0076759325, 1.3908386, 0.025429265},
                    new double[]{1.3288101, 0.11760977, 0.33314733},
                    new double[]{-0.21307566, 1.0344445, 0.12800195},
                    new double[]{-1.3955119, 0.015861465, 0.025429265},
                    new double[]{-0.62387512, -0.18763515, -1.1042564},
                    new double[]{-0.0076759325, 0.015861465, 0.025429265},
                    new double[]{0.81531082, -1.1044699, 0.64086539},
                    new double[]{-0.20891215, 0.01311151, 0.25552475},
                    new double[]{1.3801601, -0.057287319, 0.50918424},
                    new double[]{-0.0076759325, -1.3591157, 1.4115467},
                    new double[]{-1.2414621, 0.42340469, 1.3602603},
                    new double[]{1.1705968, -1.3591157, -1.3606881},
                    new double[]{-1.3955119, 1.3908386, -1.3606881},
                    new double[]{1.3801601, 0.13740944, -1.3606881},
                    new double[]{-1.1123934, -1.3591157, -0.15337988},
                    new double[]{0.1977238, 0.93269622, -0.077143423},
                    new double[]{-1.344162, -0.798675, -0.48743417},
                    new double[]{-0.0076759325, -1.3591157, -1.3606881},
                    new double[]{0.50582339, -0.085886844, 0.84739688},
                    new double[]{-0.0076759325, 1.3908386, 1.4115467},
                    new double[]{-1.3955119, -1.3591157, -1.3606881},
                    new double[]{1.3801601, 0.015861465, 0.025429265},
                    new double[]{-0.35741061, 0.16545898, -1.3606881},
                    new double[]{0.56133683, 0.73964943, -0.450009},
                    new double[]{1.3801601, 1.3908386, 1.4115467},
                    new double[]{1.3801601, -1.2128181, -0.10763801},
                    new double[]{1.3801601, -1.3591157, 1.3865966},
                    new double[]{1.3801601, 1.3908386, 0.025429265},
                    new double[]{-0.52117526, 0.525153, 0.5382927}
                    },
                    Y = new double[][] {
                    new double[]{20.2},
                    new double[]{35.236},
                    new double[]{20.548},
                    new double[]{10.752},
                    new double[]{21.532},
                    new double[]{19.148},
                    new double[]{32.86},
                    new double[]{25.772},
                    new double[]{20.884},
                    new double[]{9.98},
                    new double[]{38.724},
                    new double[]{13.904},
                    new double[]{22.552},
                    new double[]{42.404},
                    new double[]{30.124},
                    new double[]{21.432},
                    new double[]{13.792},
                    new double[]{20.884},
                    new double[]{6.12},
                    new double[]{30.288},
                    new double[]{33.528},
                    new double[]{30.604},
                    new double[]{22.004},
                    new double[]{39.3088},
                    new double[]{11.752},
                    new double[]{12.28},
                    new double[]{14.524},
                    new double[]{18.892},
                    new double[]{20.652},
                    new double[]{27.9},
                    new double[]{7.612},
                    new double[]{23.124},
                    new double[]{19.564},
                    new double[]{40.376},
                    new double[]{21.892},
                    new double[]{11.94},
                    new double[]{32.516},
                    new double[]{12.268},
                    new double[]{13.832},
                    new double[]{17.712},
                    new double[]{33.96},
                    new double[]{30.464},
                    new double[]{21.392},
                    new double[]{24.42},
                    new double[]{21.96},
                    new double[]{14.536},
                    new double[]{29.128},
                    new double[]{29.128},
                    new double[]{17.384},
                    new double[]{20.324},
                    new double[]{11.968},
                    new double[]{41.028},
                    new double[]{15.08},
                    new double[]{45.66},
                    new double[]{32.868},
                    new double[]{19.924},
                    new double[]{11.464},
                    new double[]{45.616},
                    new double[]{14.568},
                    new double[]{20.18},
                    new double[]{16.632},
                    new double[]{6.008},
                    new double[]{19.948},
                    new double[]{19.688},
                    new double[]{5.632},
                    new double[]{20.44}
                    }
                };
                idd.SetDataItem(0, demo);
                LogMessage(BuildLogLevel.Warning,
                    "InputDataCard: 未填写路径，已加载内置【训练集】demo 数据（课程设计 train.xlsx，66 样本×3 特征，Z-Score 标准化）。" +
                    "如需测试集：把本卡的「内置数据集」端口填 test（或多拖一张输入数据卡，把该端口填 test）。");
                RenderDistribution(idd, demo);
                return;
            }

            // ---------- 情况二：按扩展名分发 ----------
            var ext = System.IO.Path.GetExtension(path)?.ToLowerInvariant();
            if (ext != ".csv" && ext != ".txt")
            {
                // 明确告知用户如何处理 Excel 文件（本项目已砍掉 Excel 解析以保持精简）
                idd.SetDataItem(0, new Dataset { Name = path, X = new double[0][], Y = new double[0][] });
                LogMessage(BuildLogLevel.Error,
                    $"InputDataCard: 暂不支持 '{ext}' 文件。请先用 Excel/WPS 将其另存为 CSV" +
                    $"（建议选“CSV UTF-8”格式）后再加载。支持 .csv/.txt。");
                return;
            }

            try
            {
                if (TryParseCsv(path, delimiter.Length > 0 ? delimiter[0] : ',', hasHeader, featureCols, labelCols,
                                out double[][] X, out double[][] Y, out string err))
                {
                    var ds = new Dataset { Name = path, X = X, Y = Y };
                    idd.SetDataItem(0, ds);
                    LogMessage(BuildLogLevel.Warning,
                        $"InputDataCard: 已加载 {System.IO.Path.GetFileName(path)}，样本数={X.Length}，" +
                        $"特征维={X[0].Length}，标签维={Y[0].Length}");
                    RenderDistribution(idd, ds);
                }
                else
                {
                    idd.SetDataItem(0, new Dataset { Name = path, X = new double[0][], Y = new double[0][] });
                    LogMessage(BuildLogLevel.Error, $"InputDataCard: CSV 解析失败: {err}");
                }
            }
            catch (Exception ex)
            {
                idd.SetDataItem(0, new Dataset { Name = path, X = new double[0][], Y = new double[0][] });
                LogMessage(BuildLogLevel.Error, $"InputDataCard: 读取数据时发生异常: {ex.Message}");
            }
        }

        // ==================== 核心解析逻辑 ====================

        /// <summary>
        /// 读取并解析 CSV 文件为特征矩阵 X 与标签矩阵 Y。
        /// 步骤：智能解码 → 逐行引号感知拆分 → 表头/数据分离 → 列选择 → 数值转换（带坏格警告）
        /// </summary>
        private bool TryParseCsv(string path, char delimiter, bool hasHeader,
                                 string featureCols, string labelCols,
                                 out double[][] X, out double[][] Y, out string error)
        {
            X = null; Y = null; error = null;
            try
            {
                // ---- 第 1 步：智能解码读入所有非空行 ----
                var lines = ReadNonEmptyLines(path, out string encodingUsed, out error);
                if (lines == null) return false;
                if (lines.Count == 0) { error = "文件为空"; return false; }

                // ---- 第 2 步：分离表头与数据行 ----
                int start = 0;
                string[] headers = null;
                if (hasHeader)
                {
                    headers = ParseCsvLine(lines[0], delimiter).ToArray(); // 第一行作为列名
                    start = 1;
                }
                var dataRows = new List<List<string>>();
                for (int i = start; i < lines.Count; i++)
                    dataRows.Add(ParseCsvLine(lines[i], delimiter));     // 引号感知地拆分每一行

                if (dataRows.Count == 0) { error = "无数据行"; return false; }
                int cols = dataRows[0].Count;

                // ---- 第 3 步：确定特征列 / 标签列的索引 ----
                var featureIdx = ResolveColumnIndices(featureCols, headers, cols);
                var labelIdx = ResolveColumnIndices(labelCols, headers, cols);
                if (featureIdx == null || featureIdx.Length == 0)
                {
                    // 默认规则：除最后一列外全部作为特征（本课设即前 3 列）
                    featureIdx = new int[cols - 1];
                    for (int i = 0; i < cols - 1; i++) featureIdx[i] = i;
                }
                if (labelIdx == null || labelIdx.Length == 0)
                {
                    // 默认规则：最后一列作为标签（本课设即抗压强度）
                    labelIdx = new int[] { cols - 1 };
                }

                // ---- 第 4 步：数值转换（记录无法解析的单元格，避免脏数据被静默吞掉）----
                var badCells = new List<string>(); // 坏格位置示例（最多记 5 个）
                int badTotal = 0;                  // 坏格总数
                int n = dataRows.Count;
                X = new double[n][];
                Y = new double[n][];
                for (int i = 0; i < n; i++)
                {
                    var row = dataRows[i];
                    var xv = new double[featureIdx.Length];
                    var yv = new double[labelIdx.Length];
                    for (int j = 0; j < featureIdx.Length; j++)
                        xv[j] = ParseCell(row, featureIdx[j], i + start + 1, badCells, ref badTotal);
                    for (int j = 0; j < labelIdx.Length; j++)
                        yv[j] = ParseCell(row, labelIdx[j], i + start + 1, badCells, ref badTotal);
                    X[i] = xv;
                    Y[i] = yv;
                }

                // 存在无法解析的单元格时给出警告（不中断流程，按 0 处理）
                if (badTotal > 0)
                {
                    string samples = string.Join("；", badCells);
                    string more = badTotal > badCells.Count ? $" 等（仅列出前{badCells.Count}处）" : "";
                    LogMessage(BuildLogLevel.Warning,
                        $"InputDataCard: 共 {badTotal} 个单元格无法解析为数值，已按 0 处理：{samples}{more}");
                }

                LogMessage(BuildLogLevel.Warning, $"InputDataCard: 文件编码识别为 {encodingUsed}。");
                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        /// <summary>
        /// 智能解码读取文本的所有非空行：
        /// 先尝试严格 UTF-8 解码；若字节流不是合法 UTF-8（中文 Windows 的 ANSI/GB 系编码会失败），
        /// 则回退到 GB18030 解码，从根源上避免中文乱码。
        /// </summary>
        private List<string> ReadNonEmptyLines(string path, out string encodingUsed, out string error)
        {
            encodingUsed = null; error = null;
            try
            {
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                string text;
                try
                {
                    text = new UTF8Encoding(false, true).GetString(bytes); // 严格 UTF-8：遇到非法字节直接抛异常
                    encodingUsed = "UTF-8";
                }
                catch (DecoderFallbackException)
                {
                    text = Encoding.GetEncoding("GB18030").GetString(bytes); // 回退：简体中文 Windows 常见编码
                    encodingUsed = "GB18030";
                }
                if (text.Length > 0 && text[0] == '\uFEFF') text = text.Substring(1); // 去 BOM 字符

                var lines = new List<string>();
                using (var sr = new System.IO.StringReader(text))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                        if (!string.IsNullOrWhiteSpace(line)) lines.Add(line); // 跳过空行
                }
                return lines;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return null;
            }
        }

        /// <summary>
        /// 引号感知的 CSV 行拆分器：
        /// - 双引号包裹的字段内可以包含分隔符或换行转义；
        /// - 两个连续双引号 "" 表示字面意义上的一个双引号。
        /// （限制：不支持跨行的引号字段——课程数据不涉及该场景）
        /// </summary>
        private static List<string> ParseCsvLine(string line, char delimiter)
        {
            var fields = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // 连续两个双引号 => 字面双引号；单个双引号 => 结束引号区
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == '"') inQuotes = true;              // 进入引号区
                    else if (c == delimiter) { fields.Add(sb.ToString().Trim()); sb.Clear(); } // 字段结束
                    else sb.Append(c);
                }
            }
            fields.Add(sb.ToString().Trim()); // 最后一个字段
            return fields;
        }

        /// <summary>安全地把某一行某一列转换为 double；失败时记入 badCells 并返回 0。</summary>
        private static double ParseCell(List<string> row, int idx, int rowNo, List<string> badCells, ref int badTotal)
        {
            if (idx < 0 || idx >= row.Count)
            {
                badTotal++;
                if (badCells.Count < 5) badCells.Add($"第{rowNo}行第{idx + 1}列(缺列)");
                return 0.0;
            }
            string s = row[idx];
            if (double.TryParse(s, System.Globalization.NumberStyles.Any,
                                System.Globalization.CultureInfo.InvariantCulture, out double v))
                return v;
            if (double.TryParse(s, out v)) return v; // 回退：本地化数字格式（如逗号小数点）

            badTotal++;
            if (badCells.Count < 5) badCells.Add($"第{rowNo}行第{idx + 1}列'{s}'");
            return 0.0;
        }

        /// <summary>
        /// 把列描述（"水灰比,粉煤灰" 或 "0,2" 或混写）解析成列索引数组；
        /// 输入为空时返回 null（表示采用默认规则）。
        /// </summary>
        private static int[] ResolveColumnIndices(string spec, string[] headers, int cols)
        {
            if (string.IsNullOrWhiteSpace(spec)) return null;
            var parts = spec.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            var list = new List<int>();
            foreach (var p in parts)
            {
                var t = p.Trim();
                // 写的是数字 => 按 0 起始列索引理解
                if (int.TryParse(t, out int idx))
                {
                    if (idx >= 0 && idx < cols) list.Add(idx);
                }
                // 写的是文字 => 在表头里查找同名列（忽略大小写与首尾空白）
                else if (headers != null)
                {
                    int ix = Array.FindIndex(headers, h =>
                        string.Equals(h?.Trim(), t, StringComparison.OrdinalIgnoreCase));
                    if (ix >= 0) list.Add(ix);
                }
            }
            return list.ToArray();
        }

        /// <summary>
        /// 把数据集渲染为右侧三维视口的数据分布图：
        /// X 轴=特征1，Y 轴=特征2，柱高=标签（如抗压强度），并绘制坐标轴。
        /// 换数据集/换路径重跑时，三维点云实时变化（参数化联动）。
        /// </summary>
        private void RenderDistribution(IDataDelivery idd, Dataset ds)
        {
            if (ds == null || ds.X == null || ds.SampleCount == 0) return;

            NNVisualizer.Init(out var model, out var floor);
            NNVisualizer.BuildAxes(model, floor);

            int n = ds.SampleCount;
            int inDim = ds.X[0] != null ? ds.X[0].Length : 0;
            var xs = new double[n]; var ys = new double[n]; var zs = new double[n];
            double xMin = 0, xMax = 1, yMin = 0, yMax = 1, zMin = 0, zMax = 1;
            for (int i = 0; i < n; i++)
            {
                xs[i] = inDim > 0 ? ds.X[i][0] : 0;
                ys[i] = inDim > 1 ? ds.X[i][1] : 0;
                zs[i] = (ds.Y != null && ds.Y[i] != null && ds.Y[i].Length > 0) ? ds.Y[i][0] : 0;
                if (i == 0) { xMin = xMax = xs[i]; yMin = yMax = ys[i]; zMin = zMax = zs[i]; }
                else
                {
                    xMin = Math.Min(xMin, xs[i]); xMax = Math.Max(xMax, xs[i]);
                    yMin = Math.Min(yMin, ys[i]); yMax = Math.Max(yMax, ys[i]);
                    zMin = Math.Min(zMin, zs[i]); zMax = Math.Max(zMax, zs[i]);
                }
            }

            var nx = new List<double>(); var ny = new List<double>(); var nz = new List<double>();
            for (int i = 0; i < n; i++)
            {
                nx.Add(NNVisualizer.Norm(xs[i], xMin, xMax, 4500));
                ny.Add(NNVisualizer.Norm(ys[i], yMin, yMax, 3500));
                nz.Add(NNVisualizer.Norm(zs[i], zMin, zMax, 5000));
            }
            NNVisualizer.AddPointCloud(model, floor, nx, ny, nz);
            NNVisualizer.Render(previewController, model, s => LogMessage(BuildLogLevel.Warning, s));
            idd.SetDataItem(1, model);

            // 几何图元输出：彩色点云 + 坐标轴 + 轴标注 → 预览窗口自动渲染（MATLAB 风格）
            try { idd.SetDataList(2, NNVisualizer.BuildColoredPoints(nx, ny, nz, NNVisualizer.CPoint)); } catch { }
            try { idd.SetDataList(3, NNVisualizer.BuildColoredAxes()); } catch { }
            try { idd.SetDataList(4, NNVisualizer.BuildAxisLabels()); } catch { }

            // 2D 图表（主展示方案）：① 四联直方图（复刻课程设计 data.png）② 特征-强度散点（含趋势线）
            // 右键卡片 →「显示图表窗口」，或「导出全部图表 PNG」存到桌面\GAMA图表\
            double[][] Xmat = new double[n][];
            for (int i = 0; i < n; i++) Xmat[i] = ds.X[i];
            SetFigures("输入数据 · " + (string.IsNullOrEmpty(ds.Name) ? "数据集" : System.IO.Path.GetFileName(ds.Name)),
                FigureFactory.DatasetFigures(
                    (string.IsNullOrEmpty(ds.Name) ? "数据集" : System.IO.Path.GetFileName(ds.Name)),
                    new[] { "水灰比", "粉煤灰", "硅灰" }, Xmat, zs));
        }
    }
}
