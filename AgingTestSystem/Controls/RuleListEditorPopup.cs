using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Services;

namespace AgingTestSystem.Controls
{
    /// <summary>
    /// 自定义报警规则编辑弹出框（供系统设置窗口 CustomAlarmRules 配置项使用）：
    /// 多行文本框，一行一条"名称 | 表达式 | 持续秒"（持续秒可省，默认 0=立即触发），
    /// 输入时实时校验（绿=合法条数，红=首个错误），点【确定】复检后提交。
    /// 点击弹出框外部或【取消】放弃修改。
    /// 界面风格与系统设置窗口一致（SunnyUI 蓝主题控件 + 白底，无标题栏）。
    ///
    /// 【为什么不用表格而用文本】规则是"表达式"这种整段文本，表格三列切分反而难写；
    /// 文本 + 实时校验，出差现场复制粘贴改一条最快。格式错了红字直接指出第几行。
    /// </summary>
    public class RuleListEditorPopup : Form
    {
        private readonly TextBox _txtRules;
        private readonly Label _lblStatus;
        private readonly Sunny.UI.UIButton _btnOk;
        private readonly Sunny.UI.UIButton _btnCancel;
        private bool _closing;

        /// <summary>
        /// 提交后的值（多行原文），未提交时为 null
        /// </summary>
        public string ResultValue { get; private set; }

        /// <param name="currentValue">当前配置值（多行，一行一条规则）</param>
        public RuleListEditorPopup(string currentValue)
        {
            FormBorderStyle = FormBorderStyle.None;
            StartPosition = FormStartPosition.Manual;
            ShowInTaskbar = false;
            BackColor = Color.White;
            ClientSize = new Size(520, 340);

            // 标题行
            var lblTitle = new Label
            {
                Text = "自定义报警规则（一行一条：名称 | 表达式 | 持续秒，可省）",
                Location = new Point(12, 8),
                Size = new Size(496, 20),
                Font = new Font("微软雅黑", 9F, FontStyle.Bold),
                ForeColor = Color.FromArgb(48, 48, 48)
            };
            Controls.Add(lblTitle);

            // 规则文本框（多行，等宽字体看表达式对齐）
            _txtRules = new TextBox
            {
                Location = new Point(12, 32),
                Size = new Size(496, 150),
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Font = new Font("Consolas", 10F),
                Text = currentValue ?? ""
            };
            _txtRules.TextChanged += (s, e) => RefreshStatus();
            Controls.Add(_txtRules);

            // 变量速查（灰字，13 个冻结变量 + 单位；V1.74 加 current=本工位电流A）
            var lblVars = new Label
            {
                Text = "变量：pressure(kPa) temp(°C) tempset(°C) hum(%RH) device delaysecs vacsecs agesecs duration threshold di0 hour current(A)" +
                    "\r\n示例：超温偏离 | temp - tempset > 10 | 30",
                Location = new Point(12, 188),
                Size = new Size(496, 36),
                Font = new Font("微软雅黑", 8.5F),
                ForeColor = Color.FromArgb(130, 138, 150)
            };
            Controls.Add(lblVars);

            // 实时校验状态（绿=合法，红=首错）
            _lblStatus = new Label
            {
                Location = new Point(12, 228),
                Size = new Size(496, 40),
                Font = new Font("微软雅黑", 9F)
            };
            Controls.Add(_lblStatus);

            _btnCancel = CreateButton("取消", new Point(316, 276), new Size(88, 30), Sunny.UI.UIStyle.Gray);
            _btnCancel.Click += (s, e) => CloseAsCancel();
            Controls.Add(_btnCancel);

            _btnOk = CreateButton("确定", new Point(412, 276), new Size(96, 30), Sunny.UI.UIStyle.Blue);
            _btnOk.Click += (s, e) => Confirm();
            Controls.Add(_btnOk);

            RefreshStatus();
        }

        private static Sunny.UI.UIButton CreateButton(string text, Point location, Size size, Sunny.UI.UIStyle style)
        {
            return new Sunny.UI.UIButton
            {
                Text = text,
                Location = location,
                Size = size,
                Style = style,
                Font = new Font("微软雅黑", 9F)
            };
        }

        /// <summary>实时校验：合法显示条数，非法显示首个错误（只取第一条，不刷屏）。</summary>
        private void RefreshStatus()
        {
            List<RuleEngine.RuleDef> rules;
            List<string> errors;
            RuleEngine.ParseRuleList(_txtRules.Text, out rules, out errors);
            if (errors.Count == 0)
            {
                _lblStatus.ForeColor = Color.FromArgb(46, 139, 87);
                _lblStatus.Text = rules.Count == 0
                    ? "✓ 零规则（保持现状，零行为变化）"
                    : $"✓ {rules.Count} 条合法";
            }
            else
            {
                _lblStatus.ForeColor = Color.FromArgb(200, 40, 40);
                _lblStatus.Text = "✗ " + errors[0]
                    + (errors.Count > 1 ? $"（另有 {errors.Count - 1} 处）" : "");
            }
        }

        /// <summary>确定：复检通过才提交；有错弹框留人。</summary>
        private void Confirm()
        {
            List<RuleEngine.RuleDef> rules;
            List<string> errors;
            RuleEngine.ParseRuleList(_txtRules.Text, out rules, out errors);
            if (errors.Count > 0)
            {
                MessageBox.Show(this, "规则有误，请先修正：\r\n\r\n" + string.Join("\r\n", errors.ToArray()),
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            ResultValue = _txtRules.Text ?? "";
            _closing = true;
            Close();
        }

        private void CloseAsCancel()
        {
            ResultValue = null;
            _closing = true;
            Close();
        }

        /// <summary>无边框窗体，用浅灰描边勾出弹窗边界，与表格底框视觉统一</summary>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            using (var pen = new Pen(Color.FromArgb(214, 220, 228)))
            {
                Rectangle rect = ClientRectangle;
                rect.Width -= 1;
                rect.Height -= 1;
                e.Graphics.DrawRectangle(pen, rect);
            }
        }

        /// <summary>点击弹出框外部（窗体失焦）视为取消</summary>
        protected override void OnDeactivate(EventArgs e)
        {
            base.OnDeactivate(e);
            if (!_closing)
            {
                ResultValue = null;
                _closing = true;
                Close();
            }
        }
    }
}
