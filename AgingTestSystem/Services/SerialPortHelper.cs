using System.Collections.Generic;
using System.IO.Ports;
using System.Management;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 串口识别辅助类（CH340 自动识别）
    ///
    /// 【为什么需要它】
    /// 气压表通过 RS485 转 USB（CH340 芯片）接入工控机，Windows 会把它识别成一个
    /// "COM 口"，但具体是 COM 几不确定（COM3 / COM5 / COM10 ... 取决于 USB 插口和
    /// 历史驱动分配）。如果程序里写死 COM1，现场换一台电脑/换一个 USB 口就连不上。
    ///
    /// 参考 ModbusRtuBarometerTest Demo 的 SerialPortHelper + 本项目 ScannerService
    /// 的 WMI 写法，用系统 WMI 查询出"名字里带 CH340"的串口，从而自动找到气压表
    /// 实际插在哪个 COM 口，现场不用改配置。
    ///
    /// 【双重校验】
    /// CH340 芯片的 USB VID/PID 是固定的：VID_1A86（WCH/沁恒）+ PID_7523（CH340）。
    /// 同时校验"设备描述含 CH340" + "硬件 ID 含 VID_1A86/PID_7523"，避免误认别的串口。
    /// </summary>
    public static class SerialPortHelper
    {
        /// <summary>
        /// 判断一对（设备描述，硬件ID）是否为 CH340 串口（纯函数，回归可直接断言）。
        ///
        /// 【匹配规则】描述含 CH340（大小写不敏感）且硬件 ID 同时含 VID_1A86/PID_7523。
        /// 【V1.62】描述匹配由大小写敏感的 Contains 改为忽略大小写：
        /// 个别机器的设备描述是小写 ch340，旧写法会识别不到串口。
        /// </summary>
        /// <param name="caption">WMI Win32_PnPEntity.Caption（如 "USB-SERIAL CH340 (COM3)"）</param>
        /// <param name="pnpId">WMI PNPDeviceID（含 VID/PID 的硬件 ID）</param>
        /// <returns>true=判定为 CH340 串口</returns>
        public static bool IsCh340Device(string caption, string pnpId)
        {
            if (string.IsNullOrEmpty(caption) || string.IsNullOrEmpty(pnpId)) return false;
            return caption.IndexOf("CH340", System.StringComparison.OrdinalIgnoreCase) >= 0
                && pnpId.IndexOf("VID_1A86", System.StringComparison.OrdinalIgnoreCase) >= 0
                && pnpId.IndexOf("PID_7523", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        /// <summary>
        /// 获取第一个匹配的 CH340 串口名称（例如 "COM3"）
        /// 找不到返回 null（表示气压表适配器没插 / 驱动没装）
        /// </summary>
        /// <returns>端口名称，未找到返回 null</returns>
        public static string GetCh340PortName()
        {
            List<string> ports = GetCh340Ports();
            return ports.Count > 0 ? ports[0] : null;
        }

        /// <summary>
        /// 获取所有匹配的 CH340 串口名称列表（按系统枚举顺序）
        /// </summary>
        /// <returns>CH340 端口列表（可能为空）</returns>
        public static List<string> GetCh340Ports()
        {
            var ch340Ports = new List<string>();

            try
            {
                // 用 WMI 查询系统 PnP 设备：名字里包含 "COM" 的都是串口类设备
                //（如 "USB-SERIAL CH340 (COM3)"）
                string query = "SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'";

                using (var searcher = new ManagementObjectSearcher(query))
                {
                    foreach (ManagementObject device in searcher.Get())
                    {
                        string caption = device["Caption"]?.ToString();
                        string pnpId = device["PNPDeviceID"]?.ToString();

                        // 双重校验：设备描述含 CH340 + 硬件 ID 是 CH340 的 VID/PID
                        //（防止其它 USB 转串口芯片如 FTDI/CP2102 被误认）
                        // 判定逻辑收拢进 IsCh340Device（纯函数可单测），此处只调用。
                        if (!IsCh340Device(caption, pnpId))
                        {
                            continue;
                        }

                        // 从 Caption 中提取出 "COMx" 端口号：
                        // 格式一般是 "USB-SERIAL CH340 (COM3)"，取最后一对括号里的内容
                        int startIndex = caption.LastIndexOf('(') + 1;
                        int endIndex = caption.LastIndexOf(')');
                        if (startIndex > 0 && endIndex > startIndex)
                        {
                            string portName = caption.Substring(startIndex, endIndex - startIndex);
                            ch340Ports.Add(portName);
                        }
                    }
                }
            }
            catch
            {
                // WMI 查询失败（权限不足等）：返回空列表，上层回落配置的固定端口
            }

            return ch340Ports;
        }

        /// <summary>
        /// 钳制数据位到串口合法范围（纯函数，回归可直接断言）。
        ///
        /// 【为什么需要】System.IO.Ports.SerialPort 的 DataBits 只认 5~8，
        /// 配成 9/4/0 会在 Connect 打开串口时抛异常，被上层的 try/catch 吃掉后
        /// 表现成"连不上"，操作员会按串口故障去查线，排查方向全错。
        /// 设置窗下拉造不出非法值，但管理员手改 exe.config 能绕过一切校验，
        /// 所以主窗体加载配置时调本函数钳制 + 记警告日志（见 MainForm.LoadConfig）。
        /// </summary>
        /// <param name="value">配置里的原始值</param>
        /// <returns>5~8 原样返回，否则回退 8</returns>
        public static int ClampDataBits(int value)
        {
            if (value < 5 || value > 8) return 8;
            return value;
        }

        /// <summary>
        /// 钳制波特率为正数（纯函数，回归可直接断言）。
        /// 0/负数会让 SerialPort 赋值时直接抛异常（同样被吃成"连不上"），回退调用方给的默认值。
        /// 上限不卡：非常见档位也可能是真设备，留给驱动在 Open 时报错（错误信息明确）。
        /// </summary>
        /// <param name="value">配置里的原始值</param>
        /// <param name="fallback">非法时的回退值（调用方传对应配置项的默认值）</param>
        /// <returns>大于 0 原样返回，否则回退</returns>
        public static int ClampBaudRate(int value, int fallback)
        {
            if (value <= 0) return fallback;
            return value;
        }

        /// <summary>
        /// 钳制串口超时为正数（纯函数，回归可直接断言）。
        /// 0/负数超时语义不明（各驱动行为不一），统一回退默认值并记警告。
        /// </summary>
        /// <param name="value">配置里的原始值（毫秒）</param>
        /// <param name="fallback">非法时的回退值（调用方传对应配置项的默认值）</param>
        /// <returns>大于 0 原样返回，否则回退</returns>
        public static int ClampTimeoutMs(int value, int fallback)
        {
            if (value <= 0) return fallback;
            return value;
        }

        /// <summary>
        /// 获取当前系统所有已存在的串口名称（如 COM1、COM3 ...）
        /// 用于"判断配置里的固定端口是否存在"和"CH340 识别失败时的兜底"。
        /// </summary>
        /// <returns>系统串口名称数组</returns>
        public static string[] GetAllPortNames()
        {
            try
            {
                return SerialPort.GetPortNames();
            }
            catch
            {
                return new string[0];
            }
        }
    }
}
