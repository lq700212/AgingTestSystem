
using System;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// 工位静态信息模型（V1.19.11 新增）
    ///
    /// 【用途】
    /// 气压表硬件本身只上报"压力"数值，SN / 配方 / 延时时间 / 烧屏时间 这类
    /// "工位配置"信息无法从设备读取，需要由上位机维护并叠加到采集数据上展示。
    /// 本模型就是"工位配置"的载体：
    /// - 通过 ID 绑定（IdBindingForm，扫码枪扫码或手动输入）写入 SN；
    /// - 通过 工位设置窗口（StationSettingsForm 保存按钮）写入 SN / 配方 / 延时；
    /// - DeviceManager 在每次采集时把这里存的静态信息叠加到 BarometerData，
    ///   使工位面板（WorkstationPanelView）能显示关联后的 SN / 配方 / 延时。
    ///
    /// 【为什么需要这个类】
    /// 原来 SN / 配方 / 延时只在 Mock 数据里生成，真实气压表（ModbusRtuBarometerReader）
    /// 采集的数据这些字段恒为空，面板上永远显示不了现场绑定的 SN。
    /// 新增本模型 + DeviceManager 的按工位存储后，无论扫码枪还是手动录入，
    /// 只要写入一次，采集叠加后所有显示 SN/配方/延时 的地方都会同步展示。
    /// </summary>
    public class StationInfo
    {
        /// <summary>
        /// 工位编号（1 ~ TotalBarometers）
        /// </summary>
        public int DeviceId { get; set; }

        /// <summary>
        /// 产品序列号（绑定 / 手动录入，可空）
        /// </summary>
        public string SerialNumber { get; set; }

        /// <summary>
        /// 配方名称（工位设置窗口录入，可空）
        /// </summary>
        public string RecipeName { get; set; }

        /// <summary>
        /// 配方的负压值设定（kPa，V1.59 新增；null = 未绑定配方或配方未设定）
        /// 【用途】作为该工位的"真空到位判定"与"报警阈值"：
        /// 不同产品的吸附要求不同（默认 -5kPa，有的要求更强的 -60kPa 才算吸附牢固），
        /// 绑定了配方且设定了负压值的工位按配方值判定，未配置的回退全局
        /// AlarmPressureThresholdKPa。启动测试时定格进本次任务，
        /// 中途改配方不影响已在跑的测试。
        /// </summary>
        public decimal? RecipeNegativePressure { get; set; }

        /// <summary>
        /// 配方的显示模式（【V1.66 新增】烧屏画面记录，自由文本，可空）。
        /// 与 RecipeName 同步下发、同步清空；采集叠加到 BarometerData 供面板/日志追溯。
        /// </summary>
        public string DisplayMode { get; set; }

        /// <summary>
        /// 延时时间（时:分:秒，上电前等待，工位设置窗口录入）
        /// 为空表示尚未配置
        /// </summary>
        public TimeSpan? DelayTime { get; set; }

        /// <summary>
        /// 烧屏时间（时:分:秒，上电后老化时长，工位设置窗口录入）
        /// 为空表示尚未配置
        /// </summary>
        public TimeSpan? BurnInTime { get; set; }

        /// <summary>
        /// 深拷贝（避免外部修改污染 DeviceManager 内部存储）
        /// </summary>
        /// <returns>当前对象副本</returns>
        public StationInfo Clone()
        {
            return new StationInfo
            {
                DeviceId = this.DeviceId,
                SerialNumber = this.SerialNumber,
                RecipeName = this.RecipeName,
                RecipeNegativePressure = this.RecipeNegativePressure,
                DisplayMode = this.DisplayMode,
                DelayTime = this.DelayTime,
                BurnInTime = this.BurnInTime
            };
        }
    }
}
