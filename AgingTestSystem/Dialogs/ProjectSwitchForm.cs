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
    /// │ 当前项目：Default                 │
    /// │ ┌──────────────────────────────┐ │
    /// │ │ 项目列表（ListBox，★=当前）   │ │
    /// │ └──────────────────────────────┘ │
    /// │ 新建：[________] [创建]          │
    /// │ [切换并重启] [关闭]              │
    /// │ 注：测试中有在测工位时禁切       │
    /// └──────────────────────────────────┘
    ///
    /// 【规则】
    /// - 新建=以当前项目为模板复制（配方/策略/布局全带过去，回来改差异项即可）；
    /// - 切换=改机器指针 ActiveProject，【必须重启】（运行时路径启动时解析）；
    /// - 有工位在测（Testing/Vacuuming/Aging）时禁切：切项目=换配方换策略，
    ///   跑中的任务会读劈叉，等停机/完成再切。
    /// - 用户账号是全局的，不跟项目走（见 ProjectProfile 注释）。
    /// </summary>
    public class ProjectSwitchForm : Form
    {
        private readonly DeviceManagerRef _deviceManager;

        private Label _lblCurrent;
        private ListBox _lstProjects;
        private TextBox _txtNewName;
        private Button _btnCreate;
        private Button _btnSwitch;
        private Button _btnClose;

        /// <summary>
        /// 对 DeviceManager 的最小引用（只为查"是否在测"；用接口隔离防窗体碰业务）。
        /// MainForm 传 DeviceManager 适配器即可——这里直接用 Func<int>（在测台数）。
        /// </summary>
        public interface DeviceManagerRef
        {
            int TestingCount { get; }
        }

        /// <param name="testingCount">在测工位数（主窗体传入 () => _deviceManager.GetTestingDeviceIds().Length）</param>
        public ProjectSwitchForm(Func<int> testingCountProvider)
        {
            _deviceManager = new CountAdapter(testingCountProvider);

            // 【高 DPI 三要素】纯代码窗体：基准尺寸 + 挂起布局，末尾 ResumeLayout
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.SuspendLayout();

            this.Text = "项目切换（需重启生效）";
            this.StartPosition = FormStartPosition.CenterParent;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(400, 380);

            int y = 12;
            _lblCurrent = new Label
            {
                Location = new Point(12, y),
                Size = new Size(376, 24),
                Font = new Font(this.Font, FontStyle.Bold)
            };
            this.Controls.Add(_lblCurrent);
            y += 30;

            _lstProjects = new ListBox { Location = new Point(12, y), Size = new Size(376, 180) };
            _lstProjects.DoubleClick += (s, e) => BtnSwitch_Click(s, e);
            this.Controls.Add(_lstProjects);
            y += 190;

            var lblNew = new Label { Location = new Point(12, y + 4), Size = new Size(48, 20), Text = "新建：" };
            _txtNewName = new TextBox { Location = new Point(64, y), Size = new Size(220, 24) };
            _btnCreate = new Button { Location = new Point(292, y - 1), Size = new Size(96, 26), Text = "创建" };
            _btnCreate.Click += BtnCreate_Click;
            this.Controls.Add(lblNew);
            this.Controls.Add(_txtNewName);
            this.Controls.Add(_btnCreate);
            y += 34;

            _btnSwitch = new Button { Location = new Point(12, y), Size = new Size(188, 30), Text = "切换并重启" };
            _btnSwitch.Click += BtnSwitch_Click;
            _btnClose = new Button { Location = new Point(208, y), Size = new Size(180, 30), Text = "关闭", DialogResult = DialogResult.Cancel };
            this.Controls.Add(_btnSwitch);
            this.Controls.Add(_btnClose);
            y += 38;

            var lblNote = new Label
            {
                Location = new Point(12, y),
                Size = new Size(376, 40),
                ForeColor = Color.Gray,
                Text = "注：切换后自动重启生效；有工位在测时禁止切换。\r\n用户账号全局共享，不跟项目走。"
            };
            this.Controls.Add(lblNote);

            this.CancelButton = _btnClose;
            RefreshList();
            this.ResumeLayout(false);
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
            MessageBox.Show($"项目 [{name}] 已创建（以当前项目为模板复制）。\n选中它再点\"切换并重启\"即生效。",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            _txtNewName.Text = "";
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
            DialogResult r = MessageBox.Show($"已切换到项目 [{name}]，现在重启生效？",
                "项目切换", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r == DialogResult.Yes)
            {
                Application.Restart();
            }
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
