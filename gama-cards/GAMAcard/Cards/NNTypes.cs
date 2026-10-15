using System;
using System.Collections.Generic;

namespace MyGamaCards
{
    /// <summary>
    /// 深度学习卡片通用数据类型定义
    /// 作用：为卡片之间的端口连线提供强类型契约，保证“输入数据 → 层结构 → 模型 → 训练 → 推理/评估”
    ///       整条数据流水线在 GAMA 画布上传递的对象类型一致、可校验。
    /// 本项目（混凝土抗压强度预测课程设计）只保留任务必需的五个类型。
    /// </summary>

    /// <summary>
    /// 张量占位类型：仅在层卡片之间传递“形状信息”，不携带真实数据
    /// （真实数据的计算全部在训练卡内部的 SimpleNeuralNetwork 中完成）
    /// </summary>
    [Serializable]
    public class Tensor
    {
        // 张量形状（null 或含 0 表示该维度未指定/任意）
        public int[] Shape { get; set; }

        // 数据类型标识，例如 "float32"
        public string DType { get; set; }

        // 数据内容（本项目中层卡片仅作形状占位，通常为 null）
        public object Data { get; set; }
    }

    /// <summary>
    /// 层元数据：描述一个网络层的结构与配置
    /// 例如 Dense 层：LayerType="Dense"，Config={units:10, useBias:true}
    /// 例如激活层：LayerType="Activation"，Config={activation:"leaky_relu"}
    /// </summary>
    [Serializable]
    public class Layer
    {
        // 层类型标识（"Dense" / "Activation"）
        public string LayerType { get; set; }

        // 层配置键值对（units/useBias/activation 等）
        public Dictionary<string, object> Config { get; set; } = new Dictionary<string, object>();
    }

    /// <summary>
    /// 模型结构：按顺序组合的若干网络层（不含权重，只描述结构）
    /// 由“模型构建卡”聚合生成，交给“训练模型卡”解析并实例化真正的神经网络
    /// </summary>
    [Serializable]
    public class Model
    {
        // 模型名称（画布展示用）
        public string Name { get; set; }

        // 按连接顺序排列的网络层
        public List<Layer> Layers { get; set; } = new List<Layer>();
    }

    /// <summary>
    /// 数据集：训练/测试样本集合
    /// X 为特征矩阵（N 行 × 特征数），Y 为标签矩阵（N 行 × 标签数）
    /// 对应课程数据：X 的 3 列为水灰比/粉煤灰/硅灰（已标准化），Y 的 1 列为抗压强度
    /// </summary>
    [Serializable]
    public class Dataset
    {
        // 数据集名称（一般为文件路径，便于在日志中辨认来源）
        public string Name { get; set; }

        // 特征矩阵：X[i] 是第 i 个样本的特征向量
        public double[][] X { get; set; }

        // 标签矩阵：Y[i] 是第 i 个样本的真实标签向量
        public double[][] Y { get; set; }

        // 样本总数（便捷属性）
        public int SampleCount => X == null ? 0 : X.Length;
    }

    /// <summary>
    /// 训练结果：“训练模型卡”的输出
    /// 内部携带训练好的神经网络实例与每轮训练的 MSE 损失历史
    /// </summary>
    [Serializable]
    public class TrainingResult
    {
        // 训练好的网络（SimpleNeuralNetwork 实例），推理卡会自动解包使用
        public object TrainedModel { get; set; }

        // 每一轮（epoch）训练结束时的平均 MSE 损失，用于绘制损失曲线或查看收敛情况
        public List<double> LossHistory { get; set; } = new List<double>();

        // 网络结构文本（如 "3-10-1"），便于日志与查看器展示
        public string Architecture { get; set; }

        // 最终一轮的平均 MSE 损失
        public double FinalLoss { get; set; } = double.NaN;
    }
}
