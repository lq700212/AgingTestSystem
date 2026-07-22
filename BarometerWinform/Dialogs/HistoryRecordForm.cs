using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 历史记录查询窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 查询和展示历史运行日志，包括：
    /// - 按日期范围查询
    /// - 显示日志条目（时间、设备、事件、详情）
    /// - 导出日志文件（预留）
    ///
    /// 【Mock 数据说明】
    /// 当前使用 Mock 数据演示查询功能：
    /// - 窗体加载时自动生成最近 7 天的随机日志（每天 15-30 条）
    /// - 包含测试开始/完成、报警、配方变更、权限切换等多种事件类型
    /// - 查询按钮按日期范围筛选 Mock 数据并显示
    ///
    /// 【预留说明】
    /// 1. 日志存储路径未确定（待现场确认）
    /// 2. 日志格式未确定（文本/数据库/SQLite等）
    /// 3. 实际项目中应将 Mock 数据替换为真实日志查询
    /// 4. 导出功能预留
    /// </summary>
    public partial class HistoryRecordForm : Form
    {
        /// <summary>
        /// Mock 日志条目数据结构
        /// 实际项目中应替换为日志系统的数据模型
        /// </summary>
        private class MockLogEntry
        {
            /// <summary>日志时间</summary>
            public DateTime Time { get; set; }
            /// <summary>设备编号（如 NO.1）</summary>
            public string Device { get; set; }
            /// <summary>事件类型（如 测试开始/报警/配方变更）</summary>
            public string Event { get; set; }
            /// <summary>事件详情</summary>
            public string Detail { get; set; }
        }

        /// <summary>
        /// 权限名称数组（修复 M13：提取为静态字段，避免每次生成日志都创建新数组）
        /// </summary>
        private static readonly string[] PermissionNames = { "操作员", "技术员", "管理员" };

        /// <summary>
        /// 配方名称数组（修复 M13：同上，避免重复创建）
        /// </summary>
        private static readonly string[] RecipeNames = { "配方1", "配方2", "配方3", "配方4", "配方5" };

        /// <summary>
        /// Mock 日志数据列表（内存中维护）
        /// 窗体加载时生成，查询时从中筛选
        /// </summary>
        private readonly List<MockLogEntry> _mockLogs = new List<MockLogEntry>();

        /// <summary>
        /// 随机数生成器（用于生成 Mock 数据）
        /// </summary>
        private readonly Random _random = new Random();

        /// <summary>
        /// 构造函数
        /// </summary>
        public HistoryRecordForm()
        {
            InitializeComponent();

            // 生成最近 7 天的 Mock 日志数据
            GenerateMockLogs();

            // 默认查询当天的记录（让用户打开窗体就能看到数据）
            dtpStart.Value = DateTime.Today;
            dtpEnd.Value = DateTime.Today;

            // 加载当天数据到界面
            QueryLogs();
        }

        /// <summary>
        /// 生成 Mock 日志数据
        /// 覆盖最近 7 天，每天 15-30 条日志
        /// 包含多种事件类型，模拟真实运行场景
        /// </summary>
        private void GenerateMockLogs()
        {
            _mockLogs.Clear();

            // 事件类型及其详情模板（索引对应事件类型）
            // 使用 lambda 动态生成详情，让数据更真实
            var eventTemplates = new List<Func<int, string, string>>
            {
                // 0. 测试开始
                (deviceId, recipe) => $"设备启动老化测试，配方: {recipe}",
                // 1. 测试完成
                (deviceId, recipe) => $"设备完成老化测试，结果: {(_random.Next(10) < 8 ? "通过" : "失败")}",
                // 2. 报警 - 压力超限
                (deviceId, recipe) => $"真空压力低于阈值: {_random.Next(-99000, -90000)} Pa",
                // 3. 报警 - 温度超限
                (deviceId, recipe) => $"上部温度超过上限: {_random.Next(86, 95)}℃",
                // 4. 配方变更
                (deviceId, recipe) => $"配方从 配方{_random.Next(1, 6)} 切换为 {recipe}",
                // 5. 启动运行
                (deviceId, recipe) => $"系统启动运行，使用配方: {recipe}",
                // 6. 停止运行
                (deviceId, recipe) => $"系统停止运行，总运行时长: {_random.Next(1, 12)}小时{_random.Next(0, 60)}分钟",
                // 7. 权限切换
                (deviceId, recipe) => $"操作权限切换为: {PermissionNames[_random.Next(PermissionNames.Length)]}",
                // 8. 通讯异常
                (deviceId, recipe) => $"PLC通讯超时，尝试重连... 第 {_random.Next(1, 4)} 次",
                // 9. 通讯恢复
                (deviceId, recipe) => $"PLC通讯已恢复正常",
            };

            // 配方列表已提取为静态字段 RecipeNames（修复 M13）

            // 生成最近 7 天的日志
            for (int dayOffset = 6; dayOffset >= 0; dayOffset--)
            {
                // 当天的基础时间（凌晨 8 点开始）
                DateTime dayBase = DateTime.Today.AddDays(-dayOffset).AddHours(8);

                // 当天日志条数（15-30 条）
                int logCount = _random.Next(15, 31);

                // 跟踪每个设备的测试状态，模拟真实的测试流程
                var testingDevices = new HashSet<int>();

                for (int i = 0; i < logCount; i++)
                {
                    // 随机时间（在 8:00-20:00 之间）
                    DateTime logTime = dayBase.AddMinutes(_random.Next(0, 12 * 60));

                    // 随机设备编号（NO.1 ~ NO.72）
                    int deviceIdNum = _random.Next(1, 73);
                    string device = $"NO.{deviceIdNum}";

                    // 随机配方（使用静态字段，避免重复创建数组）
                    string recipe = RecipeNames[_random.Next(RecipeNames.Length)];

                    // 选择事件类型
                    // 测试开始/完成优先（保证流程合理性）
                    int eventType;
                    if (testingDevices.Count > 0 && _random.Next(100) < 30)
                    {
                        // 30% 概率完成一个正在测试的设备
                        eventType = 1;
                        testingDevices.Remove(deviceIdNum);
                    }
                    else if (_random.Next(100) < 40)
                    {
                        // 40% 概率开始新测试
                        eventType = 0;
                        testingDevices.Add(deviceIdNum);
                    }
                    else
                    {
                        // 其他事件随机
                        eventType = _random.Next(2, eventTemplates.Count);
                    }

                    string eventName = GetEventName(eventType);
                    string detail = eventTemplates[eventType](deviceIdNum, recipe);

                    _mockLogs.Add(new MockLogEntry
                    {
                        Time = logTime,
                        Device = device,
                        Event = eventName,
                        Detail = detail
                    });
                }
            }

            // 按时间倒序排序（最新的在前）
            _mockLogs.Sort((a, b) => b.Time.CompareTo(a.Time));
        }

        /// <summary>
        /// 根据事件类型索引获取事件名称
        /// </summary>
        private string GetEventName(int eventType)
        {
            switch (eventType)
            {
                case 0: return "测试开始";
                case 1: return "测试完成";
                case 2: return "报警";
                case 3: return "报警";
                case 4: return "配方变更";
                case 5: return "启动运行";
                case 6: return "停止运行";
                case 7: return "权限切换";
                case 8: return "通讯异常";
                case 9: return "通讯恢复";
                default: return "未知事件";
            }
        }

        /// <summary>
        /// 按日期范围查询日志并显示到 DataGridView
        /// </summary>
        private void QueryLogs()
        {
            dgvHistory.Rows.Clear();

            // 日期范围：开始日期的 00:00:00 到结束日期的 23:59:59
            DateTime startTime = dtpStart.Value.Date;
            DateTime endTime = dtpEnd.Value.Date.AddDays(1).AddSeconds(-1);

            // 统计符合条件的日志条数
            int matchCount = 0;

            // 从已排序的 _mockLogs 中筛选符合条件的日志
            foreach (var log in _mockLogs)
            {
                if (log.Time >= startTime && log.Time <= endTime)
                {
                    dgvHistory.Rows.Add(
                        log.Time.ToString("yyyy-MM-dd HH:mm:ss"),
                        log.Device,
                        log.Event,
                        log.Detail
                    );
                    matchCount++;

                    // 限制最大显示条数，避免界面卡顿
                    // 实际项目中应分页查询
                    if (matchCount >= 500)
                    {
                        break;
                    }
                }
            }

            // 在窗体标题显示查询结果统计
            this.Text = $"历史记录 - 共 {matchCount} 条" + (matchCount >= 500 ? "（已限制 500 条）" : "");
        }

        /// <summary>
        /// 查询按钮点击事件
        /// 按日期范围筛选 Mock 日志数据并显示
        /// </summary>
        private void btnQuery_Click(object sender, EventArgs e)
        {
            // 校验日期范围
            if (dtpStart.Value > dtpEnd.Value)
            {
                MessageBox.Show("开始时间不能晚于结束时间", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 执行查询
            QueryLogs();
        }

        /// <summary>
        /// 导出按钮点击事件（预留功能）
        ///
        /// 【预留说明】
        /// 待实现：
        /// 1. 弹出保存文件对话框（CSV/Excel 格式）
        /// 2. 遍历 DataGridView 中的数据写入文件
        /// 3. 可考虑按当前查询结果导出
        /// </summary>
        private void btnExport_Click(object sender, EventArgs e)
        {
            if (dgvHistory.Rows.Count == 0)
            {
                MessageBox.Show("当前没有可导出的数据，请先查询", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show(
                $"当前查询结果共 {dgvHistory.Rows.Count} 条日志。\n\n" +
                "导出功能预留：\n" +
                "1. 弹出保存文件对话框（CSV/Excel）\n" +
                "2. 将当前查询结果导出到指定文件",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
