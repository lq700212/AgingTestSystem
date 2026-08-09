using System;
using System.Drawing;
using System.IO;
using Newtonsoft.Json;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// 工位面板布局配置（【V1.51】布局外部化）
    ///
    /// 【目的】
    /// 工位网格（WorkstationGridView）自绘时使用本配置中的所有坐标/颜色/字号/文字，
    /// 不再写死常量。这样现场微调界面（挪个框、换个色、改字号、改提示文字）只需
    /// 修改程序目录下的 PanelLayout.json 配置文件，**无需改代码、无需重新编译**。
    ///
    /// 【加载规则】
    /// 程序启动时调用 <see cref="LoadOrDefault"/>：
    /// - 若程序目录存在 PanelLayout.json → 读取并解析，失败时回退默认值；
    /// - 否则使用内置默认值（与历史版本布局完全一致）。
    /// 坐标单位均为"逻辑像素"，与 96DPI 下的设计坐标一致。
    /// 颜色统一用 "R,G,B" 字符串表示（如 "255,255,255"）。
    ///
    /// 【配置项说明】
    /// 面板内容按"设计尺寸 240×205"布局，72 个工位共用同一套面板模板，
    /// 所以只需编辑一份配置即可同时调整所有面板。
    /// </summary>
    public class PanelLayoutConfig
    {
        // ===================== 网格尺寸 =====================

        /// <summary>单个面板单元格列宽（面板内容 240 + 左右边距各 2 + 边框余量）</summary>
        public int PanelColumnWidth { get; set; } = 245;

        /// <summary>单个面板单元格行高（面板内容 205 + 上下边距各 2）</summary>
        public int PanelRowHeight { get; set; } = 225;

        /// <summary>最右侧"行全选"按钮列宽</summary>
        public int RowSelectButtonColumnWidth { get; set; } = 80;

        /// <summary>面板内容设计宽（每个面板实际绘制区域宽度）</summary>
        public int PanelInnerWidth { get; set; } = 240;

        /// <summary>面板内容设计高（每个面板实际绘制区域高度）</summary>
        public int PanelInnerHeight { get; set; } = 205;

        // ===================== 字体 =====================

        /// <summary>面板正文字体名（值必须是系统已安装的字体，如 微软雅黑/宋体）</summary>
        public string FontFamily { get; set; } = "微软雅黑";

        /// <summary>面板正文文字大小（单位：磅 pt）</summary>
        public float FontSize { get; set; } = 9f;

        /// <summary>设备编号标题字体大小（磅）</summary>
        public float TitleFontSize { get; set; } = 9f;

        /// <summary>设备编号标题是否加粗</summary>
        public bool TitleFontBold { get; set; } = true;

        // ===================== 面板内容坐标（相对面板左上角） =====================

        /// <summary>上电/下电状态块</summary>
        public ElementRect RcPower { get; set; } = new ElementRect { X = 57, Y = 29, Width = 52, Height = 23 };

        /// <summary>工作状态块</summary>
        public ElementRect RcWorkState { get; set; } = new ElementRect { X = 138, Y = 29, Width = 52, Height = 23 };

        /// <summary>真空开/关状态块</summary>
        public ElementRect RcVacuumOpen { get; set; } = new ElementRect { X = 138, Y = 67, Width = 48, Height = 21 };

        /// <summary>真空压力值框</summary>
        public ElementRect RcPressureValue { get; set; } = new ElementRect { X = 57, Y = 67, Width = 78, Height = 21 };

        /// <summary>SN 值框</summary>
        public ElementRect RcSNValue { get; set; } = new ElementRect { X = 57, Y = 93, Width = 140, Height = 21 };

        /// <summary>配方值框</summary>
        public ElementRect RcRecipeValue { get; set; } = new ElementRect { X = 57, Y = 118, Width = 140, Height = 21 };

        /// <summary>延时开启值框</summary>
        public ElementRect RcDelayStartValue { get; set; } = new ElementRect { X = 57, Y = 143, Width = 80, Height = 21 };

        /// <summary>延时到达值框</summary>
        public ElementRect RcDelayArriveValue { get; set; } = new ElementRect { X = 57, Y = 168, Width = 80, Height = 21 };

        /// <summary>"设置"按钮区域</summary>
        public ElementRect RcSetButton { get; set; } = new ElementRect { X = 145, Y = 145, Width = 60, Height = 50 };

        /// <summary>右上角选中指示框</summary>
        public ElementRect RcSelectBox { get; set; } = new ElementRect { X = 212, Y = 4, Width = 23, Height = 23 };

        /// <summary>设备编号文字位置（相对面板左上角）</summary>
        public ElementPoint TitlePosition { get; set; } = new ElementPoint { X = 3, Y = 4 };

        /// <summary>静态标签"真空压力"位置</summary>
        public ElementPoint LabelPressurePosition { get; set; } = new ElementPoint { X = 3, Y = 70 };

        /// <summary>静态标签"SN:"位置</summary>
        public ElementPoint LabelSnPosition { get; set; } = new ElementPoint { X = 3, Y = 96 };

        /// <summary>静态标签"配方:"位置</summary>
        public ElementPoint LabelRecipePosition { get; set; } = new ElementPoint { X = 3, Y = 121 };

        /// <summary>静态标签"延时开启"位置</summary>
        public ElementPoint LabelDelayStartPosition { get; set; } = new ElementPoint { X = 3, Y = 146 };

        /// <summary>静态标签"延时到达"位置</summary>
        public ElementPoint LabelDelayArrivePosition { get; set; } = new ElementPoint { X = 3, Y = 171 };

        // ===================== 文字内容 =====================

        /// <summary>"设置"按钮文字</summary>
        public string SetButtonText { get; set; } = "设置";

        /// <summary>行全选按钮文字（整行全选中时显示"取消"，否则显示本文字）</summary>
        public string RowSelectAllText { get; set; } = "全选";

        /// <summary>行全选按钮文字（整行全选中时显示）</summary>
        public string RowSelectCancelText { get; set; } = "取消";

        /// <summary>选中指示符号（选中时显示，如 ✓）</summary>
        public string SelectedMarkText { get; set; } = "✓";

        /// <summary>值框内文字左内边距（px）：让文本与值框左边框留出间隔，避免"贴边"（V1.52 新增）</summary>
        public int ValueTextLeftPadding { get; set; } = 6;

        // ===================== 颜色（"R,G,B" 格式） =====================

        /// <summary>面板背景色-空闲（白）</summary>
        public string ColorNormalBackground { get; set; } = "255,255,255";

        /// <summary>面板背景色-测试中（浅黄）</summary>
        public string ColorTestingBackground { get; set; } = "255,255,224";

        /// <summary>面板背景色-故障（浅粉）</summary>
        public string ColorFaultBackground { get; set; } = "255,192,203";

        /// <summary>上电状态块背景色（绿）</summary>
        public string ColorPowerOn { get; set; } = "50,205,50";

        /// <summary>下电状态块背景色（浅灰）</summary>
        public string ColorPowerOff { get; set; } = "211,211,211";

        /// <summary>真空开状态块背景色（绿）</summary>
        public string ColorVacuumOn { get; set; } = "50,205,50";

        /// <summary>真空关状态块背景色（浅灰）</summary>
        public string ColorVacuumOff { get; set; } = "211,211,211";

        /// <summary>工作状态-故障（红）</summary>
        public string ColorWorkFault { get; set; } = "255,0,0";

        /// <summary>工作状态-繁忙/测试中（金黄）</summary>
        public string ColorWorkBusy { get; set; } = "255,215,0";

        /// <summary>工作状态-选中/已上电待测试（橙）</summary>
        public string ColorWorkSelected { get; set; } = "255,165,0";

        /// <summary>工作状态-空闲（绿）</summary>
        public string ColorWorkIdle { get; set; } = "50,205,50";

        /// <summary>"设置"按钮背景色（绿）</summary>
        public string ColorSetButton { get; set; } = "50,205,50";

        /// <summary>行全选按钮背景色（浅灰）</summary>
        public string ColorRowSelectButton { get; set; } = "211,211,211";

        /// <summary>值框背景色（白）</summary>
        public string ColorValueBox { get; set; } = "255,255,255";

        /// <summary>值框/正文文字颜色（黑）</summary>
        public string ColorText { get; set; } = "0,0,0";

        /// <summary>边框颜色（黑）</summary>
        public string ColorBorder { get; set; } = "0,0,0";

        // ===================== 加载与默认 =====================

        /// <summary>
        /// 从程序目录加载 PanelLayout.json；文件不存在或解析失败时返回内置默认配置。
        /// 默认配置与历史版本的坐标/颜色/字号完全一致，保证升级后界面不变。
        /// </summary>
        public static PanelLayoutConfig LoadOrDefault()
        {
            string path = GetConfigPath();
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                    var cfg = JsonConvert.DeserializeObject<PanelLayoutConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch (Exception)
            {
                // 配置损坏时静默回退默认值，避免程序无法启动
            }
            return new PanelLayoutConfig();
        }

        /// <summary>
        /// 把当前配置写入程序目录的 PanelLayout.json（供导出默认配置/备份使用）。
        /// 便于用户先导出"默认配置"，再按需修改。
        /// </summary>
        public void SaveDefault()
        {
            try
            {
                string path = GetConfigPath();
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
            }
            catch (Exception)
            {
                // 写入失败不阻断主流程
            }
        }

        /// <summary>配置文件路径（程序运行目录下的 PanelLayout.json）</summary>
        public static string GetConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PanelLayout.json");
        }

        /// <summary>把 "R,G,B" 字符串转成 Color；解析失败返回 fallback</summary>
        public static Color ParseColor(string rgb, Color fallback)
        {
            if (string.IsNullOrWhiteSpace(rgb)) return fallback;
            string[] parts = rgb.Split(',');
            if (parts.Length >= 3
                && int.TryParse(parts[0].Trim(), out int r)
                && int.TryParse(parts[1].Trim(), out int g)
                && int.TryParse(parts[2].Trim(), out int b))
            {
                return Color.FromArgb(Clamp(r), Clamp(g), Clamp(b));
            }
            return fallback;
        }

        /// <summary>把 Color 转成 "R,G,B" 字符串（用于导出配置）</summary>
        public static string ToColorString(Color c)
        {
            return $"{c.R},{c.G},{c.B}";
        }

        private static int Clamp(int v)
        {
            return v < 0 ? 0 : (v > 255 ? 255 : v);
        }
    }

    /// <summary>
    /// 矩形坐标（用于 JSON 序列化，替代不可直接序列化的 System.Drawing.Rectangle）
    /// </summary>
    public class ElementRect
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>转成 System.Drawing.Rectangle 供绘制使用</summary>
        public Rectangle ToRectangle()
        {
            return new Rectangle(X, Y, Width, Height);
        }
    }

    /// <summary>
    /// 点坐标（用于 JSON 序列化，替代 System.Drawing.Point）
    /// </summary>
    public class ElementPoint
    {
        public int X { get; set; }
        public int Y { get; set; }

        /// <summary>转成 System.Drawing.Point 供绘制使用</summary>
        public Point ToPoint()
        {
            return new Point(X, Y);
        }
    }
}
