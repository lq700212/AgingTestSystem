using System;
using System.Collections.Generic;
using System.Configuration;
using System.Globalization;

namespace ModbusTcpIoControllerTest
{
    /// <summary>
    /// IO 输出"备用通道映射"工具（测试 Demo 用）
    ///
    /// 与生产工程 BarometerWinform 的 Models/IoOutputChannelRemap 语义完全一致：
    /// 现场某个 DQ 输出通道烧毁 / 电压不足后，把该通道的信号改写到备用通道。
    ///
    /// 【为什么留开关】
    /// 本 Demo 与生产程序会复用到多个工作台，**多数工作台没有烧通道**，
    /// 所以默认关闭（IoBackupChannelMappingEnabled = false），不开开关就一切照旧。
    ///
    /// 【配置来源】本工程 App.config 的 appSettings：
    ///   IoBackupChannelMappingEnabled = true / false（总开关）
    ///   IoBackupChannelMappings = 0x2000@0->0x2009@10;0x2008@0->0x2009@11
    ///     （源寄存器@源通道 -> 目标寄存器@目标通道，多组用分号分隔）
    /// </summary>
    public static class OutputChannelRemap
    {
        // 一条映射：源(寄存器,通道) → 目标(寄存器,通道)
        private sealed class RemapEntry
        {
            public int SrcReg;
            public int SrcCh;
            public int DstReg;
            public int DstCh;
        }

        private static readonly bool _enabled;
        private static readonly List<RemapEntry> _maps = new List<RemapEntry>();

        /// <summary>备用通道映射是否启用（对应配置 IoBackupChannelMappingEnabled）</summary>
        public static bool Enabled => _enabled;

        static OutputChannelRemap()
        {
            // 1) 读总开关（App.config）
            bool.TryParse(ConfigurationManager.AppSettings["IoBackupChannelMappingEnabled"], out _enabled);
            if (!_enabled) return;

            // 2) 读映射表字符串并解析
            string raw = ConfigurationManager.AppSettings["IoBackupChannelMappings"];
            if (string.IsNullOrWhiteSpace(raw)) return;

            foreach (string item in raw.Split(new[] { ';', '；' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string token = item.Trim();
                if (token.Length == 0) continue;

                // 拆成左右两半：源 -> 目标
                string[] sides = token.Split(new[] { "->", "→" }, StringSplitOptions.None);
                if (sides.Length != 2) continue;

                if (TryParseEndpoint(sides[0], out int srcReg, out int srcCh) &&
                    TryParseEndpoint(sides[1], out int dstReg, out int dstCh))
                {
                    _maps.Add(new RemapEntry { SrcReg = srcReg, SrcCh = srcCh, DstReg = dstReg, DstCh = dstCh });
                }
            }
        }

        /// <summary>
        /// 查询某物理通道是否被映射，返回映射后的目标 (寄存器, 通道)。
        /// 未启用 / 未匹配时原样返回。
        /// </summary>
        /// <param name="reg">源寄存器地址（如 0x2000）</param>
        /// <param name="bit">源通道号（0~15）</param>
        /// <returns>映射后的 (目标寄存器, 目标通道)</returns>
        public static (int reg, int bit) Map(int reg, int bit)
        {
            if (!_enabled) return (reg, bit);
            foreach (var m in _maps)
            {
                if (m.SrcReg == reg && m.SrcCh == bit)
                    return (m.DstReg, m.DstCh);
            }
            return (reg, bit);
        }

        /// <summary>
        /// 该物理通道是否是某个映射的源（用于写整个寄存器时把源位剔除）。
        /// </summary>
        public static bool IsSource(int reg, int bit)
        {
            if (!_enabled) return false;
            foreach (var m in _maps)
            {
                if (m.SrcReg == reg && m.SrcCh == bit) return true;
            }
            return false;
        }

        /// <summary>
        /// 某个寄存器里是否有被映射的源通道（用于循环测试结束时一并清目标寄存器）。
        /// </summary>
        public static bool HasSourceInRegister(int reg)
        {
            if (!_enabled) return false;
            foreach (var m in _maps)
            {
                if (m.SrcReg == reg) return true;
            }
            return false;
        }

        /// <summary>
        /// 源寄存器对应的目标寄存器（同一个源寄存器假设只有一个目标；多目标时返回第一个）。
        /// </summary>
        public static int TargetRegForSource(int reg)
        {
            foreach (var m in _maps)
            {
                if (m.SrcReg == reg) return m.DstReg;
            }
            return reg;
        }

        /// <summary>
        /// 解析 "0x2000@0" 形式的寄存器@通道。
        /// </summary>
        private static bool TryParseEndpoint(string s, out int reg, out int channel)
        {
            reg = 0;
            channel = 0;
            string[] parts = s.Trim().Split('@');
            if (parts.Length != 2) return false;

            string regStr = parts[0].Trim();
            // 兼容带 / 不带 0x 前缀
            if (regStr.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ||
                regStr.StartsWith("0X", StringComparison.OrdinalIgnoreCase))
            {
                regStr = regStr.Substring(2);
            }

            if (!int.TryParse(regStr, NumberStyles.HexNumber, null, out reg)) return false;
            if (!int.TryParse(parts[1].Trim(), out channel)) return false;
            return channel >= 0 && channel <= 15;
        }
    }
}
