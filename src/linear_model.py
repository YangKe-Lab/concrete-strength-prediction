# -*- coding: utf-8 -*-
# @Time    : 2025/12/29 21:33
# @Author  : 杨珂
# @File    : linear_model.py
# @Software: PyCharm
# @Desc    : 基于最小二乘法的线性模型构建与评估
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import os

# 路径配置（基于脚本所在目录，兼容不同启动方式）
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, '..', 'data')
RESULT_DIR = os.path.join(BASE_DIR, '..', 'results')

'''
1、简单线性公式
y = a + b*x1 + c*x2 + d*x3
底层数学原理实现
'''
#准备数据
#训练集
train_df = pd.read_excel(os.path.join(DATA_DIR, 'train.xlsx'))
x1_train1 = train_df['水灰比']
x2_train1 = train_df['粉煤灰']
x3_train1 = train_df['硅灰']
y_train1 = train_df['抗压强度']
m1 = len(y_train1)
#测试集
test_df = pd.read_excel(os.path.join(DATA_DIR, 'test.xlsx'))
x1_test1 = test_df['水灰比']
x2_test1 = test_df['粉煤灰']
x3_test1 = test_df['硅灰']
y_test1 = test_df['抗压强度']
n1 = len(y_test1)
#矩阵计算求解系数矩阵β=(X^T*X)^-1 * X^T * Y
X1 = np.column_stack((np.ones(m1), x1_train1, x2_train1, x3_train1))#4X66的矩阵
XT_X1 = np.dot(X1.T, X1)#X^T*X
XT_Y1 = np.dot(X1.T, y_train1)#X^T*Y
XT_X_inv1 = np.linalg.inv(XT_X1)#求逆矩阵
beta1 = np.dot(XT_X_inv1, XT_Y1)#系数矩阵β
#计算决定系数R^2、平均绝对误差MAE、均方误差MSE
#训练集
#R^2计算
y_mean1 = np.mean(y_train1)
TSS_train1 = np.sum((y_train1 - y_mean1) ** 2)#train集总平方和
y_pred_train1 = np.dot(X1, beta1)
RSS_train1 = np.sum((y_train1 - y_pred_train1) ** 2)#train集残差平方和
R_train1 = 1 - (RSS_train1 / TSS_train1)
#MSE、MAE计算
MSE_train1 = np.mean((y_train1 - y_pred_train1) ** 2)
MAE_train1 = np.mean(np.abs(y_train1 - y_pred_train1))
#测试集
#R^2计算
y_mean_test1 = np.mean(y_test1)
TSS_test1 = np.sum((y_test1 - y_mean_test1) ** 2)#test集总平方和
X_test1 = np.column_stack((np.ones(n1), x1_test1, x2_test1, x3_test1))
y_pred_test1 = np.dot(X_test1, beta1)
RSS_test1 = np.sum((y_test1 - y_pred_test1) ** 2)#test集残差平方和
R_test1 = 1 - (RSS_test1 / TSS_test1)
#MSE、MAE计算
MSE_test1 = np.mean((y_test1 - y_pred_test1) ** 2)
MAE_test1 = np.mean(np.abs(y_test1 - y_pred_test1))
#结果输出与可视化
plt.figure(figsize=(10, 5))
#train集可视化
plt.subplot(1, 2, 1)
plt.scatter(y_train1, y_pred_train1, color='blue', label='Predicted vs Actual')
plt.plot([y_train1.min(), y_train1.max()], [y_train1.min(), y_train1.max()], 'r--', label='Ideal Fit (y=x)')
plt.title(f'Model1 Training: R^2 = {R_train1:.4f}')
plt.xlabel('Actual Compressive Strength')
plt.ylabel('Predicted Compressive Strength')
plt.legend()
#test集可视化
plt.subplot(1, 2, 2)
plt.scatter(y_test1, y_pred_test1, color='green', label='Predicted vs Actual')
plt.plot([y_test1.min(), y_test1.max()], [y_test1.min(), y_test1.max()], 'r--', label='Ideal Fit (y=x)')
plt.title(f'Model1 Testing: R^2 = {R_test1:.4f}')
plt.xlabel('Actual Compressive Strength')
plt.ylabel('Predicted Compressive Strength')
plt.legend()
plt.tight_layout()
plt.savefig(os.path.join(RESULT_DIR, 'LinearModel1.png'), dpi=1200, bbox_inches='tight', pad_inches=0.1, facecolor='white')
plt.show()
#输出R^2、MSE、MAE
print(f'Model1 Training: R^2 = {R_train1:.4f}, MSE = {MSE_train1:.4f}, MAE = {MAE_train1:.4f}')
print(f'Model1 Testing: R^2 = {R_test1:.4f}, MSE = {MSE_test1:.4f}, MAE = {MAE_test1:.4f}')


'''
2.考虑各组分之间的影响
y = a + b*x1 + c*x2 + d*x3 + e*x1*x2 + f*x1*x3 + g*x2*x3
底层数学原理实现
'''
#准备数据
#训练集
x1_train2 = x1_train1
x2_train2 = x2_train1
x3_train2 = x3_train1
x4_train2 = x1_train1 * x2_train1
x5_train2 = x1_train1 * x3_train1
x6_train2 = x2_train1 * x3_train1
y_train2 = y_train1
m2 = len(y_train2)
#测试集
x1_test2 = x1_test1
x2_test2 = x2_test1
x3_test2 = x3_test1
x4_test2 = x1_test1 * x2_test1
x5_test2 = x1_test1 * x3_test1
x6_test2 = x2_test1 * x3_test1
y_test2 = y_test1
n2 = len(y_test2)
#矩阵计算求解系数矩阵β=(X^T*X)^-1 * X^T * Y
X2 = np.column_stack((np.ones(m2), x1_train2, x2_train2, x3_train2, x4_train2, x5_train2, x6_train2))#7X66的矩阵
XT_X2 = np.dot(X2.T, X2)#X^T*X
XT_Y2 = np.dot(X2.T, y_train2)#X^T*Y
XT_X_inv2 = np.linalg.inv(XT_X2)#求逆矩阵
beta2 = np.dot(XT_X_inv2, XT_Y2)#系数矩阵β
#计算决定系数R^2、平均绝对误差MAE、均方误差MSE
#训练集
#R^2计算
y_mean2 = np.mean(y_train2)
TSS_train2 = np.sum((y_train2 - y_mean2) ** 2)#train集总平方和
y_pred_train2 = np.dot(X2, beta2)
RSS_train2 = np.sum((y_train2 - y_pred_train2) ** 2)#train集残差平方和
R_train2 = 1 - (RSS_train2 / TSS_train2)
#MSE、MAE计算
MSE_train2 = np.mean((y_train2 - y_pred_train2) ** 2)
MAE_train2 = np.mean(np.abs(y_train2 - y_pred_train2))
#测试集
#R^2计算
y_mean_test2 = np.mean(y_test2)
TSS_test2 = np.sum((y_test2 - y_mean_test2) ** 2)#test集总平方和
X_test2 = np.column_stack((np.ones(n2), x1_test2, x2_test2, x3_test2, x4_test2, x5_test2, x6_test2))
y_pred_test2 = np.dot(X_test2, beta2)
RSS_test2 = np.sum((y_test2 - y_pred_test2) ** 2)#test集残差平方和
R_test2 = 1 - (RSS_test2 / TSS_test2)
#MSE、MAE计算
MSE_test2 = np.mean((y_test2 - y_pred_test2) ** 2)
MAE_test2 = np.mean(np.abs(y_test2 - y_pred_test2))
#结果输出与可视化
plt.figure(figsize=(10, 5))
#train集可视化
plt.subplot(1, 2, 1)
plt.scatter(y_train2, y_pred_train2, color='blue', label='Predicted vs Actual')
plt.plot([y_train2.min(), y_train2.max()], [y_train2.min(), y_train2.max()], 'r--', label='Ideal Fit (y=x)')
plt.title(f'Model2 Training: R^2 = {R_train2:.4f}')
plt.xlabel('Actual Compressive Strength')
plt.ylabel('Predicted Compressive Strength')
plt.legend()
#test集可视化
plt.subplot(1, 2, 2)
plt.scatter(y_test2, y_pred_test2, color='green', label='Predicted vs Actual')
plt.plot([y_test2.min(), y_test2.max()], [y_test2.min(), y_test2.max()], 'r--', label='Ideal Fit (y=x)')
plt.title(f'Model2 Testing: R^2 = {R_test2:.4f}')
plt.xlabel('Actual Compressive Strength')
plt.ylabel('Predicted Compressive Strength')
plt.legend()
plt.tight_layout()
plt.savefig(os.path.join(RESULT_DIR, 'LinearModel2.png'), dpi=1200, bbox_inches='tight', pad_inches=0.1, facecolor='white')
plt.show()
#输出R^2、MSE、MAE
print(f'Model2 Training: R^2 = {R_train2:.4f}, MSE = {MSE_train2:.4f}, MAE = {MAE_train2:.4f}')
print(f'Model2 Testing: R^2 = {R_test2:.4f}, MSE = {MSE_test2:.4f}, MAE = {MAE_test2:.4f}')


'''
3.考虑多项式
y = a + b*x1 + c*x2 + d*x3 + e*x1*x2 + f*x1*x3 + g*x2*x3 + h*x1^2 + i*x2^2 + k*x3^2
底层数学原理实现
'''
#准备数据
#训练集
x1_train3 = x1_train1
x2_train3 = x2_train1
x3_train3 = x3_train1
x4_train3 = x1_train1 * x2_train1
x5_train3 = x1_train1 * x3_train1
x6_train3 = x2_train1 * x3_train1
x7_train3 = x1_train1 ** 2
x8_train3 = x2_train1 ** 2
x9_train3 = x3_train1 ** 2
y_train3 = y_train1
m3 = len(y_train3)
#测试集
x1_test3 = x1_test1
x2_test3 = x2_test1
x3_test3 = x3_test1
x4_test3 = x1_test1 * x2_test1
x5_test3 = x1_test1 * x3_test1
x6_test3 = x2_test1 * x3_test1
x7_test3 = x1_test1 ** 2
x8_test3 = x2_test1 ** 2
x9_test3 = x3_test1 ** 2
y_test3 = y_test1
n3 = len(y_test3)
#矩阵计算求解系数矩阵β=(X^T*X)^-1 * X^T * Y
X3 = np.column_stack((np.ones(m3), x1_train3, x2_train3, x3_train3, x4_train3, x5_train3, x6_train3, x7_train3, x8_train3, x9_train3))#10X66的矩阵
XT_X3 = np.dot(X3.T, X3)#X^T*X
XT_Y3 = np.dot(X3.T, y_train3)#X^T*Y
XT_X_inv3 = np.linalg.inv(XT_X3)#求逆矩阵
beta3 = np.dot(XT_X_inv3, XT_Y3)#系数矩阵β
#计算决定系数R^2、平均绝对误差MAE、均方误差MSE
#训练集
#R^2计算
y_mean3 = np.mean(y_train3)
TSS_train3 = np.sum((y_train3 - y_mean3) ** 2)#train集总平方和
y_pred_train3 = np.dot(X3, beta3)
RSS_train3 = np.sum((y_train3 - y_pred_train3) ** 2)#train集残差平方和
R_train3 = 1 - (RSS_train3 / TSS_train3)
#MSE、MAE计算
MSE_train3 = np.mean((y_train3 - y_pred_train3) ** 2)
MAE_train3 = np.mean(np.abs(y_train3 - y_pred_train3))
#测试集
#R^2计算
y_mean_test3 = np.mean(y_test3)
TSS_test3 = np.sum((y_test3 - y_mean_test3) ** 2)#test集总平方和
X_test3 = np.column_stack((np.ones(n3), x1_test3, x2_test3, x3_test3, x4_test3, x5_test3, x6_test3, x7_test3, x8_test3, x9_test3))
y_pred_test3 = np.dot(X_test3, beta3)
RSS_test3 = np.sum((y_test3 - y_pred_test3) ** 2)#test集残差平方和
R_test3 = 1 - (RSS_test3 / TSS_test3)
#MSE、MAE计算
MSE_test3 = np.mean((y_test3 - y_pred_test3) ** 2)
MAE_test3 = np.mean(np.abs(y_test3 - y_pred_test3))
#结果输出与可视化
plt.figure(figsize=(10, 5))
#train集可视化
plt.subplot(1, 2, 1)
plt.scatter(y_train3, y_pred_train3, color='blue', label='Predicted vs Actual')
plt.plot([y_train3.min(), y_train3.max()], [y_train3.min(), y_train3.max()], 'r--', label='Ideal Fit (y=x)')
plt.title(f'Model3 Training: R^2 = {R_train3:.4f}')
plt.xlabel('Actual Compressive Strength')
plt.ylabel('Predicted Compressive Strength')
plt.legend()
#test集可视化
plt.subplot(1, 2, 2)
plt.scatter(y_test3, y_pred_test3, color='green', label='Predicted vs Actual')
plt.plot([y_test3.min(), y_test3.max()], [y_test3.min(), y_test3.max()], 'r--', label='Ideal Fit (y=x)')
plt.title(f'Model3 Testing: R^2 = {R_test3:.4f}')
plt.xlabel('Actual Compressive Strength')
plt.ylabel('Predicted Compressive Strength')
plt.legend()
plt.tight_layout()
plt.savefig(os.path.join(RESULT_DIR, 'LinearModel3.png'), dpi=1200, bbox_inches='tight', pad_inches=0.1, facecolor='white')
plt.show()
#输出R^2、MSE、MAE
print(f'Model3 Training: R^2 = {R_train3:.4f}, MSE = {MSE_train3:.4f}, MAE = {MAE_train3:.4f}')
print(f'Model3 Testing: R^2 = {R_test3:.4f}, MSE = {MSE_test3:.4f}, MAE = {MAE_test3:.4f}')

#基于最小二乘法的线性模型构建与评估完成！！！！！！！！！！