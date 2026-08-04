using EasyModbus;
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Text;
using System.Windows.Forms;

namespace ModbusTCPTest
{
    /// <summary>
    /// 载台上电（继电器）输出测试窗体。
    ///
    /// 用途：
    ///   现场调试时，把 9 行 × 8 列 = 72 路载台上电输出端子按"映射表"的位置摆成网格按钮，
    ///   操作员点击按钮即可单独上电 / 断电某一路，也可以同时让多路上电。
    ///   程序会自动把同一排里所有"按下(ON)"的按钮对应的位值按位 OR 累加，
    ///   再用 Modbus 功能码 0x06 写入对应的保持寄存器（0x2004~0x2008）。
    ///
    /// 映射关系（与“通讯接入说明.md”第 4 节一致）：
    ///   第 1 排 → 0x2004 高字节，Y110~Y117，位值 0x0100~0x8000
    ///   第 2 排 → 0x2005 低字节，Y120~Y127，位值 0x0001~0x0080
    ///   第 3 排 → 0x2005 高字节，Y130~Y137，位值 0x0100~0x8000
    ///   第 4 排 → 0x2006 低字节，Y140~Y147，位值 0x0001~0x0080
    ///   第 5 排 → 0x2006 高字节，Y150~Y157，位值 0x0100~0x8000
    ///   第 6 排 → 0x2007 低字节，Y160~Y167，位值 0x0001~0x0080
    ///   第 7 排 → 0x2007 高字节，Y170~Y177，位值 0x0100~0x8000
    ///   第 8 排 → 0x2008 低字节，Y200~Y207，位值 0x0001~0x0080
    ///   第 9 排 → 0x2008 高字节，Y210~Y217，位值 0x0100~0x8000
    ///
    /// 使用示例（与现场直觉一致）：
    ///   - 只点第 1 排第 1 个 → 写 0x2004 = 0x0100
    ///   - 第 1 排第 1、2 个 → 写 0x2004 = 0x0100 | 0x0200 = 0x0300
    ///   - 再按一下第 1 排第 1 个(取消) → 写 0x2004 = 0x0200
    /// </summary>
    public partial class PowerOnTestForm : Form
    {
        // ===================== 静态映射表 =====================

        /// <summary>
        /// 每一排对应的寄存器地址（共 9 排）。
        /// 索引 0 = 第 1 排，索引 8 = 第 9 排。
        /// 注意：相邻两排会共享同一个寄存器（一个用低字节，一个用高字节）。
        /// </summary>
        private static readonly int[] RowRegisters = new int[]
        {
            0x2004, 0x2005, 0x2005, 0x2006, 0x2006,
            0x2007, 0x2007, 0x2008, 0x2008
        };

        /// <summary>
        /// 5 个寄存器地址（0x2004~0x2008），按寄存器维度去重排列。
        /// 写入时按这个列表逐个写。
        /// </summary>
        private static readonly int[] RegAddresses = new int[]
        {
            0x2004, 0x2005, 0x2006, 0x2007, 0x2008
        };

        /// <summary>
        /// 每一排对应的寄存器索引（指向 RegAddresses 的下标 0~4）。
        ///
        /// 关键映射关系（务必与 RowRegisters / RowBitValues 一致）：
        ///   第 1 排（Y110~Y117）→ 索引 0 → 0x2004  高字节（仅此一排，低字节未使用）
        ///   第 2 排（Y120~Y127）→ 索引 1 → 0x2005  低字节
        ///   第 3 排（Y130~Y137）→ 索引 1 → 0x2005  高字节  ← 与第 2 排共享 0x2005
        ///   第 4 排（Y140~Y147）→ 索引 2 → 0x2006  低字节
        ///   第 5 排（Y150~Y157）→ 索引 2 → 0x2006  高字节  ← 与第 4 排共享 0x2006
        ///   第 6 排（Y160~Y167）→ 索引 3 → 0x2007  低字节
        ///   第 7 排（Y170~Y177）→ 索引 3 → 0x2007  高字节  ← 与第 6 排共享 0x2007
        ///   第 8 排（Y200~Y207）→ 索引 4 → 0x2008  低字节
        ///   第 9 排（Y210~Y217）→ 索引 4 → 0x2008  高字节  ← 与第 8 排共享 0x2008
        ///
        /// 这就是为什么必须“按寄存器”而不是“按排”维护值：
        ///   若按排维护，按下第 3 排 Y134 时只写 0x2005=0x1000，
        ///   会把第 2 排已经写入的低字节 0x00FF 覆盖成 0x0000，导致 Y120~Y127 全部断电。
        /// </summary>
        private static readonly int[] RowToRegIndex = new int[]
        {
            0, 1, 1, 2, 2, 3, 3, 4, 4
        };

        /// <summary>
        /// 每一排使用的字节位置说明（仅用于行标签显示）。
        /// </summary>
        private static readonly string[] RowByteDesc = new string[]
        {
            "高字节", "低字节", "高字节", "低字节", "高字节",
            "低字节", "高字节", "低字节", "高字节"
        };

        /// <summary>
        /// 每一排 8 个按钮对应的位掩码（位值）。
        /// RowBitValues[row, col] 表示第 row+1 排第 col+1 个按钮在所属寄存器中的位值。
        ///
        /// 规律：
        ///   - 偶数排（第 2/4/6/8 排）= 低字节，从 0x0001 起每次左移 1 位
        ///   - 奇数排（第 1/3/5/7/9 排）= 高字节，从 0x0100 起每次左移 1 位
        /// </summary>
        private static readonly int[,] RowBitValues = new int[,]
        {
            { 0x0100, 0x0200, 0x0400, 0x0800, 0x1000, 0x2000, 0x4000, 0x8000 }, // 第 1 排 Y110~Y117 (0x2004 高字节)
            { 0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040, 0x0080 }, // 第 2 排 Y120~Y127 (0x2005 低字节)
            { 0x0100, 0x0200, 0x0400, 0x0800, 0x1000, 0x2000, 0x4000, 0x8000 }, // 第 3 排 Y130~Y137 (0x2005 高字节)
            { 0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040, 0x0080 }, // 第 4 排 Y140~Y147 (0x2006 低字节)
            { 0x0100, 0x0200, 0x0400, 0x0800, 0x1000, 0x2000, 0x4000, 0x8000 }, // 第 5 排 Y150~Y157 (0x2006 高字节)
            { 0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040, 0x0080 }, // 第 6 排 Y160~Y167 (0x2007 低字节)
            { 0x0100, 0x0200, 0x0400, 0x0800, 0x1000, 0x2000, 0x4000, 0x8000 }, // 第 7 排 Y170~Y177 (0x2007 高字节)
            { 0x0001, 0x0002, 0x0004, 0x0008, 0x0010, 0x0020, 0x0040, 0x0080 }, // 第 8 排 Y200~Y207 (0x2008 低字节)
            { 0x0100, 0x0200, 0x0400, 0x0800, 0x1000, 0x2000, 0x4000, 0x8000 }, // 第 9 排 Y210~Y217 (0x2008 高字节)
        };

        /// <summary>
        /// 每个按钮对应的 IO 编号（用于按钮上的文字显示）。
        /// </summary>
        private static readonly string[,] RowIoNames = new string[,]
        {
            { "Y110", "Y111", "Y112", "Y113", "Y114", "Y115", "Y116", "Y117" },
            { "Y120", "Y121", "Y122", "Y123", "Y124", "Y125", "Y126", "Y127" },
            { "Y130", "Y131", "Y132", "Y133", "Y134", "Y135", "Y136", "Y137" },
            { "Y140", "Y141", "Y142", "Y143", "Y144", "Y145", "Y146", "Y147" },
            { "Y150", "Y151", "Y152", "Y153", "Y154", "Y155", "Y156", "Y157" },
            { "Y160", "Y161", "Y162", "Y163", "Y164", "Y165", "Y166", "Y167" },
            { "Y170", "Y171", "Y172", "Y173", "Y174", "Y175", "Y176", "Y177" },
            { "Y200", "Y201", "Y202", "Y203", "Y204", "Y205", "Y206", "Y207" },
            { "Y210", "Y211", "Y212", "Y213", "Y214", "Y215", "Y216", "Y217" },
        };

        // ===================== 运行时状态 =====================

        /// <summary>
        /// Modbus TCP 客户端（本窗体自己创建并管理，与 MainForm 解耦）。
        /// </summary>
        private ModbusClient modbusClient;

        /// <summary>
        /// 目标设备的 IP 与端口（由 MainForm 跳转时传入，默认与 MainForm 一致）。
        /// </summary>
        private string host;
        private int port;

        /// <summary>
        /// 9×8 圆形灯按钮数组（运行时动态生成）。
        /// buttons[row, col] 对应第 row+1 排第 col+1 个端子。
        /// </summary>
        private CircleButton[,] buttons = new CircleButton[9, 8];

        /// <summary>
        /// 5 个寄存器当前的值（按位 OR 累积得到）。
        ///
        /// 重要：必须“按寄存器”维护，而不是“按排”维护。
        /// 因为 0x2005/0x2006/0x2007/0x2008 各自被相邻两排共享（低字节+高字节），
        /// 任意一排按钮变化时，都要把该寄存器所有相关排的 ON 按钮位值合并后整体写入，
        /// 否则会发生“后写覆盖前写”的 bug。
        ///
        /// 0x2004 是特例：只有第 1 排（高字节 Y110~Y117），低字节未使用，
        /// 其 currentRegValues[0] 的低 8 位永远是 0（因为第 1 排位值最小也是 0x0100）。
        /// </summary>
        private int[] currentRegValues = new int[5];

        /// <summary>
        /// 行标签数组（显示“第 N 排 / 0x200X / 低|高字节 / 当前值”）。
        /// </summary>
        private Label[] rowLabels = new Label[9];

        // ===================== 构造与初始化 =====================

        /// <summary>
        /// 构造函数：传入 IP 与端口，方便与 MainForm 共用同一台耦合器。
        /// </summary>
        /// <param name="host">耦合器 IP（默认 192.168.1.20）</param>
        /// <param name="port">Modbus TCP 端口（默认 502）</param>
        public PowerOnTestForm(string host = "192.168.1.20", int port = 502)
        {
            this.host = host;
            this.port = port;
            InitializeComponent();
            InitModbusClient();
            BuildButtonGrid();
            RefreshRowLabels();
        }

        /// <summary>
        /// 创建 ModbusClient 实例并设置超时（参考 MainForm.InitData）。
        /// </summary>
        private void InitModbusClient()
        {
            modbusClient = new ModbusClient(host, port);
            modbusClient.ConnectionTimeout = 5000;
        }

        /// <summary>
        /// 动态生成 9 行 × 8 列的圆形灯按钮，并加上行标签。
        /// 用代码生成是为了避免 Designer 里手写 72 个按钮太啰嗦。
        /// </summary>
        private void BuildButtonGrid()
        {
            // 网格布局参数
            const int buttonSize = 56;        // 每个圆形按钮的边长（像素）
            const int gapX = 8;               // 同一排两个按钮之间的水平间距
            const int gapY = 14;              // 两排之间的垂直间距
            const int rowLabelWidth = 130;    // 左侧行标签宽度
            const int gridLeft = 12;          // 网格在 panel 内的左边距
            const int gridTop = 6;            // 网格在 panel 内的顶边距

            // 顶部还要画一个列号行（1~8），所以先放列号
            for (int c = 0; c < 8; c++)
            {
                Label colHeader = new Label();
                colHeader.AutoSize = false;
                colHeader.Size = new Size(buttonSize, 22);
                colHeader.Location = new Point(
                    gridLeft + rowLabelWidth + c * (buttonSize + gapX),
                    gridTop);
                colHeader.Text = (c + 1).ToString();
                colHeader.TextAlign = ContentAlignment.MiddleCenter;
                colHeader.Font = new Font("宋体", 10F, FontStyle.Bold);
                colHeader.ForeColor = Color.DarkSlateGray;
                this.panelGrid.Controls.Add(colHeader);
            }

            // 9 排按钮
            for (int r = 0; r < 9; r++)
            {
                int y = gridTop + 28 + r * (buttonSize + gapY);

                // ---- 左侧行标签 ----
                Label rowLabel = new Label();
                rowLabel.AutoSize = false;
                rowLabel.Size = new Size(rowLabelWidth, buttonSize);
                rowLabel.Location = new Point(gridLeft, y);
                rowLabel.TextAlign = ContentAlignment.MiddleLeft;
                rowLabel.Font = new Font("宋体", 9.5F, FontStyle.Regular);
                rowLabel.ForeColor = Color.Black;
                rowLabel.BackColor = Color.FromArgb(245, 245, 245);
                rowLabel.BorderStyle = BorderStyle.FixedSingle;
                rowLabels[r] = rowLabel;
                this.panelGrid.Controls.Add(rowLabel);

                // ---- 8 个圆形按钮 ----
                for (int c = 0; c < 8; c++)
                {
                    CircleButton btn = new CircleButton();
                    btn.Size = new Size(buttonSize, buttonSize);
                    btn.Location = new Point(
                        gridLeft + rowLabelWidth + c * (buttonSize + gapX),
                        y);
                    btn.Text = RowIoNames[r, c];      // 按钮上显示 Y110 等
                    btn.Row = r;                       // 记录所在排（0~8）
                    btn.Col = c;                       // 记录所在列（0~7）
                    btn.BitValue = RowBitValues[r, c]; // 该按钮对应的位值
                    // 统一订阅 Click 事件，统一处理
                    btn.Click += CircleButton_Click;
                    buttons[r, c] = btn;
                    this.panelGrid.Controls.Add(btn);
                }
            }

            // 调整 panel 大小，刚好包住所有控件
            int totalWidth = gridLeft * 2 + rowLabelWidth + 8 * buttonSize + 7 * gapX;
            int totalHeight = gridTop * 2 + 28 + 9 * buttonSize + 8 * gapY;
            this.panelGrid.Size = new Size(totalWidth, totalHeight);
        }

        /// <summary>
        /// 刷新所有行标签上的文字（显示排号、寄存器、字节、当前寄存器值）。
        ///
        /// 注意：共享同一个寄存器的两排（如第 2 排和第 3 排共享 0x2005）
        /// 会显示同一个寄存器值——这是正确的，因为它们本就对应同一个 16 位寄存器。
        /// </summary>
        private void RefreshRowLabels()
        {
            for (int r = 0; r < 9; r++)
            {
                int regIdx = RowToRegIndex[r];
                rowLabels[r].Text = string.Format(
                    "第 {0} 排  0x{1:X4}  {2}\n当前值: 0x{3:X4}",
                    r + 1,
                    RegAddresses[regIdx],
                    RowByteDesc[r],
                    currentRegValues[regIdx]);
            }
        }

        // ===================== 按钮点击（核心 toggle 逻辑） =====================

        /// <summary>
        /// 圆形按钮点击事件处理：toggle 该按钮的 ON/OFF 状态，
        /// 然后重新计算所属寄存器的合并值并整体写入。
        ///
        /// 举例（第 1 排，0x2004，只有高字节）：
        ///   1) 第一次点 Y110 → Y110.IsOn=true  → 重算 0x2004=0x0100 → 写 0x2004=0x0100
        ///   2) 再点 Y111     → Y111.IsOn=true  → 重算 0x2004=0x0100|0x0200=0x0300 → 写 0x2004=0x0300
        ///   3) 再点 Y112     → Y112.IsOn=true  → 重算 0x2004=0x0300|0x0400=0x0700 → 写 0x2004=0x0700
        ///   4) 再点 Y112     → Y112.IsOn=false → 重算 0x2004=0x0700&~0x0400=0x0300 → 写 0x2004=0x0300
        ///
        /// 共享寄存器场景（第 2 排低字节 + 第 3 排高字节 共享 0x2005）：
        ///   1) 第 2 排 Y120~Y127 全部 ON → 重算 0x2005=0x00FF → 写 0x2005=0x00FF
        ///   2) 再点第 3 排 Y134         → 重算 0x2005=0x00FF | 0x1000 = 0x10FF → 写 0x2005=0x10FF
        ///      （低字节 0x00FF 保留，高字节叠加 0x1000，两排互不影响）
        /// </summary>
        private void CircleButton_Click(object sender, EventArgs e)
        {
            CircleButton btn = sender as CircleButton;
            if (btn == null) return;

            int row = btn.Row;

            // 1) toggle 该按钮的 ON/OFF 状态
            btn.IsOn = !btn.IsOn;

            // 2) 找到该按钮所属的寄存器索引
            int regIndex = RowToRegIndex[row];

            // 3) 重新计算该寄存器的合并值
            //    关键：要把共享该寄存器的所有排的 ON 按钮位值都 OR 起来，
            //    不能只算当前这一排，否则会把另一排已写入的字节覆盖掉。
            currentRegValues[regIndex] = RecomputeRegValue(regIndex);

            // 4) 把合并后的值整体写入对应的寄存器
            WriteRegister(regIndex, row);

            // 5) 刷新显示
            RefreshRowLabels();
        }

        /// <summary>
        /// 重新计算指定寄存器的合并值。
        /// 遍历所有共享该寄存器的排，把每个 ON 按钮的位值按位 OR 起来。
        ///
        /// 例如 regIndex=1（0x2005）：
        ///   - 遍历第 2 排（Y120~Y127，低字节位值 0x0001~0x0080）
        ///   - 遍历第 3 排（Y130~Y137，高字节位值 0x0100~0x8000）
        ///   - 把两排里所有 IsOn=true 的按钮位值 OR 起来，得到 16 位完整寄存器值
        ///
        /// 对于 regIndex=0（0x2004）：
        ///   - 只有第 1 排，低字节天然为 0（第 1 排位值最小为 0x0100），符合“0x2004 仅高字节”的约定。
        /// </summary>
        /// <param name="regIndex">寄存器索引（0~4，对应 0x2004~0x2008）</param>
        /// <returns>该寄存器当前的 16 位合并值</returns>
        private int RecomputeRegValue(int regIndex)
        {
            int value = 0;
            for (int r = 0; r < 9; r++)
            {
                // 只处理共享该寄存器的排
                if (RowToRegIndex[r] != regIndex) continue;

                for (int c = 0; c < 8; c++)
                {
                    if (buttons[r, c].IsOn)
                    {
                        value |= RowBitValues[r, c];
                    }
                }
            }
            return value;
        }

        /// <summary>
        /// 把指定寄存器的当前合并值写入耦合器（功能码 0x06）。
        /// 写入逻辑参考 MainForm.btnWriteData_Click。
        ///
        /// 注意：写入的是整个 16 位寄存器值（已合并低字节+高字节），
        /// 这样就不会出现“后写覆盖前写”的问题。
        /// </summary>
        /// <param name="regIndex">寄存器索引（0~4）</param>
        /// <param name="triggerRow">触发本次写入的排索引（0~8），仅用于日志显示</param>
        private void WriteRegister(int regIndex, int triggerRow)
        {
            int addr = RegAddresses[regIndex];
            int val = currentRegValues[regIndex];

            // 先检查连接状态
            if (!modbusClient.Connected)
            {
                AppendLog(string.Format(
                    "[警告] 未连接，无法写入。请先点击“连接测试”。(由第 {0} 排触发，0x{1:X4} = 0x{2:X4})",
                    triggerRow + 1, addr, val));
                return;
            }

            // 设置从站地址（耦合器 Modbus 从站地址 = 0x01，与 MainForm 一致）
            modbusClient.UnitIdentifier = 0x01;

            try
            {
                // 使用 WriteSingleRegister 写单个寄存器（功能码 0x06）
                modbusClient.WriteSingleRegister(addr, val);

                // 拼一个简短的日志：列出该寄存器共享的所有排里 ON 的端子，便于现场核对
                StringBuilder onList = new StringBuilder();
                for (int r = 0; r < 9; r++)
                {
                    if (RowToRegIndex[r] != regIndex) continue;
                    for (int c = 0; c < 8; c++)
                    {
                        if (buttons[r, c].IsOn)
                        {
                            if (onList.Length > 0) onList.Append(", ");
                            onList.Append(RowIoNames[r, c]);
                        }
                    }
                }
                if (onList.Length == 0) onList.Append("(无)");

                AppendLog(string.Format(
                    "[写入] 由第 {0} 排触发  0x{1:X4} = 0x{2:X4}   ON: {3}",
                    triggerRow + 1, addr, val, onList));
            }
            catch (Exception ex)
            {
                AppendLog(string.Format(
                    "[错误] 写入 0x{0:X4} 失败: {1}", addr, ex.Message));
                MessageBox.Show(
                    string.Format("写入 0x{0:X4} 失败:\n{1}", addr, ex.Message),
                    "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ===================== 底部控制按钮 =====================

        /// <summary>
        /// “连接测试”按钮：与耦合器建立 Modbus TCP 连接。
        /// </summary>
        private void btnConnect_Click(object sender, EventArgs e)
        {
            try
            {
                // 若已经连接，先断开再重连（避免重复连接报错）
                if (modbusClient.Connected)
                {
                    modbusClient.Disconnect();
                }
                modbusClient.Connect();
                if (modbusClient.Connected)
                {
                    AppendLog(string.Format(
                        "[连接] 已连接到 {0}:{1}", host, port));
                    MessageBox.Show("连接成功！", "提示",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                string msg = "连接失败: " + ex.Message;
                if (ex.InnerException != null)
                    msg += "\n内部异常: " + ex.InnerException.Message;
                AppendLog("[错误] " + msg);
                MessageBox.Show(msg, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// “全部关闭”按钮：把所有按钮置为 OFF，并把 5 个寄存器（0x2004~0x2008）全部写 0。
        /// 一键关断，现场应急用。
        /// </summary>
        private void btnAllOff_Click(object sender, EventArgs e)
        {
            // 1) UI 全部清零（所有 72 个按钮置为 OFF）
            for (int r = 0; r < 9; r++)
            {
                for (int c = 0; c < 8; c++)
                {
                    buttons[r, c].IsOn = false;
                }
            }

            // 2) 5 个寄存器的本地状态全部清零
            //    （按寄存器维度，不是按排维度）
            for (int i = 0; i < 5; i++)
            {
                currentRegValues[i] = 0;
            }
            RefreshRowLabels();

            // 3) 把 5 个寄存器全部写 0
            //    注意：0x2004 只用高字节，整字写 0 没问题（低字节本来就是 0）
            if (!modbusClient.Connected)
            {
                AppendLog("[警告] 未连接，仅清除本地状态。请先连接再写入。");
                return;
            }

            modbusClient.UnitIdentifier = 0x01;
            foreach (int addr in RegAddresses)
            {
                try
                {
                    modbusClient.WriteSingleRegister(addr, 0x0000);
                    AppendLog(string.Format("[关闭] 0x{0:X4} = 0x0000", addr));
                }
                catch (Exception ex)
                {
                    AppendLog(string.Format(
                        "[错误] 写 0x{0:X4} 失败: {1}", addr, ex.Message));
                }
            }
        }

        /// <summary>
        /// “读取状态”按钮：读回 0x2004~0x2008 这 5 个寄存器，
        /// 并根据读到的位反推每个按钮的 ON/OFF，刷新界面。
        /// 现场用：打开窗体后想看看耦合器当前实际输出状态时使用。
        /// </summary>
        private void btnReadStatus_Click(object sender, EventArgs e)
        {
            if (!modbusClient.Connected)
            {
                MessageBox.Show("请先点击“连接测试”建立通讯！", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            modbusClient.UnitIdentifier = 0x01;
            try
            {
                // 用 ReadHoldingRegisters 一次读 5 个保持寄存器（功能码 0x03）
                // 起始地址 0x2004，数量 5
                int[] result = modbusClient.ReadHoldingRegisters(0x2004, 5);

                // result[0] = 0x2004, result[1] = 0x2005, ... result[4] = 0x2008
                //
                // 【重要坑点】EasyModbus 的 ReadHoldingRegisters 返回 int[]，
                //   当寄存器的 bit15=1（即值 ≥ 0x8000）时，会被符号扩展成 32 位负数。
                //   例如 0xFFFF 会被读成 0xFFFFFFFF（int 值 -1）。
                //   所以必须用 & 0xFFFF 屏蔽高 16 位，还原成 16 位无符号值。
                for (int i = 0; i < 5; i++)
                {
                    result[i] = result[i] & 0xFFFF;
                }

                // 1) 把 5 个寄存器值存入 currentRegValues
                for (int i = 0; i < 5; i++)
                {
                    currentRegValues[i] = result[i];
                }

                // 2) 用位掩码反推每个按钮的 ON/OFF
                //    同一个寄存器的两排（如第 2 排和第 3 排共享 0x2005），
                //    各自用自己排的位掩码（低字节 0x0001~0x0080 或 高字节 0x0100~0x8000）
                //    去判断，互不干扰。
                for (int r = 0; r < 9; r++)
                {
                    int regIndex = RowToRegIndex[r];
                    int regValue = currentRegValues[regIndex];
                    for (int c = 0; c < 8; c++)
                    {
                        int bit = RowBitValues[r, c];
                        buttons[r, c].IsOn = (regValue & bit) != 0;
                    }
                }

                RefreshRowLabels();
                AppendLog(string.Format(
                    "[读取] 0x2004=0x{0:X4} 0x2005=0x{1:X4} 0x2006=0x{2:X4} 0x2007=0x{3:X4} 0x2008=0x{4:X4}",
                    result[0], result[1], result[2], result[3], result[4]));
            }
            catch (Exception ex)
            {
                AppendLog("[错误] 读取失败: " + ex.Message);
                MessageBox.Show("读取失败: " + ex.Message, "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// “关闭”按钮：关闭本窗体。
        /// </summary>
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        // ===================== 窗体关闭时清理 =====================

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            // 关窗时断开 Modbus 连接，避免占用 socket
            try
            {
                if (modbusClient != null && modbusClient.Connected)
                {
                    modbusClient.Disconnect();
                }
            }
            catch
            {
                // 关闭时忽略清理异常
            }
            base.OnFormClosed(e);
        }

        // ===================== 日志输出 =====================

        /// <summary>
        /// 把一条日志追加到底部 txtLog，并在前面加上时间戳。
        /// 最多保留最后 200 行，避免无限制增长。
        /// </summary>
        private void AppendLog(string message)
        {
            string line = string.Format("[{0}] {1}",
                DateTime.Now.ToString("HH:mm:ss"), message);

            if (this.txtLog.InvokeRequired)
            {
                // 跨线程调用时安全追加
                this.txtLog.Invoke(new Action<string>(AppendLog), line);
                return;
            }

            this.txtLog.AppendText(line + Environment.NewLine);

            // 限制最大行数
            const int maxLines = 200;
            var lines = this.txtLog.Lines;
            if (lines.Length > maxLines)
            {
                int skip = lines.Length - maxLines;
                var kept = new string[maxLines];
                Array.Copy(lines, skip, kept, 0, maxLines);
                this.txtLog.Lines = kept;
            }
        }

        // ========================================================================
        //  内部类：CircleButton —— 圆形灯按钮
        //  说明：
        //    - 继承自 Button，重写 OnPaint 自绘圆形
        //    - IsOn = false 时画成深灰色（暗）
        //    - IsOn = true  时画成亮绿色（亮）+ 金色边框，视觉上"明亮"
        //    - 双缓冲，避免点击时闪烁
        // ========================================================================
        public class CircleButton : Button
        {
            // ----- 颜色定义 -----
            private static readonly Color OffFill = Color.FromArgb(70, 70, 76);     // OFF：深灰，暗
            private static readonly Color OnFill = Color.FromArgb(40, 220, 70);     // ON：亮绿，明亮
            private static readonly Color OffBorder = Color.FromArgb(40, 40, 40);   // OFF 边框
            private static readonly Color OnBorder = Color.Gold;                    // ON 边框：金色，更显眼
            private static readonly Color OffText = Color.FromArgb(180, 180, 180);  // OFF 文字
            private static readonly Color OnText = Color.Black;                     // ON 文字

            // IsOn 的私有后备字段
            private bool isOn = false;

            /// <summary>
            /// 当前是否处于按下(ON)状态。
            /// setter 在状态变化时自动触发重绘，让颜色立刻刷新。
            /// </summary>
            public bool IsOn
            {
                get { return isOn; }
                set
                {
                    if (isOn != value)
                    {
                        isOn = value;
                        this.Invalidate(); // 触发 OnPaint 重绘
                    }
                }
            }

            /// <summary>所在排索引（0~8）</summary>
            public int Row { get; set; }

            /// <summary>所在列索引（0~7）</summary>
            public int Col { get; set; }

            /// <summary>该按钮在所属寄存器中的位值（0x0001/0x0100 等）</summary>
            public int BitValue { get; set; }

            public CircleButton()
            {
                // 双缓冲 + 自绘，避免闪烁
                this.SetStyle(
                    ControlStyles.AllPaintingInWmPaint |
                    ControlStyles.UserPaint |
                    ControlStyles.OptimizedDoubleBuffer |
                    ControlStyles.ResizeRedraw,
                    true);
                this.FlatStyle = FlatStyle.Flat;
                this.FlatAppearance.BorderSize = 0;
                this.Cursor = Cursors.Hand;
                this.Font = new Font("宋体", 9F, FontStyle.Bold);
                this.TextAlign = ContentAlignment.MiddleCenter;
            }

            protected override void OnPaint(PaintEventArgs e)
            {
                Graphics g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // 1) 先把背景刷成父控件的背景色，避免方形边角残留
                Color back = (this.Parent != null) ? this.Parent.BackColor : Color.White;
                using (SolidBrush bgBrush = new SolidBrush(back))
                {
                    g.FillRectangle(bgBrush, this.ClientRectangle);
                }

                // 2) 计算圆形区域（四周留 4px 内边距）
                Rectangle circleRect = this.ClientRectangle;
                circleRect.Inflate(-4, -4);

                // 3) 填充圆形
                Color fill = IsOn ? OnFill : OffFill;
                using (SolidBrush fillBrush = new SolidBrush(fill))
                {
                    g.FillEllipse(fillBrush, circleRect);
                }

                // 4) 画边框（ON 时金色加粗，OFF 时深灰细线）
                using (Pen borderPen = new Pen(IsOn ? OnBorder : OffBorder, IsOn ? 2.5f : 1.5f))
                {
                    g.DrawEllipse(borderPen, circleRect);
                }

                // 5) 画文字（Y110 等）
                Color textColor = IsOn ? OnText : OffText;
                using (SolidBrush textBrush = new SolidBrush(textColor))
                {
                    StringFormat sf = new StringFormat();
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(this.Text, this.Font, textBrush, this.ClientRectangle, sf);
                }
            }
        }
    }
}
