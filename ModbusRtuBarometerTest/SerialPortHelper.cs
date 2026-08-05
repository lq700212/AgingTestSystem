using System.Collections.Generic;
using System.Linq;
using System.Management;

namespace ModbusRtuBarometerTest
{
    public class SerialPortHelper
    {
        /// <summary>
        /// 获取第一个匹配的CH340串口名称 (例如: "COM3")
        /// </summary>
        public static string GetCh340PortName()
        {
            var ports = GetCh340Ports();
            return ports.FirstOrDefault();
        }

        /// <summary>
        /// 获取所有匹配的CH340串口名称列表
        /// </summary>
        public static List<string> GetCh340Ports()
        {
            var ch340Ports = new List<string>();
            // 查询所有名称中包含 "(COM" 的设备，这能有效筛选出串口设备[reference:3]
            string query = "SELECT * FROM Win32_PnPEntity WHERE Caption LIKE '%(COM%'";

            using (var searcher = new ManagementObjectSearcher(query))
            {
                foreach (ManagementObject device in searcher.Get())
                {
                    string caption = device["Caption"]?.ToString();
                    string pnpId = device["PNPDeviceID"]?.ToString();

                    // 检查设备描述中是否包含 "CH340"，并且硬件ID也匹配，双重保险[reference:4]
                    if (!string.IsNullOrEmpty(caption) &&
                        caption.Contains("CH340") &&
                        !string.IsNullOrEmpty(pnpId) &&
                        pnpId.Contains("VID_1A86") && // CH340的厂商ID[reference:5]
                        pnpId.Contains("PID_7523"))   // CH340的产品ID[reference:6]
                    {
                        // 从 Caption 中提取出 "COMx" 端口号
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

            return ch340Ports;
        }
    }
}
