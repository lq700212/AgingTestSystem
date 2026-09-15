using System;
using System.IO;
using AgingTestSystem.Services;
using Newtonsoft.Json;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// 主页布局配置：主窗三大区域尺寸收敛到此（顶栏高锁 30／右侧宽／状态栏高），
    /// 现场在"关于 → 主页区域调整"里拖边缘即改，存 HomeLayout.json，不用重编译。
    /// 布局（与 MainForm.Designer 的 tableLayoutPanelMain 对应）：
    /// ┌───────────────────────────────────────┐
    /// │ 顶栏 30：项目/权限/通讯＋4 按钮＋窗口三键 │
    /// ├──────────────────────────┬────────────┤
    /// │                          │ 右侧状态按钮区│
    /// │  工作站列表（占满剩余宽度， │（无配置跟窗口│
    /// │  无需配置）                 │ 23%，有配用文件）│
    /// ├──────────────────────────┴────────────┤
    /// │ 状态栏 StatusBarHeight（默认 30）       │
    /// └───────────────────────────────────────┘
    /// 右侧含运行状态/监视/操作/日志四组；操作按钮随分组宽自适应缩放。
    /// 加载：有 HomeLayout.json 读它（坏了回默认），没有用内置默认；单位逻辑像素（96DPI）。
    /// </summary>
    public class HomeLayoutConfig
    {
        /// <summary>
        /// 顶栏高度固定值（顶栏不许自定义，永远 30；
        /// 主窗 <c>ApplyHomeLayout</c>、本类加载/保存/钳制、预览画布全认这一个数，
        /// 不读文件里的旧值——文件里残留的 HeaderHeight 只是历史兼容壳）。
        /// </summary>
        public const int FixedHeaderHeight = 30;

        /// <summary>
        /// 顶栏高度（历史兼容壳：老 HomeLayout.json 里可能存着 28~100 的旧值，
        /// 反序列化不能删字段否则未知键倒无妨、缺字段回缺省；
        /// 实际生效永远是 <see cref="FixedHeaderHeight"/>，
        /// 加载/保存/钳制三处统一归位，编辑器也不再暴露它）。
        /// </summary>
        public int HeaderHeight { get; set; } = 30;

        /// <summary>右侧状态按钮区宽度（运行状态+监视+操作+日志 四块的总宽）。
        /// 类默认值 240 只作编辑器"恢复默认"的基准；主窗体无 json 时实际按
        /// 窗口 23.4% 比例自适应（见 MainForm.ComputeRightPanelWidth），有 json 时以文件值为准。</summary>
        public int RightPanelWidth { get; set; } = 240;

        /// <summary>底部状态栏高度（设备数量/采集间隔/当前时间）</summary>
        public int StatusBarHeight { get; set; } = 30;

        // ===================== 调整范围约束 =====================
        // 可视化编辑器拖动矩形块边缘时用这些上下限做钳制，
        // 防止把某个区域拖成 0 或超出合理范围导致主界面错乱。
        // 与 HomeLayoutEditorForm 中的范围常量保持同步。
        // 顶栏锁死后不再需要 HeaderRange（已删）：顶栏只有 FixedHeaderHeight 一个值。

        /// <summary>右侧状态按钮区宽度最小/最大值</summary>
        [JsonIgnore]
        public static readonly (int Min, int Max) RightPanelRange = (180, 600);

        /// <summary>底部状态栏高度最小/最大值</summary>
        [JsonIgnore]
        public static readonly (int Min, int Max) StatusBarRange = (15, 60);

        // ===================== 加载与保存 =====================

        /// <summary>
        /// 从程序目录加载 HomeLayout.json；文件不存在或解析失败时返回内置默认配置。
        /// 读到的值一律按 Range 钳制后返回（手改 json 越界、上版本存的脏值，
        /// 进不了主窗：顶栏 5000 高这种不会再把工作站区挤没；纯函数 <see cref="ClampToRange"/>，
        /// 回归可直接断言）。
        /// 顶栏锁死：读出后 HeaderHeight 一律归 <see cref="FixedHeaderHeight"/>，
        /// 老文件存的 34/44 等旧值直接作废（用户点名固定，不迁移不提醒）。
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
        /// 把四个尺寸钳到各自 Range 内（就地钳制并返回 this，方便链式调用）。
        /// 【为什么加载也要钳】编辑器输入框有自己的 Minimum/Maximum，但 json 是手改得到的：
        /// RightPanelWidth 写 5000 → SplitterDistance 越界抛异常主窗起不来；
        /// HeaderHeight 写 5000 → 工作站区高度被挤成负数。钳制后坏文件最多变成"不好看"，
        /// 不会变成"起不来/看不见"，与"损坏回退默认"同属保命逻辑。
        /// 顶栏不再按范围钳：一律归 <see cref="FixedHeaderHeight"/>（锁死，不读旧值）。
        /// </summary>
        public HomeLayoutConfig ClampToRange()
        {
            HeaderHeight = FixedHeaderHeight;
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
        /// 落盘前顶栏先归位 <see cref="FixedHeaderHeight"/>：文件里永远是 30，
        /// 老版本存的旧值下次保存即洗掉（用户点名固定，不迁移不提醒）。
        /// </summary>
        public void Save()
        {
            try
            {
                HeaderHeight = FixedHeaderHeight;
                string path = GetConfigPath();
                string json = JsonConvert.SerializeObject(this, Formatting.Indented);
                File.WriteAllText(path, json, new System.Text.UTF8Encoding(false));
            }
            catch (Exception)
            {
                // 写入失败静默忽略
            }
        }

        /// <summary>配置文件路径（跟项目走：Projects/&lt;当前项目&gt;/HomeLayout.json）</summary>
        public static string GetConfigPath()
        {
            return ProjectProfile.ResolveDataPath("HomeLayout.json", true);
        }
    }
}
