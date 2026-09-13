using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using AgingTestSystem.Services;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 公共参数窗口（设置所有气压表负压阈值）—— 业务逻辑部分
    /// 界面标题为"公共参数窗口"，其唯一功能是设置所有气压表的负压阈值。
    ///
    /// 【功能说明】
    /// 设置所有气压表的"负压阈值"（设备阈值），一次性批量写入全部气压表。
    /// 逻辑参考 ModbusRtuBarometerTest Demo 的 BatchSetThreshold 方法：
    /// 把用户输入的负压值（单位 kPa），逐台写入气压表 Holding Register 0x0010（功能码 0x06），
    /// 该寄存器驱动气压表内部的硬件报警触点（压力到达阈值时触点动作）。
    /// 【V1.19.9 新增】保存时同步更新软件报警阈值（DeviceConfig.AlarmPressureThresholdKPa），
    /// 使 DeviceManager 的压力报警判定与界面输入的负压值（kPa）保持一致。
    ///
    /// 【界面布局】（所有控件居中显示）
    /// ┌───────────────────────────────┐
    /// │  负压值设定(kPa)：[  -5.0  ]  │ ← Label + 数值框（支持正负数）
    /// │         [  保存设置   ]         │ ← 保存按钮
    /// └───────────────────────────────┘
    ///
    /// 【工作流程】
    /// 1. 用户在数值框设置负压值（默认 -5，支持正负数，单位 kPa，与气压表读数一致）
    /// 2. 点击"保存设置"按钮
    /// 3. 数值框限制范围（-9999~9999），保证输入为有效数字
    /// 4. 后台线程逐台写入全部气压表（避免 72 台连写阻塞 UI 线程）
    /// 5. 写入完成，切回 UI 线程汇总显示成功/失败台数：
    ///    - 全部成功：提示后关闭窗口
    ///    - 部分失败：列出失败台号，窗口保持打开便于现场排查后重试
    ///
    /// 【线程说明】（为什么写入要放后台线程）
    /// 批量写 72 台 + 坏设备（断电/掉线/地址拨错）时，每台坏设备约阻塞一个读超时
    /// （默认 1000ms，NModbus 还会重试），整体可能卡住 UI 几十秒。
    /// 所以用 Task 放后台线程执行（DeviceManager.SetAllBarometerThresholds 内部
    /// 已有互斥锁，串行化串口请求，线程安全），写完后用 BeginInvoke 切回 UI 线程。
    ///
    /// 【与旧版的区别】
    /// 旧版误做成"采集间隔 + 软件报警阈值"的通用参数设置；新版按现场实际需求简化为
    /// 只设置所有气压表的负压阈值（写设备 0x0010），并同步简化界面。
    /// </summary>
    public partial class CommonParameterForm : Sunny.UI.UIForm
    {
        /// <summary>
        /// 设备管理器（核心服务，透传所有硬件通讯）
        /// 用它调用 SetAllBarometerThresholds 批量写入所有气压表阈值
        /// </summary>
        private readonly DeviceManager _deviceManager;

        /// <summary>
        /// 窗体已关闭标记（V1.72.15 新增，全仓关窗竞态排查：后台批量写线程在关闭前后脚
        /// BeginInvoke 进已销毁句柄即炸"线程间操作无效"。volatile 保证后台线程立即可见；
        /// OnFormClosed 首行置位，投递点与完成入口双查，丢日志不炸框）。
        /// </summary>
        private volatile bool _closed;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="deviceManager">设备管理器（主窗体传入，负责批量写阈值）</param>
        public CommonParameterForm(DeviceManager deviceManager)
        {
            InitializeComponent();
            _deviceManager = deviceManager;

            // 界面控件居中显示（标签+输入框一组居中，按钮居中）
            CenterControls();
        }

        /// <summary>
        /// 打开后调一次：整窗按当前主题着色。
        /// 【V1.71】保存按钮已是主按钮蓝（两边都清晰），V1.60.4 的 DimGray 特例已删除。
        /// （窗体每次 new 的新实例，无需恢复。）
        /// </summary>
        public void ApplyTheme()
        {
            ThemeManager.ApplyTo(this);
        }

        /// <summary>
        /// 把控件组在窗体里居中显示
        ///
        /// 【为什么需要手工居中】
        /// 窗体是 FixedDialog（不可缩放），设计器里按固定位置摆好之后，
        /// 再根据窗体的 ClientSize 动态算一次位置，保证任何分辨率下都水平居中。
        /// 输入框和标签是"一组"（标签在左、输入框在右），这组整体水平居中；
        /// 保存按钮单独水平居中。
        ///
        /// 【所见即所得约定（V1.72.1）】
        /// UIForm 自绘蓝标题占 35px 客户区，设计器里看到的 Y（lbl 65 / nud 62 / btn 110）
        /// 就是运行时的 Y——这里只调 X（水平居中），不动 Y。
        /// 以前这里连 Y 一起写（30→65），设计器看的是旧值、运行时被搬走，
        /// 所以"设计器里离标题太近、跑起来又正常"。以后改纵向位置只改 Designer，
        /// 不要在这里写 Y，两边就永远一致。
        /// </summary>
        private void CenterControls()
        {
            // 标签的实际占用宽度（V1.72.3 血泪：原来只用 MeasureText 纯文本宽，
            // 但 lbl 是 AutoSize 的 Sunny UILabel——默认宋体 12pt + 自带内边距，
            // 实际占用（lbl.Width / PreferredSize）比纯文本宽大好几个像素；
            // 按小了算整组宽度，输入框就偏左盖到标签上。取两者最大值，两边都保：
            // 即使构造时 AutoSize 还没布局（Width 偏小），MeasureText 也能兜底。）
            int textWidth = System.Windows.Forms.TextRenderer.MeasureText(
                lblThreshold.Text, lblThreshold.Font).Width;
            int labelWidth = System.Math.Max(textWidth, lblThreshold.Width);

            // 一组控件（标签 + 间距 + 数值框）的整体宽度
            const int gap = 10;                     // 标签与数值框之间的间距（V1.72.3：8→10，用户点名太挤，留白更松）
            int groupWidth = labelWidth + gap + nudThreshold.Width;

            // 整组水平居中：左边距 = (窗体宽度 - 整组宽度) / 2（只动 X，Y 以 Designer 为准）
            int groupLeft = (ClientSize.Width - groupWidth) / 2;

            // 第一行：只居中 X，Y 保持设计值（lbl 65 / nud 62，已含 35px 标题区）
            lblThreshold.Left = groupLeft;
            nudThreshold.Left = groupLeft + labelWidth + gap;

            // 第二行：保存按钮水平居中（Y 保持设计值 110）
            btnSave.Left = (ClientSize.Width - btnSave.Width) / 2;
        }

        /// <summary>
        /// "保存设置"按钮点击事件
        ///
        /// 【流程】
        /// 1. 读取数值框的值（NumericUpDown 保证为有效数字，含正负数）
        /// 2. 保存期间禁用按钮、把文字改成"保存中..."，防止重复点击
        /// 3. 后台线程批量写入所有气压表（SetAllBarometerThresholds）
        /// 4. 写完后切回 UI 线程调用 OnBatchWriteCompleted 汇总显示结果
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            // ===== 1) 读取负压值 =====
            // NumericUpDown 已限制范围（-9999~9999）且保证为有效数字，无需再做文本校验
            decimal thresholdValue = nudThreshold.Value;

            // ===== 2) 保存期间锁定按钮，防止重复点击 =====
            btnSave.Enabled = false;
            btnSave.Text = "保存中...";

            // ===== 2.5) 同步软件报警阈值（V1.19.9） =====
            // 生产环境报警判定以本窗口输入的负压值（kPa）为准，
            // 写设备阈值的同时更新软件报警阈值，两者保持一致。
            _deviceManager.UpdateAlarmPressureThresholdKPa(thresholdValue);

            // ===== 3) 后台线程批量写入 =====
            // SetAllBarometerThresholds 逐台写 0x0010，内部有互斥锁（线程安全）；
            // 放在后台线程执行，避免 72 台连写阻塞 UI。
            Task.Run(() =>
            {
                // 写入结果字典（deviceId → 是否成功）；异常时置 null 标记
                Dictionary<int, bool> result;
                try
                {
                    result = _deviceManager.SetAllBarometerThresholds(thresholdValue);
                }
                catch (Exception ex)
                {
                    result = null;
                    System.Diagnostics.Debug.WriteLine($"[负压阈值设置] 批量写入异常: {ex.Message}");
                }

                // ===== 4) 切回 UI 线程显示结果 =====
                // 【V1.72.15】旧版只查 IsHandleCreated 且无 try：关闭瞬间仍能 BeginInvoke 进已销毁句柄
                // （与通讯/风扇窗同病根）；现关窗/释放/句柄三查 + try 包，关后完成回调直接丢弃。
                try
                {
                    if (_closed || IsDisposed || Disposing) return;
                    if (!IsHandleCreated) return;
                    BeginInvoke(new Action(() => OnBatchWriteCompleted(thresholdValue, result)));
                }
                catch { }
            });
        }

        /// <summary>
        /// 窗体关闭时置位，拦后台完成回调（**不**停硬件写——写阈值是整批落盘语义，
        /// 关窗只丢 UI 回调，设备侧写完即止；见 OnBatchWriteCompleted 入口自拦）。
        /// </summary>
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            _closed = true;
            base.OnFormClosed(e);
        }

        /// <summary>
        /// 批量写入完成后的 UI 汇总（在 UI 线程执行）
        ///
        /// 【结果处理规则】（参考 Demo 的 ShowBatchWriteSummary）
        /// - result 为 null：通讯异常，提示检查连接
        /// - result 为空字典：没有连上任何气压表，提示检查连接
        /// - 全部成功：提示成功台数，关闭窗口
        /// - 部分失败：列出失败台号 + 排查提示，窗口保持打开便于重试
        /// </summary>
        /// <param name="thresholdValue">本次写入的负压值（用于提示）</param>
        /// <param name="result">写入结果字典（deviceId → 是否成功），异常时为 null</param>
        private void OnBatchWriteCompleted(decimal thresholdValue, Dictionary<int, bool> result)
        {
            // 【V1.72.15】排队期间关窗即丢弃：BeginInvoke 已投递的回调仍会在关后执行，
            // 直碰 btnSave/MessageBox 即炸，故先拦（与通讯窗 SetConnected 同因）。
            if (_closed || IsDisposed || Disposing) return;
            try
            {
                if (btnSave == null || btnSave.IsDisposed) return;
            }
            catch { return; }
            // 恢复按钮状态（无论成功失败都要恢复）
            btnSave.Enabled = true;
            btnSave.Text = "保存设置";

            // 通讯异常（result 为 null）
            if (result == null)
            {
                // 【大扫荡】记中断事件：内存阈值已是新值、硬件未知写到哪，
                // 以前只弹框，追溯链里无痕（关窗中断连框都不弹）。
                try
                {
                    AgingTestSystem.Services.TestEventLogger.Write("", 0, "阈值批量写中断",
                        $"目标 {thresholdValue} kPa，写入过程通讯异常，硬件与内存阈值可能不一致，请重进本窗重试",
                        sn: "", recipe: "", result: "");
                }
                catch { }
                MessageBox.Show("批量设置失败，请检查气压表通讯连接", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 空结果：说明一个都没连上（SetAllThresholds 按需重连后仍连不上返回空字典）
            if (result.Count == 0)
            {
                MessageBox.Show("气压表未连接，请先连接（请检查串口/驱动后重试）", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 统计成功台数和失败台号
            int successCount = 0;
            var failedList = new List<int>();
            foreach (var kv in result)
            {
                if (kv.Value)
                {
                    successCount++;
                }
                else
                {
                    failedList.Add(kv.Key);
                }
            }

            // ===== 全部成功：提示后关闭窗口 =====
            if (failedList.Count == 0)
            {
                MessageBox.Show($"设置完成！成功 {successCount} 台，全部成功。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                this.DialogResult = DialogResult.OK;
                this.Close();
                return;
            }

            // ===== 部分失败：列出失败台号，窗口保持打开便于重试 =====
            // 失败原因一般是：断电 / 掉线 / 从站地址拨错 / 设备损坏，
            // 一次列出所有失败台，避免逐台弹窗（失败几十台不用点几十次确认）。
            // 【大扫荡】失败台硬件仍是旧阈值（内存已是新值）：明示+记事件，
            // 以前只说"重试"，不说口径已分叉。
            string failedText = string.Join("、", failedList);
            try
            {
                AgingTestSystem.Services.TestEventLogger.Write("", 0, "阈值部分失败",
                    $"目标 {thresholdValue} kPa，成功 {successCount} 台，失败台 {failedText} 仍是旧阈值",
                    sn: "", recipe: "", result: "");
            }
            catch { }
            MessageBox.Show(
                $"设置完成！成功 {successCount} 台，失败 {failedList.Count} 台。\r\n" +
                $"失败台号：{failedText}\r\n\r\n" +
                "注意：失败台硬件仍是旧阈值（软件已按新值判定），重试前该台口径不一致。\r\n" +
                "提示：失败通常表示该台气压表断电 / 掉线 / 从站地址拨错 / 损坏，\r\n" +
                "请检查硬件后点击【保存设置】重试。\r\n\r\n" +
                "【小数位核对】若仪表本机显示值与设定值差 10 倍，\r\n" +
                "请核对 App.config 的 BarometerDefaultDecimalPlaces 是否与仪表实际小数位一致。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
