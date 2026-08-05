using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ModbusTCPFanControllerTest
{

    /// <summary>
    /// 冷却送风机控制命令（写入寄存器 0x0001 的值）
    /// 根据厂家实际指令定义：
    ///   定值启动 -> 0x0003
    ///   定值停止 -> 0x0002
    /// </summary>
    public enum FanCommand : ushort
    {
        /// <summary>定值停止</summary>
        Stop = 0x0002,
        /// <summary>定值启动</summary>
        FixedValueStart = 0x0003
        // 若有其他模式可继续添加
    }
}
