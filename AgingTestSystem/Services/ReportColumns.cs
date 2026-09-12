
using System;
using System.Collections.Generic;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 报表列配置解析（【V1.74 新增】Q8 报表可配：列编排纯函数）。
    ///
    /// 【为什么是纯函数】列配置是用户手填的，脏输入必须在解析层洗掉或报错，
    /// 不能流到导出线程。SettingsForm 校验、历史窗导出、回归用例三方共用同一份
    /// vocabulary，改可用字段只改这里（与 MesMapping 同思路）。
    ///
    /// 【与 MES 映射的关系】MES vocabulary（MesMapping.FieldVocabulary）是
    /// "上报 payload 能带的字段"；这里是"历史 CSV 真实有的列"，11 个。
    /// 两套名单各管各的，不许互相引用，否则 CSV 加一列就要动 MES
    /// （V1.68 血泪：名单分叉即灵异 bug）。
    ///
    /// 【V1.76】SN/配方/结果结构化进 CSV（Q8 追溯口径），预设同步 11 列，
    /// 列序与 CSV 物理列序一致（时间→批号→SN→配方→工位→事件→结果→详情→
    /// 压力→温度→电流）。项目未上线，无老文件包袱，不做缺列兼容。
    /// </summary>
    public static class ReportColumns
    {
        /// <summary>报表列定义（显示名 = Excel 列头；字段 = 可用字段名小写）。</summary>
        public sealed class Column
        {
            public string Display;
            public string Field;
            public Column(string display, string field)
            {
                Display = display;
                Field = field;
            }
        }

        /// <summary>
        /// 可用字段 vocabulary（历史 CSV 真实有的列，大小写无所谓）：
        /// time=时间 / lot=批号 / sn=SN / recipe=配方 / device=工位号 /
        /// event=事件 / result=判定结果 / detail=详情 /
        /// pressure=压力kPa / temp=温度°C / current=电流A（V1.74，无表记空）
        /// </summary>
        public static readonly string[] AvailableFields = new string[]
        {
            "time", "lot", "sn", "recipe", "device", "event", "result", "detail",
            "pressure", "temp", "current"
        };

        /// <summary>
        /// 缺省预设（烧屏追溯惯例列序：身份→事件→判定→详情→三数；与 CSV 物理列序一致）。
        /// 配置留空 = 用这套（开关都不用开，客户改列才填）。
        /// </summary>
        public const string DefaultPreset =
            "时间=time;批号=lot;SN=sn;配方=recipe;工位=device;事件=event;结果=result;详情=detail;压力(kPa)=pressure;温度(°C)=temp;电流(A)=current";

        /// <summary>组分隔符（中英文分号/逗号/顿号都认，与 MesMapping 同口径）</summary>
        private static readonly char[] GroupSeparators = { ';', '；', ',', '，', '、' };

        /// <summary>
        /// 解析列配置（"显示名=字段"，如 "时间=time;批号=lot"）。
        /// - 空 = 缺省（调用方按 Resolve 走预设，这里返回空cols零errors）；
        /// - 字段未知 → 进 errors（SettingsForm 拦；导出跳过该列）；
        /// - 显示名为空/含空格/= → 进 errors；
        /// - 同一显示名出现两次 → 后者覆盖前者（记一条提醒）。
        /// </summary>
        /// <param name="raw">原始配置字符串</param>
        /// <param name="cols">解析出的列（保序）</param>
        /// <param name="errors">非法组描述</param>
        public static void Parse(string raw, out List<Column> cols, out List<string> errors)
        {
            cols = new List<Column>();
            errors = new List<string>();
            if (string.IsNullOrWhiteSpace(raw)) return;   // 空=缺省，调用方走 Resolve
            foreach (string item in raw.Split(GroupSeparators))
            {
                string g = (item ?? "").Trim();
                if (g.Length == 0) continue;
                int eq = g.IndexOf('=');
                if (eq <= 0 || eq >= g.Length - 1)
                {
                    errors.Add("非法列组（应为 显示名=字段）: " + g);
                    continue;
                }
                string display = g.Substring(0, eq).Trim();
                string field = g.Substring(eq + 1).Trim();
                if (display.Length == 0 || display.Contains(" ") || display.Contains("="))
                {
                    errors.Add("显示名非法（含空格/= 或为空）: " + g);
                    continue;
                }
                string canonical = null;
                foreach (string v in AvailableFields)
                {
                    if (string.Equals(v, field, StringComparison.OrdinalIgnoreCase)) { canonical = v; break; }
                }
                if (canonical == null)
                {
                    errors.Add("未知字段: " + field + "（合法: " + string.Join("/", AvailableFields) + "）");
                    continue;
                }
                bool dup = false;
                foreach (var c in cols)
                {
                    if (string.Equals(c.Display, display, StringComparison.Ordinal)) { c.Field = canonical; dup = true; break; }
                }
                if (dup)
                {
                    errors.Add("显示名重复，后者覆盖前者: " + display);
                }
                else
                {
                    cols.Add(new Column(display, canonical));
                }
            }
        }

        /// <summary>
        /// 解出实际生效的列（空配置/全错 → 缺省预设；部分错 → 对的照用，错的丢弃）。
        /// 导出线程调这个，不调 Parse（手改文件写错也不炸，永远有列可导）。
        /// </summary>
        public static List<Column> Resolve(string raw)
        {
            List<Column> cols;
            List<string> errors;
            Parse(raw, out cols, out errors);
            if (cols.Count > 0) return cols;
            // 空或全错：回缺省预设（预设是常量，解析必成功；防呆不断言）
            Parse(DefaultPreset, out cols, out errors);
            return cols;
        }
    }
}
