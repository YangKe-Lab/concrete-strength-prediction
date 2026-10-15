# Concrete Compressive Strength Prediction · 混凝土抗压强度预测

**[English](README.md) | [中文](README_zh.md)**

Predicting **concrete compressive strength** from mix proportions — a course-design project that compares **least-squares linear regression** (3 variants) against **hand-written backpropagation neural networks** (2 architectures). Every model is implemented from scratch with pure NumPy matrix operations — no scikit-learn, TensorFlow, or PyTorch.

> **Two implementations of one study:** the from-scratch NumPy pipeline below, and a **low-code E-GAMA card plug-in**
> that rebuilds the same experiment (plus a second, non-linear iris case) as drag-and-wire cards on a canvas —
> see [`gama-cards/`](gama-cards/README.md).

[![Python](https://img.shields.io/badge/Python-3.8+-blue)](https://www.python.org/)
[![NumPy](https://img.shields.io/badge/NumPy-from_scratch-orange)](https://numpy.org/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

---

## 📌 Project Overview

| Item | Detail |
|------|--------|
| **Task** | Predict concrete **28-day compressive strength** (MPa) |
| **Input features** | Water-cement ratio (水灰比), fly ash content (粉煤灰), silica fume content (硅灰) |
| **Target** | Compressive strength (抗压强度) |
| **Samples** | 81 valid records → 82 (after missing-value construction) |
| **Split** | 66 train / 16 test (~80:20, fixed seed 42) |
| **Models** | 3 least-squares linear models + 2 from-scratch ANNs |
| **Metrics** | R², MSE, MAE |

**⭐ Key result:** The single-hidden-layer ANN (**Model1**) achieves the best test-set performance — **R² = 0.9654, MSE = 2.6859, training time 0.28 s** — outperforming the best linear model (R² = 0.9616). ANN's nonlinear fitting captures the complex mix-proportion → strength relationship that linear models cannot.

---

## 📊 Dataset

The raw dataset (`data/data.xlsx`) contains **81 concrete mix samples** with 4 float64 variables and no initial missing values.

| Variable | Mean | Std | Range | Skewness | Distribution |
|----------|------|-----|-------|----------|--------------|
| 水灰比 (Water-cement ratio) | 0.4506 | 0.0725 | 0.35 ~ 0.55 | −0.0069 | Normal |
| 粉煤灰 (Fly ash) | 0.2471 | 0.1830 | 0.00 ~ 0.50 | 0.0099 | Normal |
| 硅灰 (Silica fume) | 0.0982 | 0.0726 | 0.00 ~ 0.20 | 0.0163 | Normal |
| 抗压强度 (Strength, MPa) | 22.6027 | 9.8035 | 5.63 ~ 45.66 | 0.4586 | Right-skewed |

<p align="center">
  <img src="results/data.png" alt="Feature distribution histograms" width="750">
  <br><em>Fig. 1 — Feature distribution histograms (water-cement ratio, fly ash, silica fume, compressive strength)</em>
</p>

---

## 🧹 Data Preprocessing (`src/preprocess.py`)

The pipeline consists of 4 steps:

### 1. Loading & EDA
`data.xlsx` is loaded via `pandas.read_excel()`, with `head()`/`describe()`/`info()` for a first look at data quality.

### 2. Missing-value construction & imputation
One all-NaN row is appended (`df.loc[81] = np.nan`) to simulate a missing sample, then imputed **by distribution type**:

| Variable | Skewness | Strategy | Fill value |
|----------|----------|----------|------------|
| 水灰比 | −0.0069 | **Mean** (normal) | 0.4506 |
| 粉煤灰 | 0.0099 | **Mean** (normal) | 0.2471 |
| 硅灰 | 0.0163 | **Mean** (normal) | 0.0982 |
| 抗压强度 | 0.4586 | **Median** (skewed) | 20.8840 |

### 3. Z-Score standardization
Only the 3 input features are standardized (`z = (x − μ) / σ`, mean→0, std→1) to remove scale differences; the target keeps its raw value.

### 4. Train/test split
`np.random.seed(42)` → shuffled indices → first 16 as test, remaining 66 as train, saved to `data/test.xlsx` and `data/train.xlsx`.

---

## 🧠 Models

### 1. Least-Squares Linear Models (`src/linear_model.py`)

Solved with the **normal equation** β = (XᵀX)⁻¹XᵀY, implemented purely with matrix operations:

| Model | Formula | Params |
|-------|---------|--------|
| **Model 1** — Simple linear | y = a + b·x₁ + c·x₂ + d·x₃ | 4 |
| **Model 2** — Feature interaction | M1 + e·x₁x₂ + f·x₁x₃ + g·x₂x₃ | 7 |
| **Model 3** — Polynomial | M2 + h·x₁² + i·x₂² + k·x₃² | 10 |

where x₁ = water-cement ratio, x₂ = fly ash, x₃ = silica fume, y = compressive strength.

<p align="center">
  <img src="results/LinearModel1.png" alt="Linear Model 1" width="720">
  <img src="results/LinearModel2.png" alt="Linear Model 2" width="720">
  <img src="results/LinearModel3.png" alt="Linear Model 3" width="720">
  <br><em>Fig. 2-4 — Predicted vs actual scatter plots of the three linear models (train left, test right)</em>
</p>

### 2. ANN Models (`src/ANN_model.py`)

Hand-written MLPs implementing **forward propagation → backpropagation → gradient descent** with:

- **He initialization** — `std = √(2/n_in)` for stable gradient flow
- **Leaky ReLU** (α = 0.01) — avoids dead neurons (plain ReLU made Model 2 fail to converge)
- **Identity output activation** — suited for regression
- Gradient descent, lr = 1×10⁻⁴, **10,000 epochs**, MSE loss

| Model | Architecture | Params |
|-------|--------------|--------|
| **ANN Model 1** — single hidden layer | 3 → 10 → 1 | 51 |
| **ANN Model 2** — double hidden layer | 3 → 5 → 5 → 1 | 56 |

<p align="center">
  <img src="results/ANN_Model1.png" alt="ANN Model 1" width="720">
  <img src="results/ANN_Model2.png" alt="ANN Model 2" width="720">
  <br><em>Fig. 5-6 — Predicted vs actual scatter plots of the ANN models</em>
</p>

<p align="center">
  <img src="results/ANN_Model1_Train_MSE.png" alt="ANN Model 1 training curve" width="720">
  <img src="results/ANN_Model2_Train_MSE.png" alt="ANN Model 2 training curve" width="720">
  <br><em>Fig. 7-8 — Training MSE curves over 10,000 epochs (Model 2 shows local fluctuations but converges)</em>
</p>

---

## 📈 Results

### Least-Squares Linear Models

| Model | Train R² | Test R² | Train MSE | Test MSE | Train MAE | Test MAE |
|-------|----------|---------|-----------|----------|-----------|----------|
| Model 1 (simple) | 0.9352 | 0.9443 | 6.3232 | 4.3224 | 1.9243 | 1.8112 |
| **Model 2 (interaction)** ⭐ | 0.9495 | **0.9616** | 4.9277 | **2.9796** | 1.6361 | **1.2708** |
| Model 3 (polynomial) | 0.9601 | 0.9591 | 3.8964 | 3.1790 | 1.4024 | 1.3795 |

- Interaction terms significantly improve both fit and generalization (Model 2 > Model 1)
- Model 3 fits the training set best but **slightly overfits** (test R² drops to 0.9591)
- **Model 2 is the best linear model** — good balance of complexity and generalization

### ANN Models

| Model | Train R² | Test R² | Train MSE | Test MSE | Train MAE | Test MAE | Time |
|-------|----------|---------|-----------|----------|-----------|----------|------|
| **Model 1 (3-10-1)** ⭐ | 0.9711 | **0.9654** | 2.8204 | **2.6859** | 1.2008 | 1.3201 | **0.28 s** |
| Model 2 (3-5-5-1) | 0.9722 | 0.9515 | 2.7161 | 3.7626 | 1.1193 | 1.5916 | 0.74 s |

- Model 1 beats every linear model on **both** train and test sets, training in just 0.28 s
- Model 2 fits training data best but **overfits** — test MSE rises to 3.7626; the deeper net gets stuck in local optima more easily

### Overall Comparison

| Model | Test R² | Test MSE | Test MAE |
|-------|---------|----------|----------|
| Best linear (Model 2) | 0.9616 | 2.9796 | 1.2708 |
| **Best ANN (Model 1)** ⭐ | **0.9654** | **2.6859** | 1.3201 |

### Conclusions

1. **Performance**: ANN is the overall winner — its nonlinear fitting captures the complex ingredient → strength relationship that linear models miss (train R² 0.9711 vs 0.9495, MSE −42.7%)
2. **Interpretability**: linear models remain valuable when isolating a single ingredient's effect
3. **Engineering value**: ANN Model 1 combines higher accuracy with 0.28 s training — practical for real-world mix design optimization

---

## 🧩 Low-code version — the same study as drag-and-wire cards

[`gama-cards/`](gama-cards/README.md) is a custom **card plug-in for E-GAMA** (C#, .NET Framework 4.7.2)
that rebuilds this whole experiment on a canvas: load data → dense layers → activations → train → predict →
evaluate → compare, with **no code at all**. The network core is written from scratch the same way as
[`src/ANN_model.py`](src), so the two implementations cross-check each other:

| Model | NumPy (this repo) | E-GAMA cards (C#) |
|-------|-------------------|-------------------|
| Linear · simple | 0.9352 / 0.9443 | 0.9352 / 0.9443 |
| Linear · interaction | 0.9495 / **0.9616** | 0.9495 / **0.9616** |
| Linear · poly | 0.9601 / 0.9591 | 0.9601 / 0.9591 |
| ANN 3-10-1 | 0.9711 / 0.9654 | 0.9675 / 0.9426 |
| ANN 3-5-5-1 | 0.9722 / 0.9515 | 0.9637 / 0.9300 |

*(train R² / test R². The three linear models agree **digit for digit** — the cross-check passes.
The two ANNs differ because C# and NumPy draw different He-initialisation random streams; see the
stability note below.)*

<p align="center">
  <img src="gama-cards/cases/01-concrete-strength-prediction/figures/结果对比___混凝土抗压强度预测对比_1_精度总览.png" alt="Five-model accuracy overview" width="820">
  <br><em>Fig. 9 — one-click five-model comparison generated by the 结果对比 card (accuracy overview page)</em>
</p>

### On the ANN result — nine initialisations instead of one

`src/ANN_model.py` does not seed its He initialisation, so any single ANN number — **including the
0.9654 in the key result above — is one draw**, not the network's expected performance. Re-training the
same network with 9 different initialisations gives test R² = 0.9201…0.9570 (mean **0.9481 ± 0.0119**),
and **none of the 9 runs reaches the best linear model's 0.9616**:

<p align="center">
  <img src="gama-cards/cases/02-iris-classification/figures/9次重复实验.png" alt="9-initialisation repeat study" width="700">
  <br><em>Fig. 10 — 9 initialisations of ANN Model 1 vs the best linear model (0.9616 reference line)</em>
</p>

The reading that survives repetition: on this 66/16 split the ANN and the interaction linear model are
**statistically indistinguishable**; the ANN's nonlinear capacity is real (its training-set R² is
consistently higher) but 16 test samples cannot establish a generalisation advantage.
The second case in `gama-cards/` makes the complementary point — on a genuinely non-linear task the ANN
does win, which is why "the task decides the model" is the honest conclusion:

<p align="center">
  <img src="gama-cards/cases/02-iris-classification/figures/鸢尾花_决策边界对比.png" alt="Iris decision boundary" width="720">
  <br><em>Fig. 11 — iris (versicolor vs virginica, petal length × width): straight line ≈ 94 %, ANN curved boundary ≈ 98 %</em>
</p>

---

## 📁 Repository Structure

```
concrete-strength-prediction/
├── src/
│   ├── preprocess.py        # Data loading, imputation, Z-Score, train/test split
│   ├── linear_model.py      # 3 least-squares linear models (pure matrix ops)
│   └── ANN_model.py         # 2 from-scratch ANNs (forward/backprop/gradient descent)
├── data/
│   ├── data.xlsx            # Raw dataset (81 samples)
│   ├── good_data.xlsx       # Preprocessed dataset
│   ├── train.xlsx           # Training set (66 samples)
│   └── test.xlsx            # Test set (16 samples)
├── results/                 # Generated figures (1200 dpi)
│   ├── data.png             #   Feature distribution histograms
│   ├── LinearModel1-3.png   #   Linear model fit scatter plots
│   ├── ANN_Model1/2.png     #   ANN fit scatter plots
│   └── ANN_Model*_Train_MSE.png  # Training MSE curves
├── gama-cards/              # Low-code E-GAMA card plug-in (C#): the same study as drag-and-wire cards
│   ├── GAMAcard/            #   cards + headless figure renderer + .pgz tool + manual + 12-page deck
│   ├── cases/               #   01 concrete strength (same 66/16 split) · 02 iris classification
│   └── ppt/                 #   two-cases talk deck (self-contained HTML)
├── docs/                    # (not included — course design report, private)
├── requirements.txt
├── LICENSE
└── README.md / README_zh.md
```

---

## 🚀 Quick Start

```bash
# 1. Install dependencies (conda or venv recommended)
pip install -r requirements.txt

# 2. Preprocess data → regenerates data/train.xlsx & data/test.xlsx
python src/preprocess.py

# 3. Train & evaluate the 3 linear models (saves results/*.png)
python src/linear_model.py

# 4. Train & evaluate the 2 ANN models (~1 s total on a laptop CPU)
python src/ANN_model.py
```

> Scripts resolve `data/` and `results/` paths relative to their own location, so they run from any working directory.

> **No-code route:** open [`gama-cards/cases/01-concrete-strength-prediction/case/抗压强度预测.pgz`](gama-cards/cases/01-concrete-strength-prediction/case) in E-GAMA and run the 结果对比 card — build/deploy steps and the card list are in [`gama-cards/README.md`](gama-cards/README.md).

---

## 📦 Dependencies

| Package | Purpose |
|---------|---------|
| pandas | Data loading (Excel) & manipulation |
| numpy | Matrix operations (models are pure NumPy) |
| matplotlib | Visualization |
| scipy | Statistics (skewness) |
| openpyxl | Excel file I/O |

---

## 📄 中文版

完整中文版说明见 [README_zh.md](README_zh.md)。

> The course design report (full analysis) is **not included** in this repository for privacy reasons; contact the author for academic purposes.

---

## License

[MIT](LICENSE) © 2026 Ke Yang (杨珂)
