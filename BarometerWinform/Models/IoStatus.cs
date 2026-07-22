
using System;

namespace BarometerWinform.Models
{
    /// <summary>
    /// IO状态数据模型
    /// 用于描述单个IO点的状态信息
    /// </summary>
    public class IoStatus
    {
        /// <summary>
        /// IO点编号（全局唯一）
        /// 输入范围：1-72，输出范围：73-216（144个输出）
        /// </summary>
        public int IoId { get; set; }

        /// <summary>
        /// IO类型：输入或输出
        /// </summary>
        public IoType Type { get; set; }

        /// <summary>
        /// IO点名称/描述
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 所属气压表编号
        /// 一个气压表对应2个输入、4个输出
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// 在所属设备中的本地编号（1-2为输入，1-4为输出）
        /// </summary>
        public int LocalIndex { get; set; }

        /// <summary>
        /// 当前状态：true为高电平/导通，false为低电平/断开
        /// </summary>
        public bool State { get; set; }

        /// <summary>
        /// 状态更新时间
        /// </summary>
        public DateTime UpdateTime { get; set; }

        /// <summary>
        /// 是否有报警
        /// </summary>
        public bool HasAlarm { get; set; }
    }

    /// <summary>
    /// IO类型枚举
    /// </summary>
    public enum IoType
    {
        /// <summary>
        /// 输入信号
        /// </summary>
        Input,

        /// <summary>
        /// 输出信号
        /// </summary>
        Output
    }
}
