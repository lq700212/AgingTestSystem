using System;
using System.IO;
using Newtonsoft.Json;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// 主页布局配置（【V1.58】主页区域可视化调整）
    ///
    /// 【目的】
    /// 主界面（MainForm）的几大区域尺寸——顶部标题栏高、菜单栏高、右侧状态按钮区宽、
    /// 底部状态栏高——统一收敛到本配置，不再写死。现场微调主界面（比如觉得右侧
    /// "运行状态/监视/操作/日志"区域太宽、想缩窄给工作站列表腾地方）只需在
    /// "关于 → 主页区域调整"可视化编辑器里拖动矩形块边缘，保存即写入
    /// 程序目录下的 HomeLayout.json，无需改代码、无需重新编译。
    ///
    /// 【默认值说明（V1.58 调大）】
    /// 默认标题栏/菜单栏/状态栏高度调大（40/50/30），比最初版本的 30/40/25 更高更易点按，
    /// 适配现场"嫌标题栏和顶部标题栏太小"的反馈。现场若不满意仍可在编辑器里继续调整。
    ///
    /// 【布局结构】（与 MainForm.Designer.cs 的 tableLayoutPanelMain 对应）
    /// ┌───────────────────────────────────────┐
    /// │ 顶部标题栏  TopBarHeight（默认 40）    │
    /// ├───────────────────────────────────────┤
    /// │ 菜单栏      MenuHeight（默认 50）       │
    /// ├──────────────────────────┬────────────┤
    /// │                          │ 右侧状态按钮区│
    /// │  工作站列表面板（自动占满   │ RightPanelW │
    /// │  剩余宽度，无需配置）       │ idth（默认  │
    /// │                          │ 260）        │
    /// ├──────────────────────────┴────────────┤
    /// │ 状态栏  StatusBarHeight（默认 30）      │
    /// └───────────────────────────────────────┘
    /// 说明：
    /// - 工作站列表面板（splitContainerMain.Panel1）宽度 = 主窗体总宽 - 右侧宽度 - 分隔条宽，
    ///   属于"剩余空间"，所以只需配置右侧宽度，左侧自动跟着变。
    /// - 右侧区域包含：运行状态、监视、操作、日志 四个 GroupBox（tableLayoutPanelRight）。
    /// - 若右侧宽度小于内容需要（如操作按钮），MainForm 会把操作按钮宽度同步缩放（自适应）。
    ///
    /// 【加载规则】
    /// 程序启动时调用 <see cref="LoadOrDefault"/>：
    /// - 若程序目录存在 HomeLayout.json → 读取解析，失败回退默认；
    /// - 否则使用内置默认值。
    /// 坐标单位均为"逻辑像素"，与 96DPI 下的设计坐标一致。
    /// </summary>
    public class HomeLayoutConfig
    {
        /// <summary>顶部标题栏高度（显示标题/权限/通讯状态的一行）</summary>
        public int TopBarHeight { get; set; } = 40;

        /// <summary>菜单栏高度（用户权限/参数/日志/关于 一排按钮）</summary>
        public int MenuHeight { get; set; } = 50;

        /// <summary>右侧状态按钮区宽度（运行状态+监视+操作+日志 四块的总宽）</summary>
        public int RightPanelWidth { get; set; } = 260;

        /// <summary>底部状态栏高度（设备数量/采集间隔/当前时间）</summary>
        public int StatusBarHeight { get; set; } = 30;

        // ===================== 调整范围约束 =====================
        // 可视化编辑器拖动矩形块边缘时用这些上下限做钳制，
        // 防止把某个区域拖成 0 或超出合理范围导致主界面错乱。
        // 与 HomeLayoutEditorForm 中的范围常量保持同步。

        /// <summary>顶部标题栏高度最小/最大值</summary>
        [JsonIgnore]
        public static readonly (int Min, int Max) TopBarRange = (15, 80);

        /// <summary>菜单栏高度最小/最大值</summary>
        [JsonIgnore]
        public static readonly (int Min, int Max) MenuRange = (25, 100);

        /// <summary>右侧状态按钮区宽度最小/最大值</summary>
        [JsonIgnore]
        public static readonly (int Min, int Max) RightPanelRange = (180, 600);

        /// <summary>底部状态栏高度最小/最大值</summary>
        [JsonIgnore]
        public static readonly (int Min, int Max) StatusBarRange = (15, 60);

        // ===================== 加载与保存 =====================

        /// <summary>
        /// 从程序目录加载 HomeLayout.json；文件不存在或解析失败时返回内置默认配置。
        /// </summary>
        public static HomeLayoutConfig LoadOrDefault()
        {
            string path = GetConfigPath();
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path, System.Text.Encoding.UTF8);
                    var cfg = JsonConvert.DeserializeObject<HomeLayoutConfig>(json);
                    if (cfg != null) return cfg;
                }
            }
            catch (Exception)
            {
                // 配置损坏时静默回退默认值，避免程序无法启动
            }
            return new HomeLayoutConfig();
        }

        /// <summary>
        /// 把当前配置写入程序目录的 HomeLayout.json。
        /// 写入失败不抛异常（界面调整是锦上添花，不应阻断主流程）。
        /// </summary>
        public void Save()
        {
            try
            {
                string path = GetConfigPath();
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
            }
            catch (Exception)
            {
                // 写入失败静默忽略
            }
        }

        /// <summary>配置文件路径（程序运行目录下的 HomeLayout.json）</summary>
        public static string GetConfigPath()
        {
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "HomeLayout.json");
        }
    }
}
