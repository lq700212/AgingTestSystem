using System;
using System.IO;
using AgingTestSystem.Services;
using Newtonsoft.Json;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// 主页布局配置（【V1.58】主页区域可视化调整）
    ///
    /// 【目的】
    /// 主界面（MainForm）的几大区域尺寸——顶栏（项目/权限/通讯＋4 按钮单行）高、
    /// 右侧状态按钮区宽、底部状态栏高——统一收敛到本配置，不再写死。
    /// 现场微调主界面（比如觉得右侧
    /// "运行状态/监视/操作/日志"区域太宽、想缩窄给工作站列表腾地方）只需在
    /// "关于 → 主页区域调整"可视化编辑器里拖动矩形块边缘，保存即写入
    /// 程序目录下的 HomeLayout.json，无需改代码、无需重新编译。
    ///
    /// 【默认值说明（V1.58 调大，V1.88.23 顶栏菜单并单行）】
    /// 默认顶栏/状态栏高度（36/30）：V1.58 曾把标题栏/菜单栏调大到 40/50 好点按；
    /// V1.88.23 应"按钮太占位置"把两行并成一行（项目/权限/通讯＋4 按钮同行 36px，
    /// 省 34px 纵向还给工作站区），旧 TopBarHeight/MenuHeight 双键删除、单 HeaderHeight 替代。
    /// 老 HomeLayout.json 里没有 HeaderHeight 键 → 反序列化保持类缺省 36，
    /// 直接生效，不写迁移分支（项目未上线，旧文件删了重导也行）。
    ///
    /// 【布局结构】（与 MainForm.Designer.cs 的 tableLayoutPanelMain 对应）
    /// ┌───────────────────────────────────────┐
    /// │ 顶栏 HeaderHeight（默认 36：项目/权限/  │
    /// │ 通讯＋用户权限/参数设置/日志记录/关于） │
    /// ├──────────────────────────┬────────────┤
    /// │                          │ 右侧状态按钮区│
    /// │  工作站列表面板（自动占满   │ RightPanelW │
    /// │  剩余宽度，无需配置）       │ idth（无配  │
    /// │                          │ 置跟窗口23% │
    /// │                          │ 有配置用文件│
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
        /// <summary>
        /// 顶栏高度（【V1.88.23】单行：项目/权限/通讯＋4 按钮同行；默认 36）。
        /// 旧 TopBarHeight/MenuHeight 双键已删（两行并一行，省 34px 纵向还给工作站区）；
        /// 老文件无此键即 36，不迁移。
        /// </summary>
        public int HeaderHeight { get; set; } = 36;

        /// <summary>右侧状态按钮区宽度（运行状态+监视+操作+日志 四块的总宽）。
        /// 【V1.65】类默认值 240 只作编辑器"恢复默认"的基准；主窗体无 json 时实际按
        /// 窗口 23.4% 比例自适应（见 MainForm.ComputeRightPanelWidth），有 json 时以文件值为准。</summary>
        public int RightPanelWidth { get; set; } = 240;

        /// <summary>底部状态栏高度（设备数量/采集间隔/当前时间）</summary>
        public int StatusBarHeight { get; set; } = 30;

        // ===================== 调整范围约束 =====================
        // 可视化编辑器拖动矩形块边缘时用这些上下限做钳制，
        // 防止把某个区域拖成 0 或超出合理范围导致主界面错乱。
        // 与 HomeLayoutEditorForm 中的范围常量保持同步。

        /// <summary>顶栏高度最小/最大值（【V1.88.23】34=按钮28＋上下各3边距，再小裁按钮）</summary>
        [JsonIgnore]
        public static readonly (int Min, int Max) HeaderRange = (34, 100);

        /// <summary>右侧状态按钮区宽度最小/最大值</summary>
        [JsonIgnore]
        public static readonly (int Min, int Max) RightPanelRange = (180, 600);

        /// <summary>底部状态栏高度最小/最大值</summary>
        [JsonIgnore]
        public static readonly (int Min, int Max) StatusBarRange = (15, 60);

        // ===================== 加载与保存 =====================

        /// <summary>
        /// 从程序目录加载 HomeLayout.json；文件不存在或解析失败时返回内置默认配置。
        /// 【V1.88.17】读到的值一律按 Range 钳制后返回（手改 json 越界、上版本存的脏值，
        /// 进不了主窗：顶栏 5000 高这种不会再把工作站区挤没；纯函数 <see cref="ClampToRange"/>，
        /// 回归可直接断言）。
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
                    if (cfg != null) return cfg.ClampToRange();
                }
            }
            catch (Exception)
            {
                // 配置损坏时静默回退默认值，避免程序无法启动
            }
            return new HomeLayoutConfig();
        }

        /// <summary>
        /// 把四个尺寸钳到各自 Range 内（【V1.88.17 新增】就地钳制并返回 this，方便链式调用）。
        ///
        /// 【为什么加载也要钳】编辑器输入框有自己的 Minimum/Maximum，但 json 是手改得到的：
        /// RightPanelWidth 写 5000 → SplitterDistance 越界抛异常主窗起不来；
        /// HeaderHeight 写 5000 → 工作站区高度被挤成负数。钳制后坏文件最多变成"不好看"，
        /// 不会变成"起不来/看不见"，与"损坏回退默认"同属保命逻辑。
        /// </summary>
        public HomeLayoutConfig ClampToRange()
        {
            HeaderHeight = Clamp(HeaderHeight, HeaderRange);
            RightPanelWidth = Clamp(RightPanelWidth, RightPanelRange);
            StatusBarHeight = Clamp(StatusBarHeight, StatusBarRange);
            return this;
        }

        /// <summary>整数钳到 [Min, Max]（ClampToRange 共用，单测可直调 LoadOrDefault 越界文件验证）</summary>
        private static int Clamp(int v, (int Min, int Max) range)
        {
            return v < range.Min ? range.Min : (v > range.Max ? range.Max : v);
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

        /// <summary>配置文件路径（【V1.67】跟项目走：Projects/&lt;当前项目&gt;/HomeLayout.json）</summary>
        public static string GetConfigPath()
        {
            return ProjectProfile.ResolveDataPath("HomeLayout.json", true);
        }
    }
}
