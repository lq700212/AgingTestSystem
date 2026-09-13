using System;
using System.Collections.Generic;
using System.Text;
using AgingTestSystem.Models;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// IO 备用通道映射校验器（纯静态、无 UI、无 IO，可直接单测）。
    ///
    /// 【为什么单独抽一层】
    /// 备用映射现在有三个入口会"新建/删除"映射：①系统设置表的表格编辑器
    /// （IoMappingEditorPopup）、②可视化连线页（IoRemapVisualForm）、③通讯测试窗的
    /// 右键菜单（CommunicationTestForm）。校验规则（源唯一、目标独占、自环、通道越界）
    /// 若在三处各写一份，改规则必漏。以后只改这里，三处行为一起变。
    ///
    /// 【目标独占规则（V1.81 新增）】
    /// 一个目标通道只允许服务一个源：两个源同时写同一个目标位等于"线或"在一起，
    /// 一个通道动作会带出另一个的假信号。老配置里若已有"多源同目标"照常加载执行
    /// （ParseAll 不拦，避免升级后现场行为突变），但新建连线一律拦截并明示。
    ///
    /// 【怎么改】
    /// 加新规则就在 ValidateNewMapping 里加一条 if 并配一条回归用例；删规则要同步改
    /// 三个调用方（搜索 ValidateNewMapping 即可定位）。
    /// </summary>
    public static class IoRemapValidator
    {
        /// <summary>
        /// 校验一条新建映射是否合法（查 existing 是否冲突）。
        /// </summary>
        /// <param name="existing">已有映射表（可为 null，视为无映射）</param>
        /// <param name="srcReg">源寄存器地址（如 0x2000）</param>
        /// <param name="srcCh">源通道号（0~15，0 = 第 1 路）</param>
        /// <param name="dstReg">目标寄存器地址（如 0x2009）</param>
        /// <param name="dstCh">目标通道号（0~15）</param>
        /// <param name="error">不合法时的中文原因（合法时为 null，调用方直接弹框展示）</param>
        /// <returns>true=合法可建，false=被拦截</returns>
        public static bool ValidateNewMapping(List<IoOutputChannelRemap> existing,
            int srcReg, int srcCh, int dstReg, int dstCh, out string error)
        {
            error = null;

            // 1) 通道号越界：一个寄存器只有 16 个 bit（V1.62 血泪，解析层同口径）
            if (srcCh < 0 || srcCh > 15)
            {
                error = string.Format("源通道应为 0x00~0x0F（0x00 = 第 1 路），当前为 {0}", srcCh);
                return false;
            }
            if (dstCh < 0 || dstCh > 15)
            {
                error = string.Format("目标通道应为 0x00~0x0F（0x00 = 第 1 路），当前为 {0}", dstCh);
                return false;
            }

            // 2) 自环：源与目标相同等于没换位置，建了也白建
            if (srcReg == dstReg && srcCh == dstCh)
            {
                error = "源与目标相同（没换位置），无需映射";
                return false;
            }

            if (existing == null) return true;

            // 3) 源唯一：一个源只能映射到一个目标（重复建就是改目标，请先删旧的）
            foreach (var m in existing)
            {
                if (m == null) continue;
                if (m.SourceRegister == srcReg && m.SourceChannel == srcCh)
                {
                    error = string.Format("源 {0} 已映射到 {1}，如需改目标请先删除旧映射",
                        Describe(srcReg, srcCh), Describe(m.TargetRegister, m.TargetChannel));
                    return false;
                }
            }

            // 4) 目标独占：一个目标只服务一个源（见类头注释，多源同目标会"线或"误动作）
            foreach (var m in existing)
            {
                if (m == null) continue;
                if (m.TargetRegister == dstReg && m.TargetChannel == dstCh)
                {
                    error = string.Format("目标 {0} 已被源 {1} 占用，一个备用通道只允许接一个源",
                        Describe(dstReg, dstCh), Describe(m.SourceRegister, m.SourceChannel));
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// 按源查找映射（找不到返回 null；调用方用它判断"该通道是否已映射"）。
        /// </summary>
        public static IoOutputChannelRemap FindBySource(List<IoOutputChannelRemap> existing, int srcReg, int srcCh)
        {
            if (existing == null) return null;
            foreach (var m in existing)
            {
                if (m == null) continue;
                if (m.SourceRegister == srcReg && m.SourceChannel == srcCh) return m;
            }
            return null;
        }

        /// <summary>
        /// 判断某目标是否已被占用（exclude 指定的源除外，用于"改目标"场景自查）。
        /// </summary>
        public static bool IsTargetUsed(List<IoOutputChannelRemap> existing, int dstReg, int dstCh)
        {
            if (existing == null) return false;
            foreach (var m in existing)
            {
                if (m == null) continue;
                if (m.TargetRegister == dstReg && m.TargetChannel == dstCh) return true;
            }
            return false;
        }

        /// <summary>
        /// 返回删掉指定源之后的新表（不改原表，调用方拿返回的新表去保存；
        /// 源不存在时原样返回等价拷贝）。
        /// </summary>
        public static List<IoOutputChannelRemap> WithoutSource(List<IoOutputChannelRemap> existing, int srcReg, int srcCh)
        {
            var result = new List<IoOutputChannelRemap>();
            if (existing == null) return result;
            foreach (var m in existing)
            {
                if (m == null) continue;
                if (m.SourceRegister == srcReg && m.SourceChannel == srcCh) continue;
                result.Add(m);
            }
            return result;
        }

        /// <summary>
        /// 把映射表序列化回 App.config 配置格式（与 IoOutputChannelRemap.ParseAll 互逆）：
        /// "0x2000@0x00-&gt;0x2009@0x00;0x2008@0x01-&gt;0x2009@0x01"，空表返回空串。
        /// 格式细节（十六进制 + 0x 前缀 + 分号分隔）与 IoMappingEditorPopup.Confirm 同口径，
        /// 以后改格式只改这里 + ParseAll。
        /// </summary>
        public static string Serialize(List<IoOutputChannelRemap> mappings)
        {
            if (mappings == null || mappings.Count == 0) return string.Empty;
            var sb = new StringBuilder();
            bool first = true;
            foreach (var m in mappings)
            {
                if (m == null) continue;
                if (!first) sb.Append(';');
                first = false;
                sb.AppendFormat("0x{0:X4}@0x{1:X2}->0x{2:X4}@0x{3:X2}",
                    m.SourceRegister, m.SourceChannel, m.TargetRegister, m.TargetChannel);
            }
            return sb.ToString();
        }

        /// <summary>
        /// 单端描述（"0x2000@0x00"，与配置/界面显示同格式），供提示文案拼装。
        /// </summary>
        public static string Describe(int reg, int ch)
        {
            return string.Format("0x{0:X4}@0x{1:X2}", reg, ch);
        }
    }
}
