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
    /// 【V1.19.11】"保存"已实现：把 SN / 配方 / 延时开启 / 延时到达 写入
    /// DeviceManager 工位静态信息，工位面板同步更新。
    /// 右侧按钮（破空 / 下电 / 加入对列）的具体业务功能待确认，
    /// 代码中先留下 TODO 标记，后续确认后再实现；"关闭窗口"为直接关闭。
    ///
    /// 【数据来源】
    /// 构造时传入设备管理器，从采集缓存读取当前工位数据用于回显；
    /// SN / 配方 / 延时来自工位静态信息叠加后的缓存（与工位面板一致）。
    /// 数据中不存在"极限温度"字段（属配方配置），留空待后续接入配方表。
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
        /// 【V1.19.11】SN / 配方 / 延时来自工位静态信息叠加后的缓存数据，
        /// 与工位面板（WorkstationPanelView）显示一致。
        /// </summary>
        private void LoadStationData()
        {
            BarometerData data = _deviceManager?.GetBarometerData(_deviceId);
            if (data == null) return;

            // 状态：IDLE / SELECT / BUSY / FAULT（由设备状态 + 载台上电状态推导）
            txtState.Text = GetStateText(data);
            // SN（来自工位静态信息叠加，可改）
            txtSN.Text = data.SerialNumber;
            // 配方名称
            txtRecipe.Text = data.RecipeName;
            // 延时开启时间（时:分:秒）
            txtDelay.Text = data.DelayStartTime.ToString(@"hh\:mm\:ss");
            // 启动时间（延时到达，时:分:秒）
            txtStart.Text = data.DelayArriveTime.ToString(@"hh\:mm\:ss");
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
        /// 保存按钮点击事件（【V1.19.11 实现】）
        ///
        /// 【功能】
        /// 把本窗口录入的 SN / 配方 / 延时开启 / 延时到达 写入设备管理器工位静态信息，
        /// 采集线程下次叠加后，工位面板（SN / 配方 / 延时显示）即同步更新。
        ///
        /// 【说明】
        /// - SN / 配方：可空，空串视为清空。
        /// - 延时开启 / 延时到达：格式 时:分:秒（如 01:10:20），为空视为清空。
        /// - 启动时间输入框在本窗体对应"延时到达"（与 RecipeManagerForm 语义一致）。
        /// - 极限温度（txtTemp）暂不处理：BarometerData / 工位面板没有对应字段，
        ///   属配方配置范畴，留待后续接入配方表时再关联。
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            if (_deviceManager == null)
            {
                MessageBox.Show("设备管理器未就绪，无法保存", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // ---- 1) 解析延时时间（时:分:秒）----
            TimeSpan? delayStart = ParseTimeInput(txtDelay.Text, "延时开启");
            if (delayStart == null) return;
            TimeSpan? delayArrive = ParseTimeInput(txtStart.Text, "延时到达");
            if (delayArrive == null) return;

            // ---- 2) 写入设备管理器工位静态信息 ----
            _deviceManager.SetStationSerialNumber(_deviceId, txtSN.Text);
            _deviceManager.SetStationRecipeName(_deviceId, txtRecipe.Text);
            _deviceManager.SetStationDelayTimes(_deviceId, delayStart, delayArrive);

            // ---- 3) 提示 + 关闭 ----
            MessageBox.Show(
                $"工位 {_deviceId} 保存成功！\r\n" +
                $"SN: {(string.IsNullOrWhiteSpace(txtSN.Text) ? "（空）" : txtSN.Text.Trim())}\r\n" +
                $"配方: {(string.IsNullOrWhiteSpace(txtRecipe.Text) ? "（空）" : txtRecipe.Text.Trim())}\r\n" +
                $"延时开启: {txtDelay.Text.Trim()}\r\n" +
                $"延时到达: {txtStart.Text.Trim()}",
                "保存成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// 解析 时:分:秒 格式的时间输入（供延时开启 / 延时到达使用）
        /// 非法格式时弹窗提示并聚焦输入框，返回 null 表示放弃本次保存。
        /// </summary>
        /// <param name="text">输入文本（可能为空白）</param>
        /// <param name="fieldName">字段中文名（用于提示，如"延时开启"）</param>
        /// <returns>解析出的 TimeSpan；空白返回 TimeSpan.Zero；格式错误返回 null</returns>
        private TimeSpan? ParseTimeInput(string text, string fieldName)
        {
            string trimmed = text?.Trim() ?? "";
            if (string.IsNullOrEmpty(trimmed))
            {
                // 空白视为清空（写入 TimeSpan.Zero）
                return TimeSpan.Zero;
            }

            if (TimeSpan.TryParse(trimmed, out TimeSpan ts))
            {
                return ts;
            }

            MessageBox.Show($"{fieldName} 格式不正确，请使用 时:分:秒（如 01:10:20）", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return null;
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
