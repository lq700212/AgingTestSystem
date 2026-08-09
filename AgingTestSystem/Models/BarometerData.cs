
using System;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// 气压表数据模型
    /// 用于存储单个气压表采集到的实时数据
    /// </summary>
    public class BarometerData
    {
        /// <summary>
        /// 气压表编号（从1开始）
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// 真空压力值（单位：kPa，与气压表读数一致，V1.19.9 由 Pa 改为 kPa）
        /// </summary>
        public decimal VacuumPressure { get; set; }

        /// <summary>
        /// 设备序列号
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>
        /// 当前使用的配方名称
        /// </summary>
        public string RecipeName { get; set; }

        /// <summary>
        /// 设备状态枚举：空闲、测试中、故障
        /// </summary>
        public DeviceStatus Status { get; set; }

        /// <summary>
        /// 延时开启时间（时:分:秒）
        /// </summary>
        public TimeSpan DelayTime { get; set; }

        /// <summary>
        /// 延时到达时间（时:分:秒）
        /// </summary>
        public TimeSpan StartTime { get; set; }

        /// <summary>
        /// 采集时间戳
        /// </summary>
        public DateTime CollectTime { get; set; }

        /// <summary>
        /// IO输入状态列表（每个气压表对应1个IO输入）
        /// 【V1.09 更新】依据显耀IO表，每个气压表仅有1个输入: 真空负压表信号(NPN, X地址)
        /// 索引0对应 真空负压表输入点(X000 等)
        /// </summary>
        public bool[] InputStatus { get; set; } = new bool[1];

        /// <summary>
        /// IO输出状态列表（每个气压表对应2个IO输出）
        /// 【V1.09 更新】依据显耀IO表，每个气压表有2个输出(PNP, Y地址):
        /// 索引0对应 真空电磁阀输出点(Y000 等)
        /// 索引1对应 载台上电输出点(Y110 等)
        /// </summary>
        public bool[] OutputStatus { get; set; } = new bool[2];

        /// <summary>
        /// 创建当前对象的深拷贝
        /// 【用途】DeviceManager 返回缓存数据时返回副本，避免外部修改污染缓存
        /// 数组类型字段（InputStatus/OutputStatus）也会被复制
        /// </summary>
        /// <returns>当前对象的深拷贝</returns>
        public BarometerData Clone()
        {
            return new BarometerData
            {
                DeviceId = this.DeviceId,
                VacuumPressure = this.VacuumPressure,
                SerialNumber = this.SerialNumber,
                RecipeName = this.RecipeName,
                Status = this.Status,
                DelayTime = this.DelayTime,
                StartTime = this.StartTime,
                CollectTime = this.CollectTime,
                // 数组深拷贝，避免外部修改影响原对象
                InputStatus = (bool[])this.InputStatus?.Clone(),
                OutputStatus = (bool[])this.OutputStatus?.Clone()
            };
        }
    }

    /// <summary>
    /// 设备运行状态枚举
    /// </summary>
    public enum DeviceStatus
    {
        /// <summary>
        /// 空闲状态
        /// </summary>
        Idle,

        /// <summary>
        /// 测试中
        /// </summary>
        Testing,

        /// <summary>
        /// 故障状态
        /// </summary>
        Fault
    }
}
