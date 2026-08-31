# -*- coding: utf-8 -*-
# @Time    : 2025/12/28 22:12
# @Author  : 杨珂
# @File    : preprocess.py
# @Software: PyCharm
# @Desc    : 数据预处理
import pandas as pd
import numpy as np
import matplotlib.pyplot as plt
from scipy.stats import skew
# 新增导入
from scipy.stats import t, probplot
import os

# 路径配置（基于脚本所在目录，兼容不同启动方式）
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
DATA_DIR = os.path.join(BASE_DIR, '..', 'data')
RESULT_DIR = os.path.join(BASE_DIR, '..', 'results')

'''
1.数据加载与查看
'''
df = pd.read_excel(os.path.join(DATA_DIR, 'data.xlsx'))
print(df.head()) #显示前五行数据
print(df.describe()) #显示数据的统计信息
df.info() #显示数据的基本信息

'''
2.缺失值处理
（81组数据，缺少一组数据，判断数据分布类型并进行处理）
'''
#绘制直方图
#plot1
plt.subplot(2, 2, 1)
plt.hist(df['水灰比'],color='#98C897',edgecolor='black',alpha=0.6)
plt.title('水灰比',font={'family':'SimHei'})
plt.xlabel('Value')
plt.ylabel('Frequency')
#plot2粉煤灰
plt.subplot(2, 2, 2)
plt.hist(df['粉煤灰'],color='#9C6BA3',edgecolor='black',alpha=0.6)
plt.title('粉煤灰',font={'family':'SimHei'})
plt.xlabel('Value')
plt.ylabel('Frequency')
#plot3硅灰
plt.subplot(2, 2, 3)
plt.hist(df['硅灰'],color='#9DBAD2',edgecolor='black',alpha=0.6)
plt.title('硅灰',font={'family':'SimHei'})
plt.xlabel('Value')
plt.ylabel('Frequency')
#plot4抗压强度
plt.subplot(2, 2, 4)
plt.hist(df['抗压强度'],color='#FE9C9D',edgecolor='black',alpha=0.6)
plt.title('抗压强度',font={'family':'SimHei'})
plt.xlabel('Value')
plt.ylabel('Frequency')
plt.tight_layout()#避免子图之间重叠
plt.savefig(os.path.join(RESULT_DIR, 'data.png'), dpi=1200, bbox_inches='tight', pad_inches=0.1, facecolor='white')
plt.show()

#计算偏度系数
print(skew(df['水灰比']))#-0.0069，视为正态分布，采用均值填充
print(skew(df['粉煤灰']))#0.0099，视为正态分布，采用均值填充
print(skew(df['硅灰']))#0.0163，视为正态分布，采用均值填充
print(skew(df['抗压强度']))#0.4585，视为偏态分布，采用中位数填充

#数据插补
df.loc[81] = np.nan
df['水灰比'] = df['水灰比'].fillna(df['水灰比'].mean())
df['粉煤灰'] = df['粉煤灰'].fillna(df['粉煤灰'].mean())
df['硅灰'] = df['硅灰'].fillna(df['硅灰'].mean())
df['抗压强度'] = df['抗压强度'].fillna(df['抗压强度'].median())

'''
数据归一化/标准化
（采用Z-Score标准化）
'''
feature_cols = ['水灰比', '粉煤灰', '硅灰']
for col in feature_cols:
    # 计算均值
    mean_val = df[col].mean()
    # 计算标准差
    std_val = df[col].std()
    # 应用Z-Score公式进行标准化
    df[col] = (df[col] - mean_val) / std_val

'''
数据集划分
'''
np.random.seed(42)#设置随机种子
all_nums = np.arange(82)
np.random.shuffle(all_nums)
test_nums = df.iloc[all_nums[:16]]
train_nums = df.iloc[all_nums[16:]]
test_nums.to_excel(os.path.join(DATA_DIR, 'test.xlsx'), index=False)
train_nums.to_excel(os.path.join(DATA_DIR, 'train.xlsx'), index=False)
#test.xlsx为测试集
#train.xlsx为训练集

#数据预处理完成！！！！！！！！！！