using System;
using System.Collections.Generic;
using BarometerWinform.Models;

namespace BarometerWinform.Services
{
    /// <summary>
    /// IO映射表构建器
    /// 【V1.09 新增】依据现场"显耀IO表"建立内部连续编号与三菱PLC物理地址之间的映射关系。
    ///
    /// 【显耀IO表 实际配置】
    /// - 输入(NPN, 72个): 真空负压表-1~72, 地址 X000~X107 (八进制)
    /// - 输出(PNP, 144个):
    ///   * 真空电磁阀-1~72, 地址 Y000~Y107 (八进制)
    ///   * 载台上电-1~72,  地址 Y110~Y217 (八进制)
    /// - 每个气压表对应: 1输入 + 2输出
    ///
    /// 【内部编号 vs 物理地址】
    /// 程序内部使用十进制连续编号(IoId)便于数组索引:
    ///   输入: 1 ~ TotalInputs
    ///   输出: TotalInputs+1 ~ TotalInputs+TotalOutputs
    /// 与硬件通信时需通过物理地址(PhysicalAddress)寻址。
    ///
    /// 【八进制编址说明】
    /// 三菱PLC的 X/Y 点采用八进制编号(每位数字仅 0~7)。
    /// 例如 X007 之后是 X010(非 X008), X077 之后是 X100。
    /// 本构建器使用 Convert.ToString(value, 8) 将十进制转为八进制字符串。
    /// </summary>
    public static class IoMapBuilder
    {
        /// <summary>
        /// 构建完整的IO映射表
        /// 按"输入→真空电磁阀输出→载台上电输出→预留输入/输出"顺序生成所有IO点定义
        ///
        /// 【设计说明】
        /// 业务角度：每个气压表固定对应 1输入 + 2输出，因此“业务必需”总数由 totalBarometers 决定。
        ///
        /// 现场角度：GX-CL140 后面模块数量可能多于业务使用量（例如现场是 80DI/160DO，但业务用 72DI/144DO）。
        /// 因此本构建器支持把多出来的通道作为“预留点”也生成出来，便于后续扩展或现场排查。
        /// </summary>
        /// <param name="config">设备配置（含 TotalBarometers/TotalInputs/TotalOutputs）</param>
        /// <returns>所有 IO 点定义列表</returns>
        public static List<IoPointDefinition> Build(DeviceConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (config.TotalBarometers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(config.TotalBarometers),
                    "气压表总数不能小于1");
            }

            if (config.TotalInputs < config.TotalBarometers)
            {
                throw new ArgumentOutOfRangeException(nameof(config.TotalInputs),
                    "TotalInputs 不能小于 TotalBarometers（每个气压表至少需要 1 个输入点）");
            }

            if (config.TotalOutputs < config.TotalBarometers * 2)
            {
                throw new ArgumentOutOfRangeException(nameof(config.TotalOutputs),
                    "TotalOutputs 不能小于 TotalBarometers×2（每个气压表至少需要 2 个输出点）");
            }

            var map = new List<IoPointDefinition>(config.TotalInputs + config.TotalOutputs);
            int ioId = 1;

            // ===== 1. 输入点: 真空负压表-N → X 地址(八进制) =====
            // 地址规律: X + octal(n-1), 即 n=1→X000, n=8→X007, n=9→X010, n=72→X107
            for (int n = 1; n <= config.TotalBarometers; n++)
            {
                map.Add(new IoPointDefinition
                {
                    IoId = ioId++,
                    PhysicalAddress = "X" + ToOctal(n - 1),
                    DeviceName = $"真空负压表-{n}",
                    DeviceId = n,
                    Type = IoType.Input,
                    Function = IoFunction.VacuumPressure,
                    Electrical = ElectricalType.NPN,
                    LocalIndex = 1
                });
            }

            // ===== 1.1 预留输入点: X 地址继续顺延 =====
            // 例如：现场 TotalInputs=80, TotalBarometers=72，则预留输入为 73~80，对应 X110~X117
            for (int n = config.TotalBarometers + 1; n <= config.TotalInputs; n++)
            {
                map.Add(new IoPointDefinition
                {
                    IoId = ioId++,
                    PhysicalAddress = "X" + ToOctal(n - 1),
                    DeviceName = $"预留输入-{n}",
                    DeviceId = n,
                    Type = IoType.Input,
                    Function = IoFunction.Unknown,
                    Electrical = ElectricalType.NPN,
                    LocalIndex = 1
                });
            }

            // ===== 2. 输出点A: 真空电磁阀-N → Y 地址(八进制) =====
            // 地址规律: Y + octal(n-1), 即 n=1→Y000, n=72→Y107
            for (int n = 1; n <= config.TotalBarometers; n++)
            {
                map.Add(new IoPointDefinition
                {
                    IoId = ioId++,
                    PhysicalAddress = "Y" + ToOctal(n - 1),
                    DeviceName = $"真空电磁阀-{n}",
                    DeviceId = n,
                    Type = IoType.Output,
                    Function = IoFunction.VacuumValve,
                    Electrical = ElectricalType.PNP,
                    LocalIndex = 1
                });
            }

            // ===== 3. 输出点B: 载台上电-N → Y 地址(八进制) =====
            // 地址规律: Y + octal(totalBarometers + n - 1)
            // 即从 totalBarometers 的八进制地址开始(72→110)
            // n=1→Y110, n=8→Y117, n=9→Y120, n=72→Y217
            for (int n = 1; n <= config.TotalBarometers; n++)
            {
                map.Add(new IoPointDefinition
                {
                    IoId = ioId++,
                    PhysicalAddress = "Y" + ToOctal(config.TotalBarometers + n - 1),
                    DeviceName = $"载台上电-{n}",
                    DeviceId = n,
                    Type = IoType.Output,
                    Function = IoFunction.CarrierPower,
                    Electrical = ElectricalType.PNP,
                    LocalIndex = 2
                });
            }

            // ===== 3.1 预留输出点: Y 地址继续顺延 =====
            // 例如：现场 TotalOutputs=160, TotalBarometers=72，则预留输出为 145~160，对应 Y220~Y237
            int usedOutputs = config.TotalBarometers * 2;
            for (int n = usedOutputs + 1; n <= config.TotalOutputs; n++)
            {
                map.Add(new IoPointDefinition
                {
                    IoId = ioId++,
                    PhysicalAddress = "Y" + ToOctal(n - 1),
                    DeviceName = $"预留输出-{n}",
                    DeviceId = n,
                    Type = IoType.Output,
                    Function = IoFunction.Unknown,
                    Electrical = ElectricalType.PNP,
                    LocalIndex = 1
                });
            }

            return map;
        }

        /// <summary>
        /// 构建完整的IO映射表（兼容旧调用）
        /// </summary>
        public static List<IoPointDefinition> Build(int totalBarometers)
        {
            return Build(new DeviceConfig
            {
                TotalBarometers = totalBarometers,
                TotalInputs = totalBarometers,
                TotalOutputs = totalBarometers * 2
            });
        }

        /// <summary>
        /// 获取指定气压表的IO点映射(1输入 + 2输出)
        /// 供 BarometerPanelView 显示IO功能名和物理地址使用
        /// </summary>
        /// <param name="deviceId">气压表编号(1 ~ TotalBarometers)</param>
        /// <param name="totalBarometers">气压表总数</param>
        /// <param name="totalInputs">IO 输入通道总数（用于计算内部输出编号起点）</param>
        /// <returns>该设备的IO点映射集合</returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// deviceId 不在 [1, totalBarometers] 范围内, 或 totalBarometers 小于1
        /// </exception>
        public static DeviceIoMapping GetDeviceMapping(int deviceId, int totalBarometers, int totalInputs)
        {
            // 参数校验: 防止 deviceId 越界导致 ToOctal 生成非法地址(如 "X0-1")
            if (totalBarometers < 1)
            {
                throw new ArgumentOutOfRangeException(nameof(totalBarometers),
                    "气压表总数不能小于1");
            }
            if (totalInputs < totalBarometers)
            {
                throw new ArgumentOutOfRangeException(nameof(totalInputs),
                    "TotalInputs 不能小于 totalBarometers（每个气压表至少需要 1 个输入点）");
            }
            if (deviceId < 1 || deviceId > totalBarometers)
            {
                throw new ArgumentOutOfRangeException(nameof(deviceId),
                    $"设备编号 {deviceId} 超出合法范围 [1, {totalBarometers}]");
            }

            return new DeviceIoMapping
            {
                // 输入: 真空负压表, X + octal(deviceId-1)
                VacuumPressureInput = new IoPointDefinition
                {
                    IoId = deviceId,
                    PhysicalAddress = "X" + ToOctal(deviceId - 1),
                    DeviceName = $"真空负压表-{deviceId}",
                    DeviceId = deviceId,
                    Type = IoType.Input,
                    Function = IoFunction.VacuumPressure,
                    Electrical = ElectricalType.NPN,
                    LocalIndex = 1
                },
                // 输出1: 真空电磁阀, Y + octal(deviceId-1)
                VacuumValveOutput = new IoPointDefinition
                {
                    IoId = totalInputs + deviceId,
                    PhysicalAddress = "Y" + ToOctal(deviceId - 1),
                    DeviceName = $"真空电磁阀-{deviceId}",
                    DeviceId = deviceId,
                    Type = IoType.Output,
                    Function = IoFunction.VacuumValve,
                    Electrical = ElectricalType.PNP,
                    LocalIndex = 1
                },
                // 输出2: 载台上电, Y + octal(totalBarometers + deviceId - 1)
                CarrierPowerOutput = new IoPointDefinition
                {
                    IoId = totalInputs + totalBarometers + deviceId,
                    PhysicalAddress = "Y" + ToOctal(totalBarometers + deviceId - 1),
                    DeviceName = $"载台上电-{deviceId}",
                    DeviceId = deviceId,
                    Type = IoType.Output,
                    Function = IoFunction.CarrierPower,
                    Electrical = ElectricalType.PNP,
                    LocalIndex = 2
                }
            };
        }

        /// <summary>
        /// 将十进制数值转换为三菱PLC八进制地址字符串(3位, 前补零)
        /// 例如: 0→"000", 7→"007", 8→"010", 71→"107", 72→"110", 143→"217"
        /// </summary>
        /// <param name="decimalValue">十进制数值</param>
        /// <returns>3位八进制字符串</returns>
        private static string ToOctal(int decimalValue)
        {
            // Convert.ToString(value, 8) 将十进制转为八进制字符串(如 0→"0", 8→"10", 72→"110")
            // PadLeft(3, '0') 确保至少3位, 不足前补零(如 "0"→"000", "10"→"010")
            return System.Convert.ToString(decimalValue, 8).PadLeft(3, '0');
        }
    }
}
