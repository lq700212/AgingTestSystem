using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using BarometerWinform.Services;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 负压阈值设置窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 设置所有气压表的"负压阈值"（设备阈值），一次性批量写入全部气压表。
    /// 逻辑参考 ModbusRtuBarometerTest Demo 的 BatchSetThreshold 方法：
    /// 把用户输入的负压值，逐台写入气压表 Holding Register 0x0010（功能码 0x06），
    /// 该寄存器驱动气压表内部的硬件报警触点（压力到达阈值时触点动作）。
    ///
    /// 【界面布局】（所有控件居中显示）
    /// ┌───────────────────────────────┐
    /// │     负压值设定：[_________]     │ ← Label + 输入框
    /// │         [  保存设置   ]         │ ← 保存按钮
    /// └───────────────────────────────┘
    ///
    /// 【工作流程】
    /// 1. 用户在输入框填写负压值（默认 -95，单位与气压表读数一致）
    /// 2. 点击"保存设置"按钮
    /// 3. 校验输入是否合法（非空、必须是数字）
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
    public partial class CommonParameterForm : Form
    {
        /// <summary>
        /// 设备管理器（核心服务，透传所有硬件通讯）
        /// 用它调用 SetAllBarometerThresholds 批量写入所有气压表阈值
        /// </summary>
        private readonly DeviceManager _deviceManager;

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
        /// 把控件组在窗体里居中显示
        ///
        /// 【为什么需要手工居中】
        /// 窗体是 FixedDialog（不可缩放），设计器里按固定位置摆好之后，
        /// 再根据窗体的 ClientSize 动态算一次位置，保证任何分辨率下都水平居中。
        /// 输入框和标签是"一组"（标签在左、输入框在右），这组整体水平居中；
        /// 保存按钮单独水平居中。
        /// </summary>
        private void CenterControls()
        {
            // 标签的实际像素宽度（AutoSize 的宽度用 PreferredSize 或测量文本得到）
            int labelWidth = System.Windows.Forms.TextRenderer.MeasureText(
                lblThreshold.Text, lblThreshold.Font).Width;

            // 一组控件（标签 + 间距 + 输入框）的整体宽度
            const int gap = 8;                      // 标签与输入框之间的间距
            int groupWidth = labelWidth + gap + txtThreshold.Width;

            // 整组水平居中：左边距 = (窗体宽度 - 整组宽度) / 2
            int groupLeft = (ClientSize.Width - groupWidth) / 2;

            // 第一行：标签在上、输入框微调垂直对齐（标签高 12，输入框高 21，y 差 3 即居中）
            lblThreshold.Location = new System.Drawing.Point(groupLeft, 30);
            txtThreshold.Location = new System.Drawing.Point(groupLeft + labelWidth + gap, 27);

            // 第二行：保存按钮水平居中
            btnSave.Location = new System.Drawing.Point((ClientSize.Width - btnSave.Width) / 2, 75);
        }

        /// <summary>
        /// "保存设置"按钮点击事件
        ///
        /// 【流程】
        /// 1. 校验输入（非空、是数字）
        /// 2. 保存期间禁用按钮、把文字改成"保存中..."，防止重复点击
        /// 3. 后台线程批量写入所有气压表（SetAllBarometerThresholds）
        /// 4. 写完后切回 UI 线程调用 OnBatchWriteCompleted 汇总显示结果
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            // ===== 1) 校验输入 =====
            string text = txtThreshold.Text.Trim();

            // 空输入：提示并聚焦输入框
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("请输入负压值", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtThreshold.Focus();
                return;
            }

            // 非数字：提示并聚焦输入框
            if (!decimal.TryParse(text, out decimal thresholdValue))
            {
                MessageBox.Show("请输入有效的数字", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtThreshold.Focus();
                return;
            }

            // ===== 2) 保存期间锁定按钮，防止重复点击 =====
            btnSave.Enabled = false;
            btnSave.Text = "保存中...";

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
                // IsHandleCreated 判断窗体还没被关闭，避免对已释放窗体调用 BeginInvoke
                if (IsHandleCreated)
                {
                    BeginInvoke(new Action(() => OnBatchWriteCompleted(thresholdValue, result)));
                }
            });
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
            // 恢复按钮状态（无论成功失败都要恢复）
            btnSave.Enabled = true;
            btnSave.Text = "保存设置";

            // 通讯异常（result 为 null）
            if (result == null)
            {
                MessageBox.Show("批量设置失败，请检查气压表通讯连接", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // 空结果：说明一个都没连上（SetAllThresholds 未连接时返回空字典）
            if (result.Count == 0)
            {
                MessageBox.Show("未连接任何气压表，请先检查通讯连接", "提示",
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
            string failedText = string.Join("、", failedList);
            MessageBox.Show(
                $"设置完成！成功 {successCount} 台，失败 {failedList.Count} 台。\r\n" +
                $"失败台号：{failedText}\r\n\r\n" +
                "提示：失败通常表示该台气压表断电 / 掉线 / 从站地址拨错 / 损坏，\r\n" +
                "请检查硬件后点击【保存设置】重试。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
