# ChartPreview —— 图表离线自检工具

不启动 E-GAMA，直接用**与卡片完全相同**的绘图源码（`Cards\ChartMpl.cs`、`FigureFactory.cs`、`ReportTypes.cs`、
`SimpleNN.cs`、`ChartWindow.cs`）把全部图表渲染成 PNG，用来快速核查排版（刻度/图例遮挡/裁剪/配色），
改图时不必反复拖卡。

## 用法

```bash
cd tools\ChartPreview
dotnet build -c Debug
bin\Debug\FigTest.exe  你的标签名      # 例如 FigTest.exe v1
```

输出：`tools\ChartPreview\bin\Debug\out\<标签名>\*.png`（含课程设计全部图 + 结果对比 5 页 + 1 张 2 倍高清验证图）。
同时会在控制台打印 5 个模型（2 个 ANN + 3 个线性）的训练集/测试集指标，
可用来核对算法是否与课程设计报告一致（线性模型应为 0.9352 / 0.9495 / 0.9601）。

## 说明

- `DemoData.cs` 是课程设计 train/test 的原始数据（66 + 16 样本）；
- 修改卡片里的绘图代码后，本工具重新编译即可同步生效（csproj 用 `<Compile Include>` 直接链入 `Cards\*.cs`）；
- 这是开发辅助工具，不参与 `.crd` 部署。
