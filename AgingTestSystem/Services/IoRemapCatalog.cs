using System;
using System.Collections.Generic;
using AgingTestSystem.Models;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 备用映射目标池模式（可视化连线页右侧点位池的范围）。
    /// 以后现场备用点不够用，加新模式（如 ByRegister 指定寄存器）只加枚举 + BuildTargetEndpoints 分支，
    /// 调用方（下拉框/用例）跟着加一项即可，不动连线与保存逻辑。
    /// </summary>
    public enum IoRemapTargetPool
    {
        /// <summary>仅预留输出（Function=Unknown 的输出点，默认 Y220~Y237；最安全，不会抢正常通道）</summary>
        SpareOnly,

        /// <summary>全部空闲输出（所有输出点，占用态由调用方按映射表标徽标；适合备用点用完的现场）</summary>
        AllFreeOutputs
    }

    /// <summary>
    /// 映射端点（连线图左右两列的一个"点位"：寄存器 + 通道 + 显示名）。
    /// 数据全部来自 IoMapBuilder，改 TotalBarometers/TotalInputs/TotalOutputs 配置自动适应，
    /// 不手写地址（V1.80 预留页同思路）。
    /// </summary>
    public sealed class IoRemapEndpoint
    {
        /// <summary>寄存器绝对地址（如 0x2000）</summary>
        public ushort Register;

        /// <summary>通道号（0~15，0 = 第 1 路）</summary>
        public int Channel;

        /// <summary>PLC 物理地址（如 Y000 / Y220，八进制显示）</summary>
        public string IoName;

        /// <summary>设备名（如 真空电磁阀-1 / 预留输出-145）</summary>
        public string DeviceName;

        /// <summary>业务功能（决定左侧分组：真空电磁阀 / 载台上电 / 预留输出）</summary>
        public IoFunction Function;

        /// <summary>内部连续编号 IoId（同 IoPointDefinition 口径，排查用）</summary>
        public int IoId;

        /// <summary>字典键（"寄存器:通道"，如 "8192:0"，8192 = 0x2000；连线/查表统一用它）</summary>
        public string Key
        {
            get { return Register + ":" + Channel; }
        }

        /// <summary>分组标题（左侧按功能分组显示用）</summary>
        public string GroupTitle
        {
            get { return IoRemapCatalog.GroupTitleOf(Function); }
        }

        /// <summary>短显示（"Y000 真空电磁阀-1"，节点正文用）</summary>
        public string DisplayText
        {
            get { return IoName + " " + DeviceName; }
        }

        /// <summary>全显示（"Y000 真空电磁阀-1 (0x2000@0x00)"，提示/列表用）</summary>
        public string FullText
        {
            get { return string.Format("{0} {1} ({2})", IoName, DeviceName, IoRemapValidator.Describe(Register, Channel)); }
        }
    }

    /// <summary>
    /// 映射点位目录（纯静态、无 UI、可单测）：从配置算出"可连线的源池/目标池"。
    ///
    /// 【源池】全部输出点（真空电磁阀 + 载台上电 + 预留输出）：预留 DO 本身坏了也要能映射，
    /// 且以后加新功能输出（如破空阀独立成路）自动进池，调用方按 GroupTitle 过滤即可。
    ///
    /// 【目标池】默认仅预留输出（SpareOnly，用户评审结论：防误抢正常通道）；
    /// AllFreeOutputs 兜底备用点用完的现场（占用徽标由连线页按映射表标，不在这里过滤，
    /// 否则切池会丢已配映射的显示）。
    ///
    /// 【寄存器换算口径】与 ModbusTcpIoController / SpareGrid.RegBitOf 一致：
    /// 输入 IoId=1→输入起始+0/bit0；输出 TotalInputs+1→输出起始+0/bit0。
    /// 以后改编址只改这里 + 那两处（搜索 RegBitOf 定位）。
    /// </summary>
    public static class IoRemapCatalog
    {
        /// <summary>
        /// 由 IO 点定义反推寄存器地址与位（与 SpareGrid.RegBitOf 同口径，抽到这里供新代码复用；
        /// 老代码保持不动，减小回归面）。
        /// </summary>
        public static void RegBitOf(DeviceConfig config, IoPointDefinition p, out int reg, out int bit)
        {
            if (p.Type == IoType.Input)
            {
                int bitIndex = p.IoId - 1;
                reg = config.IoInputRegisterStartAddress + bitIndex / 16;
                bit = bitIndex % 16;
            }
            else
            {
                int outIndex = p.IoId - 1 - config.TotalInputs;
                reg = config.IoOutputRegisterStartAddress + outIndex / 16;
                bit = outIndex % 16;
            }
        }

        /// <summary>
        /// 构建源池（全部输出点，按寄存器→通道排序；配置非法时返回空表，调用方显示空态）。
        /// </summary>
        public static List<IoRemapEndpoint> BuildSourceEndpoints(DeviceConfig config)
        {
            var result = new List<IoRemapEndpoint>();
            if (config == null) return result;
            List<IoPointDefinition> map;
            try
            {
                map = IoMapBuilder.Build(config);
            }
            catch
            {
                // 配置非法（如总数对不上）：返回空，调用方显示空态 + 提示去系统设置改总数
                return result;
            }
            foreach (var p in map)
            {
                if (p == null || p.Type != IoType.Output) continue;
                RegBitOf(config, p, out int reg, out int bit);
                result.Add(new IoRemapEndpoint
                {
                    Register = (ushort)reg,
                    Channel = bit,
                    IoName = p.PhysicalAddress,
                    DeviceName = p.DeviceName,
                    Function = p.Function,
                    IoId = p.IoId
                });
            }
            result.Sort(CompareEndpoints);
            return result;
        }

        /// <summary>
        /// 构建目标池（SpareOnly=仅预留输出；AllFreeOutputs=全部输出；排序与源池一致）。
        /// </summary>
        public static List<IoRemapEndpoint> BuildTargetEndpoints(DeviceConfig config, IoRemapTargetPool pool)
        {
            var all = BuildSourceEndpoints(config);
            if (pool == IoRemapTargetPool.AllFreeOutputs) return all;
            var result = new List<IoRemapEndpoint>();
            foreach (var e in all)
            {
                if (e.Function == IoFunction.Unknown) result.Add(e);
            }
            return result;
        }

        /// <summary>
        /// 功能分组标题（左侧分组/筛选下拉共用；加新功能时改这里 + 下拉项 + 用例）。
        /// </summary>
        public static string GroupTitleOf(IoFunction function)
        {
            switch (function)
            {
                case IoFunction.VacuumValve: return "真空电磁阀";
                case IoFunction.CarrierPower: return "载台上电";
                default: return "预留输出";
            }
        }

        /// <summary>排序：寄存器升序 → 通道升序（连线图左右列都按物理顺序排，所见即地址序）</summary>
        private static int CompareEndpoints(IoRemapEndpoint a, IoRemapEndpoint b)
        {
            int c = a.Register.CompareTo(b.Register);
            if (c != 0) return c;
            return a.Channel.CompareTo(b.Channel);
        }
    }
}
