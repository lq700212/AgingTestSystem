
using System;

namespace BarometerWinform.Models
{
    /// <summary>
    /// 配方配置模型
    /// 用于存储测试配方的参数配置
    /// 每个配方包含测试所需的各项参数
    /// </summary>
    public class RecipeConfig
    {
        /// <summary>
        /// 配方编号
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 配方名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 负压值设定（单位：kPa，与气压表读数一致）
        /// </summary>
        public decimal NegativePressure { get; set; }

        /// <summary>
        /// 延时时间（时:分:秒，配方窗口"延时时间"，工位面板"延时开启"）
        /// </summary>
        public TimeSpan DelayTime { get; set; }

        /// <summary>
        /// 启动时间（时:分:秒，配方窗口"启动时间"，工位面板"延时到达"）
        /// </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>
        /// 极限温度（单位：摄氏度）
        /// </summary>
        public decimal LimitTemperature { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;
    }
}
