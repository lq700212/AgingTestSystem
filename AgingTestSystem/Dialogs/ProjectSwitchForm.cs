using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Services;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 项目切换窗体（【V1.67 新增】一期多项目切换入口，仅管理员）。
    ///
    /// 【界面布局】
    /// ┌──────────────────────────────────┐
    /// │ 当前项目：烧屏测试                │
    /// │ ┌──────────────────────────────┐ │
    /// │ │ 项目列表（ListBox，★=当前）   │ │
    /// │ └──────────────────────────────┘ │
    /// │ 新建：[________] [创建]          │
    /// │ [切换并生效] [删除项目] [关闭]   │
    /// │ 注：测试中有在测工位时禁切       │
    /// └──────────────────────────────────┘
    ///
    /// 【规则】
    /// - 新建=以当前项目为模板复制（配方/策略/布局全带过去，回来改差异项即可）；
    /// - 切换=改机器指针 ActiveProject，主窗体随后热加载（配方/工位设置/策略/
    ///   布局即时换装，【V1.72.10】无需重启；成功后 SwitchedProjectName 带回项目名）；
    /// - 删除=删非当前项目整个目录（【V1.72.11】建错/验证完的清理口；当前项目
    ///   禁删，先切走再删；二次确认防手滑）；
    /// - 有工位在测（Testing/Vacuuming/Aging）时禁切：切项目=换配方换策略，
    ///   跑中的任务会读劈叉，等停机/完成再切。
    /// - 用户账号是全局的，不跟项目走（见 ProjectProfile 注释）。
    /// </summary>
    public partial class ProjectSwitchForm : Sunny.UI.UIForm
    {
        private readonly DeviceManagerRef _deviceManager;

        /// <summary>
        /// 对 DeviceManager 的最小引用（只为查"是否在测"；用接口隔离防窗体碰业务）。
        /// MainForm 传 DeviceManager 适配器即可——这里直接用 Func<int>（在测台数）。
        /// </summary>
        public interface DeviceManagerRef
        {
            int TestingCount { get; }
        }

        /// <summary>
        /// 本次成功切换到的项目名（【V1.72.10】主窗体凭此做热加载；null=没切换，
        /// 主窗体什么都不做。原来这里是"切换并重启"，现在即时生效不重启了）。
        /// </summary>
        public string SwitchedProjectName { get; private set; }

        /// <param name="testingCount">在测工位数（主窗体传入 () => _deviceManager.GetTestingDeviceIds().Length）</param>
        public ProjectSwitchForm(Func<int> testingCountProvider)
        {
            _deviceManager = new CountAdapter(testingCountProvider);

            // 【V1.72.12 Designer 化】静态边框搬进 ProjectSwitchForm.Designer.cs，
            // 这里只初填"要读服务"的那一项（项目列表依赖 ProjectProfile）。
            InitializeComponent();
            RefreshList();
        }

        /// <summary>Func 适配器（MainForm 传 () => 在测台数 即可，不用把 DeviceManager 整个交进来）。</summary>
        private class CountAdapter : DeviceManagerRef
        {
            private readonly Func<int> _provider;
            public CountAdapter(Func<int> provider) { _provider = provider; }
            public int TestingCount { get { return _provider != null ? _provider() : 0; } }
        }

        /// <summary>刷新列表（★标当前项目）。</summary>
        private void RefreshList()
        {
            string current = ProjectProfile.ActiveProfileName;
            _lblCurrent.Text = "当前项目：" + current;
            _lstProjects.Items.Clear();
            List<string> names = ProjectProfile.ListProfiles();
            if (names.Count == 0) names.Add(current);
            foreach (string n in names)
            {
                _lstProjects.Items.Add(string.Equals(n, current, StringComparison.OrdinalIgnoreCase)
                    ? "★ " + n : "    " + n);
                if (string.Equals(n, current, StringComparison.OrdinalIgnoreCase))
                {
                    _lstProjects.SelectedIndex = _lstProjects.Items.Count - 1;
                }
            }
        }

        /// <summary>从列表行文本还原项目名（去 ★/空格前缀）。</summary>
        private static string StripMarker(string item)
        {
            if (string.IsNullOrEmpty(item)) return "";
            return item.Replace("★", "").Trim();
        }

        private void BtnCreate_Click(object sender, EventArgs e)
        {
            string name = (_txtNewName.Text ?? "").Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请先输入新项目名（字母/数字/中文/下划线/连横线/空格）。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!ProjectProfile.CreateProfile(name))
            {
                MessageBox.Show("创建失败：名字非法或项目已存在。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            MessageBox.Show($"项目 [{name}] 已创建（以当前项目为模板复制）。\n选中它再点\"切换并生效\"即切换。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtNewName.Text = "";
            RefreshList();
        }

        /// <summary>
        /// 删除项目（【V1.72.11】建错名/验证完的清理口）。
        /// 只删"选中的非当前项目"：当前项目禁删（先切走再删，防内存与文件对不上）；
        /// 删的是别的项目目录，不碰当前内存与采集，所以在测也可删（与"禁切"不同，
        /// 这里不查 TestingCount，原因写在 DeleteProfile 注释里）。
        /// 二次确认框里带项目名防手滑；删完刷新列表即可，无需热加载（顶栏/数据不动）。
        /// </summary>
        private void BtnDelete_Click(object sender, EventArgs e)
        {
            if (_lstProjects.SelectedItem == null)
            {
                MessageBox.Show("请先在列表中选中要删除的项目。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string name = StripMarker(_lstProjects.SelectedItem.ToString());
            if (string.Equals(name, ProjectProfile.ActiveProfileName, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show($"项目 [{name}] 是当前项目，不能删除。\n" +
                    "请先切换到别的项目，再回来删它。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult r = MessageBox.Show($"确定要删除项目 [{name}] 吗？\n" +
                "该项目下的配方/工位设置/主页布局/策略将全部删除，且不可恢复。",
                "确认删除", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
            if (r != DialogResult.OK) return;
            if (!ProjectProfile.DeleteProfile(name))
            {
                MessageBox.Show("删除失败（项目不存在或文件被占用）。", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            RefreshList();
        }

        private void BtnSwitch_Click(object sender, EventArgs e)
        {
            if (_lstProjects.SelectedItem == null)
            {
                MessageBox.Show("请先在列表中选中要切换的项目。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            string name = StripMarker(_lstProjects.SelectedItem.ToString());
            if (string.Equals(name, ProjectProfile.ActiveProfileName, StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("已经是当前项目，无需切换。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            // 在测禁切：切项目=换配方换策略，跑中任务会读劈叉
            if (_deviceManager.TestingCount > 0)
            {
                MessageBox.Show($"当前有 {_deviceManager.TestingCount} 台在测，禁止切换项目。\n" +
                    "请等待完成/停止并复位后，再切换。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!ProjectProfile.SwitchTo(name))
            {
                MessageBox.Show("切换失败（项目不存在或写入配置失败）。", "错误",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            // 【V1.72.10 热更】指针已改即返回，主窗体凭 SwitchedProjectName 热加载，
            // 不再弹窗问重启（原来 Application.Restart，已删）。
            SwitchedProjectName = name;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
