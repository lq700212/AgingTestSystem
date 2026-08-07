using System;
using System.Windows.Forms;
using BarometerWinform.Models;
using BarometerWinform.Services;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 工位设置窗口（业务逻辑部分）—— V1.18 新增
    ///
    /// 【功能说明】
    /// 点击工位面板上的"设置"按钮（btnSet）后弹出本窗口，
    /// 用于查看 / 设置单个工位的测试相关参数：
    /// 状态、SN、配方、延时时间、启动时间、极限温度。
    ///
    /// 【界面布局】
    /// ┌────────────────────────────────────────────────┐
    /// │ 工位设置窗口 NO 1                                │  ← 标题（带工位编号）
    /// ├────────────────────────────────┬───────────────┤
    /// │ 左侧设置列（整体居中）          │ 右侧按钮列      │
    /// │  状态:                  [空闲] │ [破空]        │
    /// │  SN:                    [___] │ [下电]        │
    /// │  配方:                  [___] │ [保存]        │
    /// │  延时时间:              [___] │ [加入对列]     │
    /// │  启动时间:              [___] │ [关闭窗口]     │
    /// │  极限温度:              [___] │               │
    /// └────────────────────────────────┴───────────────┘
    /// 设置项名称与输入框均左对齐，整个设置列在窗口中水平居中。
    /// 状态项显示中文状态：空闲 / 选中 / 繁忙 / 故障（V1.18 由 IDLE/SELECT/BUSY 改中文）。
    ///
    /// 【说明】
    /// 右侧按钮（破空 / 下电 / 保存 / 加入对列）的具体业务功能待确认，
    /// 代码中先留下 TODO 标记，后续确认后再实现；"关闭窗口"为直接关闭。
    ///
    /// 【数据来源】
    /// 构造时传入设备管理器，从采集缓存读取当前工位数据用于回显；
    /// 数据中不存在"启动时间 / 极限温度"字段（属配方配置），回显留空待设置。
    /// </summary>
    public partial class StationSettingsForm : Form
    {
        /// <summary>设备管理器（用于读取当前工位数据）</summary>
        private readonly DeviceManager _deviceManager;

        /// <summary>设备配置（预留，后续按钮功能实现时计算 IO 点编号使用）</summary>
        private readonly DeviceConfig _config;

        /// <summary>当前操作的工位编号（1 ~ TotalBarometers）</summary>
        private readonly int _deviceId;

        /// <summary>
        /// 构造函数
        /// 初始化界面，设置标题为"工位设置窗口 NO X"，并从采集缓存回显当前工位数据。
        /// </summary>
        /// <param name="deviceManager">设备管理器（可为 null，null 时仅显示空输入框）</param>
        /// <param name="config">设备配置</param>
        /// <param name="deviceId">工位编号（从1开始）</param>
        public StationSettingsForm(DeviceManager deviceManager, DeviceConfig config, int deviceId)
        {
            InitializeComponent();
            _deviceManager = deviceManager;
            _config = config;
            _deviceId = deviceId;

            // 窗口标题带工位编号，如"工位设置窗口 NO 1"
            this.Text = $"工位设置窗口 NO {deviceId}";

            // 从采集缓存读取当前工位数据并回显到输入框
            LoadStationData();
        }

        /// <summary>
        /// 回显当前工位数据
        /// 读取设备管理器缓存中的该工位最新数据，填充状态 / SN / 配方 / 延时时间。
        /// 缓存中没有数据时（采集未开始 / 离线）保持输入框为空。
        /// </summary>
        private void LoadStationData()
        {
            BarometerData data = _deviceManager?.GetBarometerData(_deviceId);
            if (data == null) return;

            // 状态：IDLE / SELECT / BUSY / FAULT（由设备状态 + 载台上电状态推导）
            txtState.Text = GetStateText(data);
            // SN（采集缓存一般无值，留待现场录入/绑定）
            txtSN.Text = data.SerialNumber;
            // 配方名称
            txtRecipe.Text = data.RecipeName;
            // 延时时间（时:分:秒，取延时开启时间）
            txtDelay.Text = data.DelayStartTime.ToString(@"hh\:mm\:ss");
        }

        /// <summary>
        /// 计算工位的当前状态文本（中文：空闲 / 选中 / 繁忙 / 故障，V1.18 由英文改中文）
        /// 规则与工位面板工作状态一致：
        /// - 故障 → 故障
        /// - 测试中 → 繁忙
        /// - 空闲且载台已上电 → 选中
        /// - 空闲且载台未上电 → 空闲
        /// </summary>
        /// <param name="data">工位数据</param>
        /// <returns>状态文本</returns>
        private string GetStateText(BarometerData data)
        {
            if (data.Status == DeviceStatus.Fault) return "故障";
            if (data.Status == DeviceStatus.Testing) return "繁忙";

            // 载台上电输出状态（OutputStatus[1]）
            bool carrierPower = data.OutputStatus != null &&
                                data.OutputStatus.Length >= 2 &&
                                data.OutputStatus[1];
            return carrierPower ? "选中" : "空闲";
        }

        /// <summary>
        /// 破空按钮点击事件
        /// 具体业务功能待确认后实现。
        /// </summary>
        private void btnBreakVacuum_Click(object sender, EventArgs e)
        {
            // TODO: 破空功能待确认后实现（例如：开启真空电磁阀释放负压 / 手动排空）
        }

        /// <summary>
        /// 下电按钮点击事件
        /// 具体业务功能待确认后实现。
        /// </summary>
        private void btnPowerOff_Click(object sender, EventArgs e)
        {
            // TODO: 下电功能待确认后实现（例如：关闭该工位载台上电输出）
        }

        /// <summary>
        /// 保存按钮点击事件
        /// 具体业务功能待确认后实现。
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            // TODO: 保存功能待确认后实现（例如：将输入框的 SN / 配方 / 延时等写入工位配置）
        }

        /// <summary>
        /// 加入对列按钮点击事件
        /// 具体业务功能待确认后实现。
        /// </summary>
        private void btnAddToQueue_Click(object sender, EventArgs e)
        {
            // TODO: 加入对列功能待确认后实现（例如：将当前工位设置加入批量执行队列）
        }

        /// <summary>
        /// 关闭窗口按钮点击事件
        /// 直接关闭本窗口。
        /// </summary>
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
