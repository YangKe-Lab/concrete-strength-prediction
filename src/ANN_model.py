# -*- coding: utf-8 -*-
# @Time    : 2025/12/30 21:41
# @Author  : 杨珂
# @File    : ANN_model.py
# @Software: PyCharm
# @Desc    : 基于ANN神经网络的预测模型构建与评估
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
import time
import os

# 路径配置（基于脚本所在目录，兼容不同启动方式）
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, '..', 'data')
RESULT_DIR = os.path.join(BASE_DIR, '..', 'results')

'''
数据准备
'''
#使用矩阵存储数据方便计算
train = np.array(pd.read_excel(os.path.join(DATA_DIR, 'train.xlsx')))
test = np.array(pd.read_excel(os.path.join(DATA_DIR, 'test.xlsx')))
input_train = train[:, :3] #66X3
output_train = train[:, 3].reshape(-1, 1) #66X1
input_test = test[:, :3] #16X3
output_test = test[:, 3].reshape(-1, 1) #16X1

'''
基于ANN神经网络的混凝土抗压强度预测模型1
结构：输入层（3个神经元）-->隐藏层（8个神经元）-->输出层（1个神经元）
'''
#创建ANN_model1类
class ANN_model1:
    def __init__(self, ):
        #初始化参数
        self.input_size = 3 #输入层神经元个数
        self.hidden_size = 10 #隐含层神经元个数
        self.output_size = 1 #输出层神经元个数
        self.learning_rate = 0.0001 #学习率
        #He初始化确定权重
        he_std1 = np.sqrt(2 / self.input_size)
        self.w1 = np.random.randn(self.input_size, self.hidden_size) * he_std1
        he_std2 = np.sqrt(2 / self.hidden_size)
        self.w2 = np.random.randn(self.hidden_size, self.output_size) * he_std2
        #初始偏置设置为0
        self.b1 = np.zeros(shape=(self.hidden_size,)) #隐含层的偏置
        self.b2 = np.zeros(shape=(self.output_size,)) #输出层的偏置
        #存储训练过程的MSE值
        self.train_MSE = []

    def relu(self, x, alpha=0.01):
        #使用Leaky ReLU为激活函数，避免神经元死亡问题
        return np.maximum(alpha * x, x)

    def identity(self, x):
        #输出层激活函数，采用恒等激活函数，满足回归的需求
        return x

    def forward(self, x):
        #前向传播
        hidden_linear = np.dot(x, self.w1) + self.b1
        hidden_output = self.relu(hidden_linear)
        output_linear = np.dot(hidden_output, self.w2) + self.b2
        final_output = self.identity(output_linear)
        self.hidden_linear = hidden_linear
        self.hidden_output = hidden_output
        return final_output

    def backward(self, x, y_true, y_pred):
        #反向传播
        output_error = y_pred - y_true
        self.dw2 = np.dot(self.hidden_output.T, output_error)
        self.db2 = np.sum(output_error, axis=0)
        #Leaky ReLU：x>0时梯度为1，x<=0时梯度为alpha=0.01
        hidden_error = np.dot(output_error, self.w2.T) * np.where(self.hidden_linear > 0, 1, 0.01)
        self.dw1 = np.dot(x.T, hidden_error)
        self.db1 = np.sum(hidden_error, axis=0)

    def update(self):
        #梯度下降，调整权重和偏置
        self.w1 -= self.learning_rate * self.dw1
        self.w2 -= self.learning_rate * self.dw2
        self.b1 -= self.learning_rate * self.db1
        self.b2 -= self.learning_rate * self.db2

    def train(self, X_train, Y_train, epochs):
        #模型训练
        start_time = time.time()
        Y_train = Y_train.reshape(-1, 1)
        for epoch in range(1, epochs + 1):
            Y_pred = self.forward(X_train) #前向传播
            self.backward(X_train, Y_train, Y_pred) #反向传播
            self.update() #更新权重和偏置
            MSE = np.mean((Y_pred - Y_train) ** 2) #计算MSE
            self.train_MSE.append(MSE) #存储MSE
            if epoch % 100 == 0:
                print(f"第{epoch}轮训练 | 模型预测误差（MSE）：{MSE:.6f}")
        end_time = time.time()
        total_time = end_time - start_time
        print(f"训练完成！总耗时：{total_time:.2f}秒")
        plt.figure(figsize=(12,4))
        plt.plot(self.train_MSE, color='blue', linewidth=1.8, label='Training MSE')
        plt.xlabel('Epochs', fontsize=14)
        plt.ylabel('MSE', fontsize=14)
        plt.title('ANN Model1 Train MSE', fontsize=16)
        plt.grid(True, alpha=0.5, linestyle='--', color='#CCCCCC')
        plt.legend(loc='best', fontsize=12)
        plt.savefig(os.path.join(RESULT_DIR, 'ANN_Model1_Train_MSE.png'), dpi=1200, bbox_inches='tight', pad_inches=0.1, facecolor='white')
        plt.show()

    def predict(self, X_test):
        #模型预测
        return self.forward(X_test)


'''
基于神经网络的混凝土抗压强度预测模型2
结构：输入层（3个神经元）-->隐藏层1（5个神经元）-->隐藏层2（5个神经元）-->输出层（1个神经元）
'''
#创建ANN_model2类
class ANN_model2:
    def __init__(self, ):
        #初始化参数
        self.input_size = 3 #输入层神经元个数
        self.hidden_size1 = 5 #隐含层1神经元个数
        self.hidden_size2 = 5 #隐含层2神经元个数
        self.output_size = 1 #输出层神经元个数
        self.learning_rate = 0.0001 #学习率
        #He初始化确定权重
        he_std1 = np.sqrt(2 / self.input_size)
        self.w1 = np.random.randn(self.input_size, self.hidden_size1) * he_std1
        he_std2 = np.sqrt(2 / self.hidden_size1)
        self.w2 = np.random.randn(self.hidden_size1, self.hidden_size2) * he_std2
        he_std3 = np.sqrt(2 / self.hidden_size2)
        self.w3 = np.random.randn(self.hidden_size2, self.output_size) * he_std3
        #初始偏置设置为0
        self.b1 = np.zeros(shape=(self.hidden_size1,)) #隐含层1的偏置
        self.b2 = np.zeros(shape=(self.hidden_size2,)) #隐含层2的偏置
        self.b3 = np.zeros(shape=(self.output_size,)) #输出层的偏置
        #存储训练过程的MSE值
        self.train_MSE = []

    def relu(self, x, alpha=0.01):
        #激活函数采用Leaky ReLU，避免神经元死亡问题（模型2神经元死亡问题突出）
        return np.maximum(alpha * x, x)

    def identity(self, x):
        #输出层激活函数，采用恒等激活函数
        return x

    def forward(self, x):
        #前向传播
        hidden1_linear = np.dot(x, self.w1) + self.b1
        hidden1_output = self.relu(hidden1_linear)
        hidden2_linear = np.dot(hidden1_output, self.w2) + self.b2
        hidden2_output = self.relu(hidden2_linear)
        output_linear = np.dot(hidden2_output, self.w3) + self.b3
        final_output = self.identity(output_linear)
        self.hidden1_linear = hidden1_linear
        self.hidden1_output = hidden1_output
        self.hidden2_linear = hidden2_linear
        self.hidden2_output = hidden2_output
        return final_output

    def backward(self, x, y_true, y_pred):
        #反向传播
        output_error = y_pred - y_true
        self.dw3 = np.dot(self.hidden2_output.T, output_error)
        self.db3 = np.sum(output_error, axis=0)
        hidden2_error = np.dot(output_error, self.w3.T) * np.where(self.hidden2_linear > 0, 1, 0.01)
        self.dw2 = np.dot(self.hidden1_output.T, hidden2_error)
        self.db2 = np.sum(hidden2_error, axis=0)
        hidden1_error = np.dot(hidden2_error, self.w2.T) * np.where(self.hidden1_linear > 0, 1, 0.01)
        self.dw1 = np.dot(x.T, hidden1_error)
        self.db1 = np.sum(hidden1_error, axis=0)

    def update(self):
         #梯度下降，调整权重和偏置
          self.w1 -= self.learning_rate * self.dw1
          self.w2 -= self.learning_rate * self.dw2
          self.w3 -= self.learning_rate * self.dw3
          self.b1 -= self.learning_rate * self.db1
          self.b2 -= self.learning_rate * self.db2
          self.b3 -= self.learning_rate * self.db3

    def train(self, X_train, Y_train, epochs):
        #模型训练
        start_time = time.time()
        Y_train = Y_train.reshape(-1, 1)
        for epoch in range(1, epochs + 1):
            Y_pred = self.forward(X_train)  #前向传播
            self.backward(X_train, Y_train, Y_pred) #反向传播
            self.update()   #更新权重和怕偏置
            MSE = np.mean((Y_pred - Y_train) ** 2)  #计算MSE
            self.train_MSE.append(MSE)  #存储MSE
            if epoch % 100 == 0:
                print(f"第{epoch}轮训练 | 模型预测误差（MSE）：{MSE:.6f}")
        end_time = time.time()
        total_time = end_time - start_time
        print(f"训练完成！总耗时：{total_time:.2f}秒")
        plt.figure(figsize=(12,4))
        plt.plot(self.train_MSE, color='blue', linewidth=1.8, label='Training MSE')
        plt.xlabel('Epochs', fontsize=14)
        plt.ylabel('MSE', fontsize=14)
        plt.title('ANN Model2 Train MSE', fontsize=16)
        plt.grid(True, alpha=0.5, linestyle='--', color='#CCCCCC')
        plt.legend(loc='best', fontsize=12)
        plt.savefig(os.path.join(RESULT_DIR, 'ANN_Model2_Train_MSE.png'), dpi=1200, bbox_inches='tight', pad_inches=0.1, facecolor='white')
        plt.show()

    def predict(self, X_test):
        #模型预测
        return self.forward(X_test)


'''
ANN_model1结果输出与可视化
'''
#结果计算
model1 = ANN_model1()
model1.train(input_train, output_train, epochs=10000)
#训练集预测
y_pred_train1 = model1.predict(input_train)
#测试集预测
y_pred_test1 = model1.predict(input_test)
#计算R^2、MSE、MAE
#训练集
y_mean_train1 = np.mean(output_train)
TSS_train1 = np.sum((output_train - y_mean_train1) ** 2)
RSS_train1 = np.sum((output_train - y_pred_train1) ** 2)
R_train1 = 1 - (RSS_train1 / TSS_train1)
MSE_train1 = np.mean((output_train - y_pred_train1) ** 2)
MAE_train1 = np.mean(np.abs(output_train - y_pred_train1))
#测试集
y_mean_test1 = np.mean(output_test)
TSS_test1 = np.sum((output_test - y_mean_test1) ** 2)
RSS_test1 = np.sum((output_test - y_pred_test1) ** 2)
R_test1 = 1 - (RSS_test1 / TSS_test1)
MSE_test1 = np.mean((output_test - y_pred_test1) ** 2)
MAE_test1 = np.mean(np.abs(output_test - y_pred_test1))
#结果输出
print(f'ANN Model1 Training: R^2 = {R_train1:.4f}, MSE = {MSE_train1:.4f}, MAE = {MAE_train1:.4f}')
print(f'ANN Model1 Testing: R^2 = {R_test1:.4f}, MSE = {MSE_test1:.4f}, MAE = {MAE_test1:.4f}')
#可视化
plt.figure(figsize=(10, 5))
#train集可视化
plt.subplot(1, 2, 1)
plt.scatter(output_train, y_pred_train1, color='blue', label='Predicted vs Actual')
plt.plot([output_train.min(), output_train.max()], [output_train.min(), output_train.max()], 'r--', label='Ideal Fit (y=x)')
plt.title(f'ANN Model1 Training: R^2 = {R_train1:.4f}')
plt.xlabel('Actual Compressive Strength')
plt.ylabel('Predicted Compressive Strength')
plt.legend()
#test集可视化
plt.subplot(1, 2, 2)
plt.scatter(output_test, y_pred_test1, color='green', label='Predicted vs Actual')
plt.plot([output_test.min(), output_test.max()], [output_test.min(), output_test.max()], 'r--', label='Ideal Fit (y=x)')
plt.title(f'ANN Model1 Testing: R^2 = {R_test1:.4f}')
plt.xlabel('Actual Compressive Strength')
plt.ylabel('Predicted Compressive Strength')
plt.legend()
plt.tight_layout()
plt.savefig(os.path.join(RESULT_DIR, 'ANN_Model1.png'), dpi=1200, bbox_inches='tight', pad_inches=0.1, facecolor='white')
plt.show()


'''
ANN_model2结果输出与可视化
'''
#结果计算
model2 = ANN_model2()
model2.train(input_train, output_train, epochs=10000)
#训练集预测
y_pred_train2 = model2.predict(input_train)
#测试集预测
y_pred_test2 = model2.predict(input_test)
#计算R^2、MSE、MAE
#训练集
y_mean_train2 = np.mean(output_train)
TSS_train2 = np.sum((output_train - y_mean_train2) ** 2)
RSS_train2 = np.sum((output_train - y_pred_train2) ** 2)
R_train2 = 1 - (RSS_train2 / TSS_train2)
MSE_train2 = np.mean((output_train - y_pred_train2) ** 2)
MAE_train2 = np.mean(np.abs(output_train - y_pred_train2))
#测试集
y_mean_test2 = np.mean(output_test)
TSS_test2 = np.sum((output_test - y_mean_test2) ** 2)
RSS_test2 = np.sum((output_test - y_pred_test2) ** 2)
R_test2 = 1 - (RSS_test2 / TSS_test2)
MSE_test2 = np.mean((output_test - y_pred_test2) ** 2)
MAE_test2 = np.mean(np.abs(output_test - y_pred_test2))
#结果输出
print(f'ANN Model2 Training: R^2 = {R_train2:.4f}, MSE = {MSE_train2:.4f}, MAE = {MAE_train2:.4f}')
print(f'ANN Model2 Testing: R^2 = {R_test2:.4f}, MSE = {MSE_test2:.4f}, MAE = {MAE_test2:.4f}')
#可视化
plt.figure(figsize=(10, 5))
#train集可视化
plt.subplot(1, 2, 1)
plt.scatter(output_train, y_pred_train2, color='blue', label='Predicted vs Actual')
plt.plot([output_train.min(), output_train.max()], [output_train.min(), output_train.max()], 'r--', label='Ideal Fit (y=x)')
plt.title(f'ANN Model2 Training: R^2 = {R_train2:.4f}')
plt.xlabel('Actual Compressive Strength')
plt.ylabel('Predicted Compressive Strength')
plt.legend()
#test集可视化
plt.subplot(1, 2, 2)
plt.scatter(output_test, y_pred_test2, color='green', label='Predicted vs Actual')
plt.plot([output_test.min(), output_test.max()], [output_test.min(), output_test.max()], 'r--', label='Ideal Fit (y=x)')
plt.title(f'ANN Model2 Testing: R^2 = {R_test2:.4f}')
plt.xlabel('Actual Compressive Strength')
plt.ylabel('Predicted Compressive Strength')
plt.legend()
plt.tight_layout()
plt.savefig(os.path.join(RESULT_DIR, 'ANN_Model2.png'), dpi=1200, bbox_inches='tight', pad_inches=0.1, facecolor='white')
plt.show()

#基于ANN神经网络的混凝土抗压强度预测模型构建与评估完成！！！！！！！！！！