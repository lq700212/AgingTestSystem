using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using BarometerWinform.Controls;
using BarometerWinform.Models;
using BarometerWinform.Services;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 系统设置窗口 —— 业务逻辑部分
    ///
    /// 【功能说明】
    /// 把 App.config 里分散的配置项按【业务分类】单页纵向展示（不使用选项卡），
    /// 每个分类用一个分隔标题条（SunnyUI UILine）隔开，下面紧跟该分类的配置表格：
    /// - 设置名称（配置项 key，只读）
    /// - 说明（每个配置项的中文含义，只读）
    /// - 设置值（可直接编辑输入）
    ///
    /// 分类（见 _categories）：
    ///   基础配置 / 气压表串口通讯 / IO耦合器（Modbus TCP）/ 气压表寄存器 /
    ///   报警参数 / 冷却送风机 / 老化测试业务 / 扫码枪
    ///
    /// 内容整体放在一个可滚动面板（pnlScroll）里，页面内容超高时自动出现滚动条，
    /// 所有分类一眼看全，不用来回切页签。
    ///
    /// 点击【保存设置】后，把所有改动写回程序运行目录下的 exe.config
    /// （即程序实际读取的配置文件，与 App.config 同源），
    /// 写完后刷新 appSettings 缓存。由于设备参数在程序启动时一次性加载，
    /// 修改后需重启程序才生效。
    ///
    /// 【实现要点】
    /// - 分类与 key 顺序在 _categories 中集中维护，分类标题条 + 表格在 SetupSections 里
    ///   动态创建、在 LayoutSections 里按 Y 坐标依次排布，表格高度按可见行行高之和自动计算。
    ///   新增配置项只需在 _descriptions 和 _categories 里各加一行，无需改界面布局
    /// - 表格三列均启用内容换行，行高按内容（TextRenderer 测量换行高度）在 LayoutSections 中
    ///   逐行计算，保证说明 / 设置值等长文本全部显示不被截断
    /// - 搜索过滤：无匹配的分类不参与排布（_sectionVisible 状态数组，不用控件 Visible——
    ///   窗体未显示时控件 Visible 恒为 false），网格高度仅统计可见行，避免结果区留大片空白
    /// - 界面使用 SunnyUI 控件（UILine 分类标题 / UIDataGridView 表格 / UIButton 按钮）
    ///   呈现，风格与主程序一致、观感更佳
    /// - 保存前按配置项类型做合法性校验（整数/小数/布尔/十六进制地址），
    ///   不合法项会整批拦截并列出，避免写坏配置文件
    /// - 使用 System.Configuration.ConfigurationManager.OpenExeConfiguration
    ///   读写 exe.config，配置值来源为运行时的 ConfigurationManager.AppSettings，
    ///   与程序启动加载的取值完全一致
    /// </summary>
    public partial class SettingsForm : Form
    {
        /// <summary>
        /// 当前程序正在使用的设备配置（用于取当前生效值做兜底、以及按属性类型校验）
        /// </summary>
        private readonly DeviceConfig _config;

        /// <summary>
        /// 每个分类的界面元素：分类标题条 + 该分类的配置表格
        /// 与 _categories 顺序一一对应，保存时遍历全部表格收集用户修改
        /// </summary>
        private readonly List<KeyValuePair<Sunny.UI.UILine, Sunny.UI.UIDataGridView>> _sections =
            new List<KeyValuePair<Sunny.UI.UILine, Sunny.UI.UIDataGridView>>();

        /// <summary>搜索框控件</summary>
        private Sunny.UI.UITextBox _txtSearch;
        /// <summary>清除搜索按钮</summary>
        private Button _btnClearSearch;

        /// <summary>
        /// 每个分类是否显示（搜索过滤时置为是否有匹配行）。
        /// 不用控件 Visible 判断：窗体尚未显示时控件 Visible 恒为 false，会导致初始布局错乱。
        /// </summary>
        private bool[] _sectionVisible;

        /// <summary>
        /// 需要限定为 true/false 下拉选择的配置项（防输入错误，只能选其一）
        /// </summary>
        private static readonly HashSet<string> _boolKeys = new HashSet<string>
        {
            "UseMockCommunication",
            "InvertInputs",
            "InvertOutputs",
            "IoBackupChannelMappingEnabled",
            "AlarmWhenPressureHigherThanThreshold",
            "FanEnabled",
            "FanAutoDetectEnabled",
            "UseDiAlarmContact",
            "ScannerEnabled",
            "ScannerDebugLog",
        };

        /// <summary>
        /// 数字类配置项的范围约束（防输入越界/乱输），保存前仍会按 ValidateValue 二次校验。
        /// </summary>
        private static readonly Dictionary<string, (decimal Min, decimal Max, int Decimals, decimal Increment)> _numericKeys =
            new Dictionary<string, (decimal, decimal, int, decimal)>
            {
                { "TotalBarometers", (1, 999, 0, 1) },
                { "TotalInputs", (1, 999, 0, 1) },
                { "TotalOutputs", (1, 999, 0, 1) },
                { "CollectInterval", (10, 60000, 0, 10) },
                { "PanelColumns", (1, 100, 0, 1) },
                { "PanelRows", (1, 100, 0, 1) },

                { "SerialReadTimeoutMs", (10, 60000, 0, 10) },
                { "SerialWriteTimeoutMs", (10, 60000, 0, 10) },

                { "PlcPort", (1, 65535, 0, 1) },
                { "IoUnitId", (1, 255, 0, 1) },
                { "TcpSendTimeoutMs", (10, 60000, 0, 10) },
                { "TcpReceiveTimeoutMs", (10, 60000, 0, 10) },

                { "BarometerDefaultDecimalPlaces", (0, 4, 0, 1) },
                { "BarometerPressureScale", (-100, 100, 3, 0.1m) },

                { "AlarmPressureThresholdKPa", (-200, 200, 2, 0.5m) },

                { "FanPort", (1, 65535, 0, 1) },
                { "FanUnitId", (1, 255, 0, 1) },
                { "FanTimeoutMs", (10, 60000, 0, 10) },

                { "VacuumConfirmTimeoutMs", (100, 600000, 0, 100) },
                { "CommunicationLossAlarmCount", (1, 10000, 0, 1) },
                { "MaxTestDurationSeconds", (0, 86400, 0, 10) },
                { "FanTempAlarmLimitC", (0, 200, 1, 0.5m) },
            };

        /// <summary>
        /// 配置项说明字典（key → 中文说明），显示在表格"说明"列
        /// 覆盖 App.config 全部配置项，key 必须与 _categories 中用到的 key 一致
        /// </summary>
        private readonly Dictionary<string, string> _descriptions = new Dictionary<string, string>
        {
            // ===== 基础配置 =====
            { "TotalBarometers", "气压表总数（当前 72）" },
            { "TotalInputs", "IO 输入总数（当前 80）" },
            { "TotalOutputs", "IO 输出总数（当前 160）" },
            { "CollectInterval", "数据采集间隔（毫秒）" },
            { "PanelColumns", "主视图每行显示的气压表数量（列数）" },
            { "PanelRows", "主视图每列显示的气压表数量（行数）" },

            // ===== 气压表串口通讯 =====
            { "PortName", "气压表通信端口（如 COM9，留空则启动时自动识别 CH340）" },
            { "BaudRate", "气压表波特率（19200）" },
            { "DataBits", "数据位（8）" },
            { "StopBits", "停止位（1）" },
            { "Parity", "校验位（None）" },
            { "SerialReadTimeoutMs", "串口读取超时（毫秒）" },
            { "SerialWriteTimeoutMs", "串口写入超时（毫秒）" },
            { "UseMockCommunication", "是否使用模拟通讯（true=不接硬件用假数据）" },

            // ===== IO耦合器（Modbus TCP）与 TCP 超时 =====
            { "PlcAddress", "PLC / IO 耦合器 IP（如 192.168.1.20）" },
            { "PlcPort", "PLC 通讯端口（502）" },
            { "IoUnitId", "IO 耦合器从站地址（UnitId，默认 1）" },
            { "IoInputRegisterStartAddress", "IO 输入寄存器起始地址（十六进制，如 0x1000）" },
            { "IoOutputRegisterStartAddress", "IO 输出寄存器起始地址（十六进制，如 0x2000）" },
            { "InvertInputs", "输入点逻辑是否取反（false/true）" },
            { "InvertOutputs", "输出点逻辑是否取反（false/true）" },
            { "IoBackupChannelMappingEnabled", "是否启用 IO 输出备用通道映射（false/true）" },
            { "IoBackupChannelMappings", "IO 备用通道映射表（格式：0x2000@0->0x2009@10;0x2008@0->0x2009@11）" },
            { "TcpSendTimeoutMs", "TCP 发送超时（毫秒，耦合器/送风机通用）" },
            { "TcpReceiveTimeoutMs", "TCP 接收超时（毫秒，耦合器/送风机通用）" },

            // ===== 气压表寄存器 =====
            { "BarometerPressureRegisterAddress", "气压表压力寄存器地址（0x0001）" },
            { "BarometerDefaultDecimalPlaces", "气压表小数位（务必与仪表实际一致，当前 1）" },
            { "BarometerPressureScale", "压力缩放系数（读数 × 该值）" },

            // ===== 报警参数 =====
            { "AlarmPressureThresholdKPa", "报警压力阈值（kPa，如 -95）" },
            { "AlarmWhenPressureHigherThanThreshold", "报警方向（true=压力高于阈值时报警）" },

            // ===== 冷却送风机 =====
            { "FanEnabled", "是否启用冷却送风机（false/true）" },
            { "FanIpAddress", "送风机控制屏 IP（如 192.168.1.220）" },
            { "FanAutoDetectEnabled", "送风机 IP 自动识别开关（false/true）" },
            { "FanIpCandidates", "送风机候选 IP 列表（逗号分隔，如 192.168.1.220,192.168.1.221）" },
            { "FanPort", "送风机通讯端口（50000）" },
            { "FanUnitId", "送风机从站地址（默认 1）" },
            { "FanTimeoutMs", "送风机通讯超时（毫秒）" },

            // ===== 老化测试业务 =====
            { "VacuumConfirmTimeoutMs", "真空建立确认超时（毫秒，默认 15000）" },
            { "CommunicationLossAlarmCount", "通讯故障报警阈值（连续读取失败 N 次）" },
            { "MaxTestDurationSeconds", "老化测试最大时长（秒，0=不限时手动停止）" },
            { "UseDiAlarmContact", "气压表报警触点(DI)是否并入报警判定（false/true）" },
            { "FanTempAlarmLimitC", "送风机温度告警上限（°C，0=不启用）" },

            // ===== 扫码枪 =====
            { "ScannerEnabled", "是否启用扫码枪（false/true）" },
            { "ScannerPort", "扫码枪固定串口（留空则按关键词自动识别）" },
            { "ScannerDeviceKeyword", "扫码枪设备识别关键词（如 Xenon 1902）" },
            { "ScannerBaudRate", "扫码枪波特率（115200）" },
            { "ScannerDataBits", "扫码枪数据位（8）" },
            { "ScannerStopBits", "扫码枪停止位（1）" },
            { "ScannerParity", "扫码枪校验位（None）" },
            { "ScannerDebugLog", "扫码枪心跳调试日志开关（false/true）" },
        };

        /// <summary>
        /// 分类定义：页签标题 → 该分类下的配置项 key 列表（按显示顺序）
        ///
        /// 分组逻辑（与 App.config 注释分组一致）：
        /// - 基础配置：设备数量 / 采集间隔 / 面板布局
        /// - 气压表串口通讯：串口参数 / 超时 / Mock 开关
        /// - IO耦合器：IP / 端口 / 从站地址 / 寄存器地址 / 逻辑取反 / 备用通道映射 / TCP 超时
        /// - 气压表寄存器：压力寄存器 / 小数位 / 缩放系数
        /// - 报警参数：压力报警阈值与方向
        /// - 冷却送风机：启用 / IP 自动识别 / 端口 / 超时
        /// - 老化测试业务：真空确认 / 失联报警 / 最大时长 / DI 触点 / 温度告警
        /// - 扫码枪：启用 / 端口识别 / 串口参数 / 调试日志
        ///
        /// 【新增配置项】只需：①在 _descriptions 加说明；②在本数组对应分类的 Keys 里加 key
        /// </summary>
        private readonly (string Title, string[] Keys)[] _categories = new (string Title, string[] Keys)[]
        {
            ("基础配置", new string[]
            {
                "TotalBarometers", "TotalInputs", "TotalOutputs",
                "CollectInterval", "PanelColumns", "PanelRows"
            }),
            ("气压表串口通讯", new string[]
            {
                "PortName", "BaudRate", "DataBits", "StopBits", "Parity",
                "SerialReadTimeoutMs", "SerialWriteTimeoutMs", "UseMockCommunication"
            }),
            ("IO耦合器（Modbus TCP）", new string[]
            {
                "PlcAddress", "PlcPort", "IoUnitId",
                "IoInputRegisterStartAddress", "IoOutputRegisterStartAddress",
                "InvertInputs", "InvertOutputs",
                "IoBackupChannelMappingEnabled", "IoBackupChannelMappings",
                "TcpSendTimeoutMs", "TcpReceiveTimeoutMs"
            }),
            ("气压表寄存器", new string[]
            {
                "BarometerPressureRegisterAddress",
                "BarometerDefaultDecimalPlaces", "BarometerPressureScale"
            }),
            ("报警参数", new string[]
            {
                "AlarmPressureThresholdKPa", "AlarmWhenPressureHigherThanThreshold"
            }),
            ("冷却送风机", new string[]
            {
                "FanEnabled", "FanIpAddress", "FanAutoDetectEnabled", "FanIpCandidates",
                "FanPort", "FanUnitId", "FanTimeoutMs"
            }),
            ("老化测试业务", new string[]
            {
                "VacuumConfirmTimeoutMs", "CommunicationLossAlarmCount",
                "MaxTestDurationSeconds", "UseDiAlarmContact", "FanTempAlarmLimitC"
            }),
            ("扫码枪", new string[]
            {
                "ScannerEnabled", "ScannerPort", "ScannerDeviceKeyword",
                "ScannerBaudRate", "ScannerDataBits", "ScannerStopBits", "ScannerParity",
                "ScannerDebugLog"
            }),
        };

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="config">当前生效的设备配置（主窗体传入，用于取兜底值与类型校验）</param>
        public SettingsForm(DeviceConfig config)
        {
            InitializeComponent();
            _config = config;

            // 按分类创建分隔标题和表格，再填充数据并排布版面
            SetupSections();
            LoadSettings();
            LayoutSections();

            // 创建搜索框（位于布局顶部）
            SetupSearchBox();
        }

        /// <summary>
        /// 按 _categories 逐个创建“分类标题条（UILine）+ 配置表格（UIDataGridView）”
        /// 全部添加到可滚动面板 pnlScroll 中，并把表格记录到 _sections，供加载 / 保存遍历
        /// </summary>
        private void SetupSections()
        {
            _sections.Clear();
            pnlScroll.Controls.Clear();

            foreach (var category in _categories)
            {
                // 分类标题分隔条：一段文字 + 一条水平线，起分组标题作用
                var line = new Sunny.UI.UILine
                {
                    Text = category.Title,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Font = new Font(this.Font.FontFamily, 10F, FontStyle.Bold),
                    ForeColor = Color.FromArgb(48, 119, 238),
                    LineColor = Color.FromArgb(48, 119, 238),
                    TextInterval = 12
                };
                pnlScroll.Controls.Add(line);

                // 该分类的配置表格
                Sunny.UI.UIDataGridView grid = CreateGrid();
                pnlScroll.Controls.Add(grid);

                _sections.Add(new KeyValuePair<Sunny.UI.UILine, Sunny.UI.UIDataGridView>(line, grid));
            }

            // 每节分类的显示状态（搜索过滤用）：初始全部显示
            _sectionVisible = new bool[_sections.Count];
            for (int i = 0; i < _sectionVisible.Length; i++)
            {
                _sectionVisible[i] = true;
            }
        }

        /// <summary>
        /// 创建一个配置表格（SunnyUI UIDataGridView），三列：设置名称 / 说明 / 设置值
        /// 名称列和说明列只读，设置值列可编辑（用户输入新值）
        /// </summary>
        private Sunny.UI.UIDataGridView CreateGrid()
        {
            var grid = new Sunny.UI.UIDataGridView
            {
                Style = Sunny.UI.UIStyle.Blue,
                AllowUserToAddRows = false,          // 不允许用户新增行
                AllowUserToDeleteRows = false,       // 不允许用户删除行
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None,
                RowTemplate = { Height = 24 },
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.None,
                ScrollBars = ScrollBars.None
            };

            // 三列表头
            grid.Columns.Add("colKey", "设置名称");
            grid.Columns.Add("colDesc", "说明");
            grid.Columns.Add("colValue", "设置值");

            // 名称 / 说明列只读
            grid.Columns["colKey"].ReadOnly = true;
            grid.Columns["colDesc"].ReadOnly = true;

            // 列宽分配：名称 260 + 说明 430 + 设置值占满剩余宽度
            grid.Columns["colKey"].Width = 260;
            grid.Columns["colKey"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns["colDesc"].Width = 430;
            grid.Columns["colDesc"].AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
            grid.Columns["colValue"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            // 三列内容显示不下时自动换行（行高按内容在 LayoutSections 里自动计算，保证内容全部显示）
            grid.Columns["colKey"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.Columns["colDesc"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.Columns["colValue"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;

            // 单元格统一样式：白底深字，避免下拉框/数字框出现系统灰色底
            grid.DefaultCellStyle.BackColor = Color.White;
            grid.DefaultCellStyle.ForeColor = Color.FromArgb(48, 48, 48);
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(48, 119, 238);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.EnableHeadersVisualStyles = false;
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(237, 243, 253);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(48, 48, 48);
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Color.FromArgb(237, 243, 253);
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = Color.FromArgb(48, 48, 48);
            grid.ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing;

            // 可手输下拉（波特率）允许输入列表外的自定义值：捕获校验异常，把新值补进列表后提交
            grid.DataError += Grid_DataError;

            return grid;
        }

        /// <summary>
        /// 处理 DataGridView 数据错误：波特率下拉允许手输自定义值，
        /// 输入不在列表里的波特率时自动补进 Items 并接受该值，而不是弹出错误
        /// </summary>
        private void Grid_DataError(object sender, DataGridViewDataErrorEventArgs e)
        {
            var grid = sender as DataGridView;
            if (grid != null && e.RowIndex >= 0 && e.ColumnIndex >= 0)
            {
                DataGridViewCell cell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                if (cell is DataGridViewComboBoxCell combo && grid.EditingControl is ComboBox editing)
                {
                    string typed = editing.Text;
                    if (!string.IsNullOrWhiteSpace(typed) && !combo.Items.Contains(typed))
                    {
                        combo.Items.Add(typed);
                        cell.Value = typed;
                        e.ThrowException = false;
                        return;
                    }
                }
            }
            e.ThrowException = false;
        }

        /// <summary>
        /// 把全部配置项按分类填入各分类表格
        /// 值取运行时的 ConfigurationManager.AppSettings（与程序启动读取一致），
        /// 缺失时用程序当前生效值（DeviceConfig 属性）兜底
        /// </summary>
        private void LoadSettings()
        {
            for (int i = 0; i < _categories.Length; i++)
            {
                Sunny.UI.UIDataGridView grid = _sections[i].Value;
                grid.Rows.Clear();

                foreach (string key in _categories[i].Keys)
                {
                    // 说明列：取不到说明（配置被移除）时显示为空，不阻塞加载
                    _descriptions.TryGetValue(key, out string desc);

                    int rowIdx = grid.Rows.Add();
                    grid.Rows[rowIdx].Cells["colKey"].Value = key;
                    grid.Rows[rowIdx].Cells["colDesc"].Value = desc ?? "";
                    grid.Rows[rowIdx].Cells["colValue"] = CreateValueCell(key, GetEffectiveValue(key));
                }
            }
        }

        /// <summary>
        /// 单页纵向排布：按 Y 坐标依次放置每个分类的标题条和表格，
        /// 表格高度按自身行数自动计算，最后设置滚动面板的内容总高
        /// </summary>
        private void LayoutSections()
        {
            const int margin = 14;    // 左右留白
            const int gap = 14;       // 分类之间的垂直间距
            int y = margin + 36;      // 36 = 搜索框面板高度
            int width = pnlScroll.ClientSize.Width - margin * 2;
            if (width < 100) width = 100;

            for (int i = 0; i < _sections.Count; i++)
            {
                Sunny.UI.UILine line = _sections[i].Key;
                Sunny.UI.UIDataGridView grid = _sections[i].Value;

                // 被搜索过滤掉的分类不参与排布，避免在结果上方留下大片空白
                if (!_sectionVisible[i])
                {
                    continue;
                }

                // 标题条：占满可用宽度
                line.Location = new Point(margin, y);
                line.Width = width;
                line.Height = 28;
                y += line.Height + 4;

                // 表格：占满可用宽度。先定宽（设置值列按剩余宽度自适应），
                // 再按内容换行计算每个可见行的行高，表格高度 = 表头 + 可见行高之和 + 边距
                grid.Location = new Point(margin, y);
                grid.Width = width;

                int totalRowHeight = 0;
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (!row.Visible) continue;
                    row.Height = ComputeRowHeight(grid, row);
                    totalRowHeight += row.Height;
                }
                int gridHeight = grid.ColumnHeadersHeight + totalRowHeight + 2;
                grid.Height = gridHeight;
                y += gridHeight + gap;
            }

            // 内容总高（超出可视区时 pnlScroll 自动出现滚动条）
            pnlScroll.AutoScrollMinSize = new Size(0, y + margin);
        }

        /// <summary>
        /// 按单元格内容换行后的实际行高计算（三列取最大值，保证内容全部显示不被截断）
        /// 换行宽度按列宽减去左右内边距计算，行高 = 文本高度 + 上下内边距，最小不低于默认行高 24。
        /// </summary>
        /// <param name="grid">所属表格（取列宽与字体）</param>
        /// <param name="row">要计算的行</param>
        /// <returns>该行应设置的像素高度</returns>
        private static int ComputeRowHeight(DataGridView grid, DataGridViewRow row)
        {
            const int xPadding = 8;   // 左右内边距（从换行宽度中扣除）
            const int yPadding = 3;   // 上下内边距（加到文本高度上）
            int maxTextHeight = 1;

            foreach (DataGridViewCell cell in row.Cells)
            {
                string text = cell.Value?.ToString() ?? "";
                if (text.Length == 0) continue;

                int colWidth = grid.Columns[cell.ColumnIndex].Width - xPadding;
                if (colWidth < 10) colWidth = 10;

                Size textSize = TextRenderer.MeasureText(text, grid.Font,
                    new Size(colWidth, 0),
                    TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl | TextFormatFlags.NoPadding);
                if (textSize.Height > maxTextHeight) maxTextHeight = textSize.Height;
            }

            return Math.Max(maxTextHeight + yPadding, 24);
        }

        /// <summary>
        /// 在滚动面板顶部创建搜索框，用于快速过滤配置项
        /// </summary>
        private void SetupSearchBox()
        {
            var pnlSearch = new Panel
            {
                Location = new Point(0, 0),
                Height = 36,
                Width = pnlScroll.ClientSize.Width,
                BackColor = Color.FromArgb(245, 248, 255)
            };

            var lblSearch = new Label
            {
                Text = "搜索配置项：",
                Location = new Point(14, 5),
                Size = new Size(110, 26),
                TextAlign = ContentAlignment.MiddleLeft,
                Font = new Font(this.Font.FontFamily, 11F, FontStyle.Bold),
                ForeColor = Color.FromArgb(48, 119, 238)
            };
            pnlSearch.Controls.Add(lblSearch);

            _txtSearch = new Sunny.UI.UITextBox
            {
                Location = new Point(124, 5),
                Size = new Size(320, 26),
                Font = new Font(this.Font.FontFamily, 11F),
                Watermark = "输入关键字过滤配置项"
            };
            _txtSearch.TextChanged += TxtSearch_TextChanged;
            pnlSearch.Controls.Add(_txtSearch);

            _btnClearSearch = new Button
            {
                Text = "✕",
                Location = new Point(448, 5),
                Size = new Size(26, 26),
                FlatStyle = FlatStyle.Flat,
                TabStop = false
            };
            _btnClearSearch.Click += (s, e) => { _txtSearch.Clear(); _txtSearch.Focus(); };
            pnlSearch.Controls.Add(_btnClearSearch);

            pnlScroll.Controls.Add(pnlSearch);
        }

        private void TxtSearch_TextChanged(object sender, EventArgs e)
        {
            ApplySearchFilter();
        }

        private void ApplySearchFilter()
        {
            string keyword = _txtSearch.Text.Trim();
            bool hasKeyword = !string.IsNullOrEmpty(keyword);

            for (int i = 0; i < _categories.Length; i++)
            {
                var section = _sections[i];
                Sunny.UI.UILine line = section.Key;
                Sunny.UI.UIDataGridView grid = section.Value;

                if (!hasKeyword)
                {
                    // 无搜索关键字 → 显示全部
                    line.Visible = true;
                    grid.Visible = true;
                    _sectionVisible[i] = true;
                    foreach (DataGridViewRow row in grid.Rows)
                    {
                        row.Visible = true;
                    }
                    continue;
                }

                // 按关键字过滤行
                bool anyVisible = false;
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow) continue;

                    string key = row.Cells["colKey"].Value?.ToString() ?? "";
                    string desc = row.Cells["colDesc"].Value?.ToString() ?? "";
                    bool match = key.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0
                              || desc.IndexOf(keyword, StringComparison.OrdinalIgnoreCase) >= 0;
                    row.Visible = match;
                    if (match) anyVisible = true;
                }

                // 隐藏无匹配行的分类
                line.Visible = anyVisible;
                grid.Visible = anyVisible;
                _sectionVisible[i] = anyVisible;
            }

            LayoutSections();
        }

        /// <summary>
        /// 获取配置项的当前值
        /// 优先读 ConfigurationManager.AppSettings（与程序启动读取一致）；
        /// 若配置里没有该键，则用内存中 DeviceConfig 的属性值兜底
        /// </summary>
        private string GetEffectiveValue(string key)
        {
            string raw = System.Configuration.ConfigurationManager.AppSettings[key];
            if (raw != null) return raw;

            var prop = _config.GetType().GetProperty(key);
            if (prop != null)
            {
                object value = prop.GetValue(_config, null);
                if (value != null) return value.ToString();
            }
            return "";
        }

        /// <summary>
        /// 根据配置项类型创建"设置值"单元格控件，防止用户乱输导致配置写坏：
        /// - 布尔项（_boolKeys）：下拉框只允许选择 true / false
        /// - PortName：下拉框列出系统当前检测到的所有串口，供用户直接选择
        /// - 串口通讯参数：波特率用可手输下拉（常用档位 + 自定义），数据位/停止位/校验位用固定选项下拉
        /// - 数字项（_numericKeys）：用 NumericUpDown 单元格，按范围限制上下限与小数位
        /// - 其余文本项：普通文本框
        /// </summary>
        private static DataGridViewCell CreateValueCell(string key, string value)
        {
            if (_boolKeys.Contains(key))
            {
                return CreateStrictComboCell(
                    new[] { "false", "true" },
                    value != null && value.Trim().Equals("true", StringComparison.OrdinalIgnoreCase) ? "true" : "false");
            }

            if (key == "PortName" || key == "ScannerPort")
            {
                return CreatePortComboCell(value);
            }

            // 串口通讯参数：波特率（含扫码枪）用可手输下拉，支持自定义波特率
            if (key == "BaudRate" || key == "ScannerBaudRate")
            {
                return CreateBaudComboCell(value);
            }

            if (key == "DataBits" || key == "ScannerDataBits")
            {
                return CreateStrictComboCell(new[] { "5", "6", "7", "8" }, value);
            }

            if (key == "StopBits" || key == "ScannerStopBits")
            {
                // 显示 1 / 1.5 / 2，配置存 1 / 15 / 2（15 表示 1.5，与 ScannerService 约定一致）
                return CreateOptionComboCell(
                    new[]
                    {
                        new ComboOption("1", "1"),
                        new ComboOption("1.5", "15"),
                        new ComboOption("2", "2"),
                    },
                    NormalizeStopBits(value));
            }

            if (key == "Parity" || key == "ScannerParity")
            {
                // 界面显示中文，实际存值映射为标准枚举名（None/Odd/Even/Mark/Space），
                // 保证配置文件里只有这 5 种合法值，杜绝非法字符导致下游解析失败
                return CreateOptionComboCell(
                    new[]
                    {
                        new ComboOption("无校验(NONE)", "None"),
                        new ComboOption("奇校验(ODD)", "Odd"),
                        new ComboOption("偶校验(EVEN)", "Even"),
                        new ComboOption("1校验(MARK)", "Mark"),
                        new ComboOption("空格校验(SPACE)", "Space"),
                    },
                    NormalizeParity(value));
            }

            if (_numericKeys.TryGetValue(key, out var range))
            {
                var cell = new DataGridViewNumericUpDownCell
                {
                    Minimum = range.Min,
                    Maximum = range.Max,
                    DecimalPlaces = range.Decimals,
                    Increment = range.Increment
                };

                decimal parsed;
                if (decimal.TryParse(value, out parsed))
                {
                    parsed = Math.Max(range.Min, Math.Min(range.Max, parsed));
                }
                else
                {
                    parsed = Math.Max(range.Min, Math.Min(range.Max, 0));
                }
                cell.Value = parsed;
                return cell;
            }

            var textCell = new DataGridViewTextBoxCell();
            textCell.Value = value;
            return textCell;
        }

        /// <summary>把停止位配置值规整为下拉项实际保存的值（1.5 或 15 都统一为 15）</summary>
        private static string NormalizeStopBits(string value)
        {
            value = (value ?? "").Trim();
            if (value == "1.5" || value == "15") return "15";
            if (value == "2") return "2";
            return "1";
        }

        /// <summary>
        /// 把校验位配置值映射为标准枚举名（None/Odd/Even/Mark/Space）。
        /// 兼容历史写法（小写 none/odd、中文"无校验"、缩写等），
        /// 任何非法/未知值都归一到 None，保证存值只有这 5 种合法枚举名。
        /// </summary>
        private static string NormalizeParity(string value)
        {
            string v = (value ?? "").Trim();
            switch (v.ToLowerInvariant())
            {
                case "none":
                case "n":
                case "无校验":
                    return "None";
                case "odd":
                case "o":
                case "奇校验":
                    return "Odd";
                case "even":
                case "e":
                case "偶校验":
                    return "Even";
                case "mark":
                case "m":
                case "1校验":
                case "标记":
                    return "Mark";
                case "space":
                case "s":
                case "空格校验":
                    return "Space";
                default:
                    return "None";
            }
        }

        /// <summary>
        /// 固定选项下拉单元格（DropDownList，只能选不能手输）。
        /// 选项为纯字符串，当前值不在列表时补一项避免空显示。
        /// </summary>
        private static DataGridViewStrictComboBoxCell CreateStrictComboCell(IEnumerable<string> items, string currentValue)
        {
            var cell = new DataGridViewStrictComboBoxCell();
            StyleComboCell(cell);
            foreach (string item in items)
            {
                if (!cell.Items.Contains(item)) cell.Items.Add(item);
            }
            if (!string.IsNullOrEmpty(currentValue) && !cell.Items.Contains(currentValue))
            {
                cell.Items.Add(currentValue);
            }
            if (!string.IsNullOrEmpty(currentValue))
            {
                cell.Value = currentValue;
            }
            return cell;
        }

        /// <summary>
        /// 固定选项下拉单元格（DropDownList），选项为"显示文本/实际保存值"，
        /// 用于停止位（显示 1.5 存 15）、校验位（显示中文存枚举名）。
        /// </summary>
        private static DataGridViewStrictComboBoxCell CreateOptionComboCell(ComboOption[] options, string currentValue)
        {
            var cell = new DataGridViewStrictComboBoxCell();
            StyleComboCell(cell);
            foreach (ComboOption option in options)
            {
                cell.Items.Add(option);
            }
            cell.DisplayMember = "Display";
            cell.ValueMember = "Value";

            if (!string.IsNullOrEmpty(currentValue))
            {
                bool found = false;
                foreach (ComboOption option in cell.Items)
                {
                    if (option.Value == currentValue) { found = true; break; }
                }
                if (!found)
                {
                    cell.Items.Add(new ComboOption(currentValue, currentValue));
                }
                cell.Value = currentValue;
            }
            return cell;
        }

        /// <summary>串口下拉单元格（DropDownList）：列出系统检测到的所有串口</summary>
        private static DataGridViewStrictComboBoxCell CreatePortComboCell(string currentValue)
        {
            var cell = new DataGridViewStrictComboBoxCell();
            StyleComboCell(cell);
            cell.DropDownWidth = 220;

            string[] ports = SerialPortHelper.GetAllPortNames();
            foreach (string port in ports)
            {
                if (!cell.Items.Contains(port)) cell.Items.Add(port);
            }

            // 当前值不在检测列表里也保留，避免已配置但当前未插的端口被误清
            if (!string.IsNullOrEmpty(currentValue) && !cell.Items.Contains(currentValue))
            {
                cell.Items.Add(currentValue);
            }

            // 留空 = 启动时自动识别，因此空值保持空（下拉框显示空白待选），不强制选第一个
            if (!string.IsNullOrEmpty(currentValue))
            {
                cell.Value = currentValue;
            }
            return cell;
        }

        /// <summary>波特率下拉单元格（可手输）：列出常用档位，也支持输入自定义波特率</summary>
        private static DataGridViewEditableComboBoxCell CreateBaudComboCell(string currentValue)
        {
            var cell = new DataGridViewEditableComboBoxCell();
            StyleComboCell(cell);

            string[] rates =
            {
                "110", "300", "600", "1200", "2400", "4800",      // 低速档
                "9600", "19200", "38400", "57600",                 // 中速档
                "115200", "230400", "460800", "921600"             // 高速档
            };
            foreach (string rate in rates)
            {
                if (!cell.Items.Contains(rate)) cell.Items.Add(rate);
            }

            // 当前值不在列表里也补一项，便于回显自定义波特率
            if (!string.IsNullOrEmpty(currentValue) && !cell.Items.Contains(currentValue))
            {
                cell.Items.Add(currentValue);
            }
            cell.Value = currentValue;
            return cell;
        }

        /// <summary>下拉单元格统一样式：扁平无灰底、白底深字，与页面风格一致</summary>
        private static void StyleComboCell(DataGridViewComboBoxCell cell)
        {
            cell.FlatStyle = FlatStyle.Flat;
            cell.DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton;
            cell.Style.BackColor = Color.White;
            cell.Style.ForeColor = Color.FromArgb(48, 48, 48);
            cell.Style.SelectionBackColor = Color.FromArgb(48, 119, 238);
            cell.Style.SelectionForeColor = Color.White;
        }

        /// <summary>
        /// 按配置项类型校验用户输入的值是否合法
        /// </summary>
        /// <param name="key">配置项名称</param>
        /// <param name="value">用户输入值（已 Trim）</param>
        /// <param name="error">校验失败时的中文提示</param>
        /// <returns>true=合法，false=不合法</returns>
        private static bool ValidateValue(string key, string value, out string error)
        {
            error = null;

            // 自由文本/列表类配置项（IP、端口映射表等），不强制校验
            switch (key)
            {
                case "FanIpCandidates":
                case "IoBackupChannelMappings":
                    return true;
            }

            switch (key)
            {
                // 整数
                case "TotalBarometers":
                case "TotalInputs":
                case "TotalOutputs":
                case "CollectInterval":
                case "PanelColumns":
                case "PanelRows":
                case "BaudRate":
                case "DataBits":
                case "StopBits":
                case "SerialReadTimeoutMs":
                case "SerialWriteTimeoutMs":
                case "TcpSendTimeoutMs":
                case "TcpReceiveTimeoutMs":
                case "BarometerDefaultDecimalPlaces":
                case "PlcPort":
                case "FanPort":
                case "FanTimeoutMs":
                case "VacuumConfirmTimeoutMs":
                case "CommunicationLossAlarmCount":
                case "MaxTestDurationSeconds":
                case "ScannerBaudRate":
                case "ScannerDataBits":
                case "ScannerStopBits":
                    if (!int.TryParse(value, out _)) { error = "应为整数"; return false; }
                    return true;

                // 字节（0~255）
                case "IoUnitId":
                case "FanUnitId":
                    if (!byte.TryParse(value, out _)) { error = "应为 0~255 的整数"; return false; }
                    return true;

                // 寄存器地址（支持十进制或十六进制 0x 写法）
                case "IoInputRegisterStartAddress":
                case "IoOutputRegisterStartAddress":
                case "BarometerPressureRegisterAddress":
                    if (!TryParseUShort(value)) { error = "应为数字或十六进制（如 0x1000）"; return false; }
                    return true;

                // 小数（decimal / float）
                case "BarometerPressureScale":
                case "AlarmPressureThresholdKPa":
                    if (!decimal.TryParse(value, out _)) { error = "应为数字"; return false; }
                    return true;
                case "FanTempAlarmLimitC":
                    if (!float.TryParse(value, out _)) { error = "应为数字"; return false; }
                    return true;

                // 布尔
                case "UseMockCommunication":
                case "InvertInputs":
                case "InvertOutputs":
                case "IoBackupChannelMappingEnabled":
                case "AlarmWhenPressureHigherThanThreshold":
                case "FanEnabled":
                case "FanAutoDetectEnabled":
                case "UseDiAlarmContact":
                case "ScannerEnabled":
                case "ScannerDebugLog":
                    if (!bool.TryParse(value, out _)) { error = "应为 true 或 false"; return false; }
                    return true;

                // 其余为字符串类（端口名、IP、关键词、校验位等），不做强制校验
                default:
                    return true;
            }
        }

        /// <summary>
        /// 解析 ushort（支持 "4096" 或 "0x1000" 两种写法）
        /// </summary>
        private static bool TryParseUShort(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return false;
            if (value.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            {
                return ushort.TryParse(value.Substring(2), System.Globalization.NumberStyles.HexNumber,
                    System.Globalization.CultureInfo.InvariantCulture, out _);
            }
            return ushort.TryParse(value, out _);
        }

        /// <summary>
        /// "保存设置"按钮点击事件
        ///
        /// 【流程】
        /// 1. 遍历全部分类表格，收集每行的 key / 值
        /// 2. 按类型校验每个值，不合法项整批拦截并列出（避免写坏配置文件）
        /// 3. 写回 exe.config 的 appSettings（OpenExeConfiguration + Save）
        /// 4. 刷新 appSettings 缓存，提示重启生效
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            var changes = new Dictionary<string, string>();
            var invalid = new List<string>();

            // 遍历每个分类下的配置表格
            foreach (var section in _sections)
            {
                DataGridView grid = section.Value;
                foreach (DataGridViewRow row in grid.Rows)
                {
                    if (row.IsNewRow) continue;

                    string key = row.Cells["colKey"].Value?.ToString();
                    if (string.IsNullOrWhiteSpace(key)) continue;

                    string value = (row.Cells["colValue"].Value?.ToString() ?? "").Trim();
                    if (!ValidateValue(key, value, out string error))
                    {
                        invalid.Add($"【{key}】 {value}  →  {error}");
                        continue;
                    }
                    changes[key] = value;
                }
            }

            if (invalid.Count > 0)
            {
                MessageBox.Show("以下配置值不合法，请修改后再保存：\r\n\r\n" +
                    string.Join("\r\n", invalid),
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 写回配置文件（程序运行目录下的 exe.config，与 App.config 同源）
            try
            {
                var config = System.Configuration.ConfigurationManager.OpenExeConfiguration(
                    System.Configuration.ConfigurationUserLevel.None);

                foreach (var kv in changes)
                {
                    var setting = config.AppSettings.Settings[kv.Key];
                    if (setting == null)
                    {
                        config.AppSettings.Settings.Add(kv.Key, kv.Value);
                    }
                    else
                    {
                        setting.Value = kv.Value;
                    }
                }

                config.Save(System.Configuration.ConfigurationSaveMode.Modified);
                System.Configuration.ConfigurationManager.RefreshSection("appSettings");
            }
            catch (Exception ex)
            {
                MessageBox.Show("保存配置失败：" + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            MessageBox.Show("设置已保存，重启程序后生效。", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// "关闭"按钮点击事件：直接关闭窗口（不保存）
        /// </summary>
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
