# E-GAMA neural-network cards — the same study as drag-and-wire cards

> Part of the [`concrete-strength-prediction`](../README.md) repository — this folder is the *low-code*
> counterpart of the NumPy study in [`../src/`](../src).
> 中文文档：[`README_zh.md`](README_zh.md)

A custom **card plug-in for E-GAMA** (a Chinese structural-engineering teaching/CAD platform,
`ProjectPenguin` framework, .NET Framework 4.7.2). The plug-in adds a complete *drag-and-wire*
deep-learning toolchain — data loading, dense layers, activations, training, inference, evaluation and
multi-model comparison with publication-quality figures — so a structural engineer can build and train a
BP neural network on a canvas **without writing a single line of code**.

The C# network core is written from scratch exactly like [`../src/ANN_model.py`](../src):
He init, Leaky-ReLU hidden layers, identity output, MSE loss, full-batch gradient descent,
lr = 1e-4 / 10 000 epochs. The three least-squares models reproduce the reference numbers
**digit for digit** (train R² = 0.9352 / 0.9495 / 0.9601) — the cross-check that says the card
implementation is faithful to the Python study.

Two runnable cases ship with it:

| Case | Task | Models | Data |
|---|---|---|---|
| **01 · concrete compressive strength** | regression (3 mix-design features → strength) | 2 ANN (3-10-1, 3-5-5-1) + 3 linear (simple / interaction / poly) | the same 66 train / 16 test split as [`../data/`](../data) |
| **02 · iris classification** | non-linear classification (linearly non-separable classes) | 2 ANN + 3 linear | stratified 120/30, plus a 2-feature binary subset |

---

## Layout

```
gama-cards/
├── GAMAcard/                     the C# card plug-in (VS 2019/2022, .NET Framework 4.7.2)
│   ├── Cards/                    one .cs per card + algorithm core + plotting core
│   ├── tools/ChartPreview/       headless figure renderer (no GAMA dependency → self-test off-platform)
│   ├── tools/pgz_tool.py         offline .pgz reader/writer (dump cards & wiring, rewrite data paths)
│   ├── ppt/                      course-design talk deck (12 pages, self-contained HTML)
│   ├── 使用文档.md               full user manual: cards, ports, parameters, FAQ
│   ├── 连线方案.md               complete wiring recipe for the 5-model canvas + reference metrics
│   ├── 案例画布解析.md           deck analysis: card inventory, parameter values, wiring — read from the .pgz
│   └── 工程说明.md               build & debug instructions
├── cases/
│   ├── 01-concrete-strength-prediction/
│   │   ├── case/抗压强度预测.pgz  runnable card deck (drag into E-GAMA or File → Open)
│   │   ├── data/*.csv            train / test sets (same 66/16 split as ../data/)
│   │   ├── figures/*.png         22 exported figures (3200×1900)
│   │   └── README.md
│   └── 02-iris-classification/
│       ├── case/鸢尾花案例.pgz    second deck (same card topology, different data)
│       ├── data/*.csv            3-class and 2-feature binary subsets
│       ├── figures/*.png         decision-boundary comparison + 9-initialisation repeat study
│       └── README.md
└── ppt/                          two-cases talk deck (14 pages, self-contained HTML)
```

## The cards

| Card | Role | Key ports (in → out) |
|---|---|---|
| **输入数据** Input Data | load a CSV (UTF-8 / GB18030 auto-detect) or the built-in demo set | path, columns, label, delimiter, built-in set → `Dataset` |
| **全连接层** Dense Layer | declare one layer's unit count | units, bias, *upstream model* → layer metadata, **model** |
| **激活函数** Activation | per-layer activation: leaky_relu / relu / sigmoid / tanh / linear | activation type, upstream model → layer metadata, model |
| **模型构建** Model Builder | aggregate layer metadata by wire order | layer list → `Model` |
| **训练模型** Train | train the ANN (full-batch GD + MSE), optional test set, loss history | model, dataset(+test), epochs (10000), lr (1e-4), seed (42) → result, **model report** |
| **模型推理** Predict | single / batch / dataset inference | model, inputs → `double[][]` |
| **模型评估** Evaluate | R² / MSE / MAE on any prediction set | truth, prediction → 3 metrics, summary, report |
| **线性模型** Linear Model | closed-form least squares β=(XᵀX)⁻¹Xᵀy, `simple` / `interaction` / `poly` | train set, type, test set → coefficients, metrics, report |
| **结果对比** Compare | merge up to 5 model reports (**报告1…报告5**, a summary chain, or multi-wire) → 5 comparison pages | reports → summary + figures |
| **张量查看器** Tensor Viewer | one-line summary of any object (dataset / model / result / report) | object → text |
| **图表窗口** (right-click menu) | every visual card: 📊 show figure tabs · 💾 export all figures as 3200×1900 PNG | |

Plotting core: `ChartMpl.cs` (matplotlib-style framed axes, 1-2-5 ticks, scatter/line/bar/table/log axis,
automatic legend placement), `FigureFactory.cs` (results → figures, no GAMA types → testable off-platform),
`ChartWindow.cs` (GDI+ tabs + PNG export).

## Build & deploy

```bash
# requires: .NET Framework 4.7.2 target pack (VS 2019/2022 or the SDK), E-GAMA installed at D:\E-GAMA
cd gama-cards/GAMAcard
dotnet build -c Release          # or: dotnet build -c Release -t:Rebuild  (forces the post-build copy)
```

The post-build step copies the assembly to `%APPDATA%\nonstructure\Penguin\Cards\GAMAcard.crd`.
E-GAMA must be **closed** while copying (otherwise the file is locked) and **restarted** afterwards to pick
up the new card set; the cards then appear under the **深度学习 (Deep Learning)** category.

Reference DLLs (`ProjectPenguin`, `NSFramework`, `NSGeo3dm`, `NSStructurePreview`, `NSDataContainer`,
`NSGraphicManager`, `Newtonsoft.Json`) are referenced with `<Private>False</Private>` from `D:\E-GAMA`;
adjust the `HintPath`s if E-GAMA lives elsewhere.

## Run the cases

1. Open E-GAMA → **File → Open** (or drag it into the window)
   `cases/01-concrete-strength-prediction/case/抗压强度预测.pgz`.
2. Run the **结果对比** (Compare) card first — it pulls the whole upstream chain
   (2 data sets → 2 ANNs + 3 linear models → comparison).
3. Right-click any visual card → 📊 **显示图表窗口** to inspect the figures, or 💾 **导出全部图表 PNG**.

The concrete deck ships with **empty data paths**: both input cards fall back to the built-in demo data
(the same 66/16 samples as `cases/01-…/data/*.csv`, so the deck runs out of the box).
The iris deck keeps **relative paths** (`data\iris_train.csv`); if your GAMA does not resolve relative
paths, open the two 文字白板 (text) cards that feed the 输入数据 cards and paste the absolute path to
`cases/02-iris-classification/data/…`.

## Results measured on this build (seed 42)

**Case 01 — concrete strength**

| Model | Train R² | Test R² |
|---|---|---|
| Linear · simple | 0.9352 | 0.9443 |
| Linear · interaction | 0.9495 | **0.9616** |
| Linear · poly | 0.9601 | 0.9591 |
| ANN 3-10-1 | **0.9675** | 0.9426 |
| ANN 3-5-5-1 | 0.9637 | 0.9300 |

The linear rows match [`../results/`](../results) **digit for digit**. The ANNs do *not*: the C# cards and
the NumPy study use different He-initialisation RNGs, and the NumPy script does not seed it — so a single
ANN number is one draw from a distribution. Re-running the network with 9 initialisations gives test
R² = 0.9201…0.9570 (mean 0.9481 ± 0.0119) with **0/9 runs above the best linear model's 0.9616**
(see `cases/02-iris-classification/figures/9次重复实验.png`). Honest reading: on this 66/16 split the ANN's
advantage is not statistically established — the difference is smaller than the initialisation noise.

**Case 02 — iris, decision-boundary demo**: on the two-feature versicolor/virginica subset a straight line
cannot separate the overlapping region (linear ≈ **94 %**), while the ANN learns a curved boundary
(≈ **98 %**, stable over 5 seeds). Same toolchain, opposite conclusion → *the task decides the model.*

## Notes / limitations

* CSV only (Excel must be re-saved as CSV); MSE loss and full-batch GD are fixed by design; teaching-scale data.
* Gradient is summed (not averaged) per batch → use the default lr = 1e-4 with 10 000 epochs; larger values diverge.
* ANN results depend on initialisation (the reference NumPy script does not seed He init); the C# card is
  reproducible for a fixed seed.
* E-GAMA is commercial software and is **not** included; this folder contains only original card code,
  original decks/data/figures. Reference DLLs are referenced, never redistributed.
* The talk decks (`ppt/`) carry the author's name and affiliation on their title slides.
