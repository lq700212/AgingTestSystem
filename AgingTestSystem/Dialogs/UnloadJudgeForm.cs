using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AgingTestSystem.Models;
using AgingTestSystem.Services;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 下料判定窗体（【V1.67 新增】Q22 PendingReview 配套）。
    ///
    /// 【界面布局】（【V1.71】UIForm 自绘蓝标题，内容整体下移 35px）
    /// ┌──────────────────────────────────┐
    /// │ 下料判定（N 台送判）              │
    /// │ 完成态 M 台可判，K 台跳过（灰字）  │
    /// │ ○ PASS  ○ FAIL                   │
    /// │ 不良代码：[________]（FAIL 必填） │
    /// │ 处置：[重测 ▼]（FAIL 必选）       │
    /// │ [执行判定] [关闭]                 │
    /// │ 结果：已判定 M 台，跳过 K 台       │
    /// └──────────────────────────────────┘
    ///
    /// 【流程】执行 → DeviceManager.RecordUnloadJudge（只收 Completed 台 →
    /// 逐台写"下料判定"CSV 事件 → 回空闲）。判定结果以 CSV/历史查询为准，
    /// 面板回空闲后不再保留（既有追溯链，见 RecordUnloadJudge 注释）。
    /// 【权限】操作员可操作（下料是操作员的活）；AutoPass 模式主窗体根本不让进。
    /// </summary>
    public class UnloadJudgeForm : Sunny.UI.UIForm
    {
        /// <summary>FAIL 处置选项（重测=回到待测，报废/降级/让步=出厂口径，由质量定）</summary>
        public static readonly string[] Dispositions = new string[]
        {
            "重测", "报废", "降级", "让步放行"
        };

        private readonly int[] _deviceIds;
        private readonly DeviceManager _deviceManager;

        private Sunny.UI.UILabel _lblScope;
        private RadioButton _rbPass;
        private RadioButton _rbFail;
        private Sunny.UI.UITextBox _txtDefectCode;
        private Sunny.UI.UIComboBox _cmbDisposition;
        private Sunny.UI.UIButton _btnExecute;
        private Sunny.UI.UIButton _btnClose;
        private Sunny.UI.UILabel _lblResult;

        /// <param name="deviceIds">选中的工位号（主窗体已判空）</param>
        /// <param name="deviceManager">设备管理器（执行判定 + 读完成态）</param>
        public UnloadJudgeForm(int[] deviceIds, DeviceManager deviceManager)
        {
            _deviceIds = deviceIds ?? new int[0];
            _deviceManager = deviceManager;

            // 【高 DPI 三要素】纯代码窗体：基准尺寸 + 挂起布局，末尾 ResumeLayout
            this.AutoScaleDimensions = new SizeF(6F, 12F);
            this.AutoScaleMode = AutoScaleMode.Font;
            this.SuspendLayout();

            this.Text = "下料判定";
            this.StartPosition = FormStartPosition.CenterParent;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.ClientSize = new Size(440, 335);
            // 【V1.71】绝对布局：禁缩小（MinimumSize=ClientSize），防缩坏布局；可放大。
            this.MinimumSize = new Size(440, 335);

            int y = 47;   // 【V1.71】UIForm 标题区 35px，内容下移
            _lblScope = new Sunny.UI.UILabel
            {
                Location = new Point(12, y),
                Size = new Size(416, 36),
                Text = BuildScopeText()
            };
            this.Controls.Add(_lblScope);
            y += 44;

            _rbPass = new RadioButton { Location = new Point(12, y), Size = new Size(100, 24), Text = "PASS", Checked = true };
            _rbFail = new RadioButton { Location = new Point(120, y), Size = new Size(100, 24), Text = "FAIL" };
            this.Controls.Add(_rbPass);
            this.Controls.Add(_rbFail);
            y += 32;

            var lblCode = new Sunny.UI.UILabel { Location = new Point(12, y + 4), Size = new Size(80, 20), Text = "不良代码：" };
            _txtDefectCode = new Sunny.UI.UITextBox { Location = new Point(96, y), Size = new Size(332, 24) };
            this.Controls.Add(lblCode);
            this.Controls.Add(_txtDefectCode);
            y += 32;

            var lblDisp = new Sunny.UI.UILabel { Location = new Point(12, y + 4), Size = new Size(80, 20), Text = "处置：" };
            _cmbDisposition = new Sunny.UI.UIComboBox
            {
                Location = new Point(96, y),
                Size = new Size(332, 24),
                DropDownStyle = Sunny.UI.UIDropDownStyle.DropDownList
            };
            _cmbDisposition.Items.AddRange(Dispositions);
            this.Controls.Add(lblDisp);
            this.Controls.Add(_cmbDisposition);
            y += 40;

            _btnExecute = new Sunny.UI.UIButton { Location = new Point(12, y), Size = new Size(200, 30), Text = "执行判定" };
            _btnExecute.Click += BtnExecute_Click;
            _btnClose = new Sunny.UI.UIButton
            {
                Location = new Point(228, y),
                Size = new Size(200, 30),
                Text = "关闭",
                DialogResult = DialogResult.Cancel,
                FillColor = Color.DimGray,
                RectColor = Color.DimGray,
                ForeColor = Color.White,
                Style = Sunny.UI.UIStyle.Custom
            };
            this.Controls.Add(_btnExecute);
            this.Controls.Add(_btnClose);
            y += 40;

            _lblResult = new Sunny.UI.UILabel { Location = new Point(12, y), Size = new Size(416, 40), ForeColor = Color.Blue };
            this.Controls.Add(_lblResult);

            this.CancelButton = _btnClose;
            this.ResumeLayout(false);
        }

        /// <summary>统计送判范围文案（完成态可判，其余跳过；打开时快照，仅展示用）</summary>
        private string BuildScopeText()
        {
            int ok = 0;
            foreach (int id in _deviceIds)
            {
                try
                {
                    BarometerData data = _deviceManager != null ? _deviceManager.GetBarometerData(id) : null;
                    if (data != null && data.Status == DeviceStatus.Completed) ok++;
                }
                catch { /* 单台读取失败按跳过算 */ }
            }
            return $"送判 {_deviceIds.Length} 台：完成态 {ok} 台可判，" +
                $"{_deviceIds.Length - ok} 台非完成态将跳过。\n" +
                "（FAIL 必须填不良代码并选处置；PASS 可直接执行）";
        }

        /// <summary>
        /// 执行判定：FAIL 必填不良代码+处置（防"判了等于没判"）；调 manager 落盘+复位。
        /// </summary>
        private void BtnExecute_Click(object sender, EventArgs e)
        {
            bool pass = _rbPass.Checked;
            string code = (_txtDefectCode.Text ?? "").Trim();
            string disp = pass ? "—" : (_cmbDisposition.SelectedItem as string ?? "");
            if (!pass && string.IsNullOrEmpty(code))
            {
                MessageBox.Show("FAIL 必须填写不良代码，否则追溯断链。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            if (!pass && string.IsNullOrEmpty(disp))
            {
                MessageBox.Show("FAIL 必须选择处置（重测/报废/降级/让步放行）。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int[] judged;
            int[] skipped;
            _deviceManager.RecordUnloadJudge(_deviceIds, pass, code, disp, out judged, out skipped);
            _lblResult.Text = $"已判定 {judged.Length} 台" +
                (judged.Length > 0 ? $"（{string.Join("、", judged.Take(20).Select(i => i.ToString()).ToArray())}" +
                (judged.Length > 20 ? "…" : "") + "）" : "") +
                (skipped.Length > 0 ? $"；跳过 {skipped.Length} 台（非完成态）" : "") +
                "。明细见历史查询 CSV。";
            _lblScope.Text = BuildScopeText();   // 刷新：判完的台已回空闲
        }
    }
}
