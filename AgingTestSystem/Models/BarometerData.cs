
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
        /// 当前配方的显示模式（【V1.66 新增】烧屏画面记录，自由文本，可空）。
        /// 由 StationInfo.DisplayMode 经采集叠加写入；面板暂不显示，启动/报警日志携带追溯。
        /// </summary>
        public string DisplayMode { get; set; }

        /// <summary>
        /// 载台电流（【V1.74 新增】Q2 通用骨架：每工位一路，单位 A）。
        /// 由 DeviceManager 采集循环从 IPowerMeter 回填（与 InputStatus/OutputStatus 同位置）：
        /// - float.NaN = 无数据（电表未启用/未连接/该路无回采），面板悬停显示"--"，CSV 记空，
        ///   规则变量取 NaN（比较恒 false，不误报，与 temp 离线同语义）；
        /// - 正常值 = 该工位载台实时电流。注意它只是"记录+追溯"，不参与任何报警判定
        ///   （电流判报警等电表到货、阈值策略定了之后再做，现在动手就是误报）。
        /// </summary>
        public float LoadCurrentA { get; set; } = float.NaN;

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
        /// 最近一次老化测试的结果（V1.59 新增）
        /// "PASS" = 本次老化全程无报警、到时正常完成；"FAIL" = 测试中途发生报警被联动切断；
        /// 空字符串 = 尚未测过或已人工复位（复位时清空）。
        /// 【为什么放在数据模型上】工位面板渲染与历史追溯都直接读采集缓存，
        /// 跟着 BarometerData 走可以让"完成待取料/合格标记"像压力值一样自动广播到所有界面。
        /// </summary>
        public string LastTestResult { get; set; } = "";

        /// <summary>
        /// 采集时间戳
        /// </summary>
        public DateTime CollectTime { get; set; }

        /// <summary>
        /// IO输入状态列表（每个气压表对应1个IO输入）
        /// 【V1.09 更新】依据IO分配表，每个气压表仅有1个输入: 真空负压表信号(NPN, X地址)
        /// 索引0对应 真空负压表输入点(X000 等)
        /// </summary>
        public bool[] InputStatus { get; set; } = new bool[1];

        /// <summary>
        /// IO输出状态列表（每个气压表对应2个IO输出）
        /// 【V1.09 更新】依据IO分配表，每个气压表有2个输出(PNP, Y地址):
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
                DisplayMode = this.DisplayMode,
                LoadCurrentA = this.LoadCurrentA,
                Status = this.Status,
                LastTestResult = this.LastTestResult,
                DelayTime = this.DelayTime,
                StartTime = this.StartTime,
                CollectTime = this.CollectTime,
                // 数组深拷贝，避免外部修改影响原对象
                // 【大扫荡】null→空数组不回 null：字段缺省非空，下游 Length/[0] 不判空，
                // 某处置 null 后 Clone→广播→面板即 NRE（ApplyData 有守卫别处没有）。
                InputStatus = (bool[])this.InputStatus?.Clone() ?? new bool[0],
                OutputStatus = (bool[])this.OutputStatus?.Clone() ?? new bool[0]
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
        /// 已完成·待取料（V1.59 新增）
        /// 老化计时到时、自动下电关阀后的状态：产品已老化完毕但仍吸附在载台上，
        /// 等操作员取件。面板用独立颜色标注，人工复位或重新扫码绑定后回到 Idle。
        /// 【为什么不直接回 Idle】操作员需要一眼区分"这个工位还没投料"和
        /// "这个工位测完了该取件了"——72 个工位全靠颜色管理，混用一个状态会漏取料。
        /// </summary>
        Completed,

        /// <summary>
        /// 故障状态
        /// </summary>
        Fault
    }

    /// <summary>
    /// 老化测试子阶段枚举（V1.59 新增，DeviceManager 内部状态机使用）
    ///
    /// 【三阶段时序】（行业通识：未吸附固定不通电，老化讲究连续性）
    ///   启动(只开阀) ──► Vacuuming 抽真空 ──► 真空到位 且 延时开启到 ──► 上电
    ///                 （到位前超时 = 真空建立失败报警，永不带电）         │
    ///                                                                  ▼
    ///                              到时自动下电关阀 ◄── Aging 老化计时（配方启动时间）
    ///                                    │
    ///                                    ▼
    ///                            Completed 已完成·待取料(PASS)
    /// </summary>
    public enum AgingPhase
    {
        /// <summary>未在测试</summary>
        None = 0,

        /// <summary>抽真空阶段：阀已开、载台未上电，等"压力到位 + 延时开启到"</summary>
        Vacuuming = 1,

        /// <summary>老化计时阶段：已上电，倒计时到时后自动完成</summary>
        Aging = 2
    }
}
