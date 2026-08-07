using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using BarometerWinform.Models;

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
    ///   动态创建、在 LayoutSections 里按 Y 坐标依次排布，表格高度按行数自动计算。
    ///   新增配置项只需在 _descriptions 和 _categories 里各加一行，无需改界面布局
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
            { "AlarmPressureThresholdPa", "报警压力阈值（Pa，如 -95000）" },
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
                "AlarmPressureThresholdPa", "AlarmWhenPressureHigherThanThreshold"
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
                ScrollBars = ScrollBars.Vertical
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
            grid.Columns["colDesc"].DefaultCellStyle.WrapMode = DataGridViewTriState.True;
            grid.Columns["colValue"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            return grid;
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
                    grid.Rows[rowIdx].Cells["colValue"].Value = GetEffectiveValue(key);
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
            int y = margin;
            int width = pnlScroll.ClientSize.Width - margin * 2;
            if (width < 100) width = 100;

            foreach (var section in _sections)
            {
                Sunny.UI.UILine line = section.Key;
                Sunny.UI.UIDataGridView grid = section.Value;

                // 标题条：占满可用宽度
                line.Location = new Point(margin, y);
                line.Width = width;
                line.Height = 28;
                y += line.Height + 4;

                // 表格：占满可用宽度，高度 = 表头 + 行数 × 行高 + 边距
                int gridHeight = grid.ColumnHeadersHeight + grid.Rows.Count * grid.RowTemplate.Height + 2;
                grid.Location = new Point(margin, y);
                grid.Width = width;
                grid.Height = gridHeight;
                y += gridHeight + gap;
            }

            // 内容总高（超出可视区时 pnlScroll 自动出现滚动条）
            pnlScroll.AutoScrollMinSize = new Size(0, y + margin);
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
                case "AlarmPressureThresholdPa":
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
