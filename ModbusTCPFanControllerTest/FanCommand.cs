using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusTCPFanControllerTest
{

    /// <summary>
    /// 冷却送风机控制命令（寄存器 0x0001 的取值）
    ///
    /// 【重要】寄存器 0x0001 的取值与写入命令共用同一套码值（实测确认），
    /// 读取时设备会回这 4 个值之一，写入时只使用 定值启动/定值停止：
    ///   - 0x0000 程式停止
    ///   - 0x0001 程式启动
    ///   - 0x0002 定值停止
    ///   - 0x0003 定值启动
    ///
    /// 为什么枚举要补全 4 个值：如果只定义 0x0002/0x0003，设备回 0x0000/0x0001 时，
    /// 强转出来的枚举"没有名字"，ToString() 会打印裸数字（如 "1"），UI 显示就错了。
    /// 见《冷却送风机 Modbus TCP 通信接口说明文档》第 3 节寄存器映射表。
    /// </summary>
    public enum FanCommand : ushort
    {
        /// <summary>程式停止</summary>
        ProgramStopped = 0x0000,

        /// <summary>程式启动</summary>
        ProgramRunning = 0x0001,

        /// <summary>定值停止（上位机"定值停止"按钮写入此值）</summary>
        Stop = 0x0002,

        /// <summary>定值启动（上位机"定值启动"按钮写入此值）</summary>
        FixedValueStart = 0x0003
    }
}
