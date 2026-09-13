using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using AgingTestSystem.Services.License;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 软件授权窗（【V1.83 新增】机器码导出 + 授权导入，纯代码窗体无 Designer）。
    ///
    /// 【界面布局】（单列纵向，标签+值左右排）
    /// ┌─────────────────────────────────┐
    /// │ 授权状态：[已授权至…/试用剩余…]  │ ← lblStatus（加粗，红/绿按状态）
    /// │ 机器码：                          │
    /// │ ┌───────────────────┐ [复制]    │ ← txtMachine（只读）+ btnCopy
    /// │ [导出机器码] [导入授权文件]       │ ← btnExport / btnImport（导入仅管理员）
    /// │ 项目/点数/到期：[…]              │ ← lblDetail（证里读到的，无证显示试用）
    /// │              [关闭]              │ ← btnClose（Sunny 灰）
    /// └─────────────────────────────────┘
    ///
    /// 【两种打开方式】
    /// - 启动闸（Program.Main）：无有效授权阻断时弹出，此时无登录概念，
    ///   canEdit=true（能摸到工控机的人就是实施/管理员），导对了继续进主界面；
    /// - 主界面【关于→软件授权】：canEdit=管理员才可导入，操作员/技术员只读看状态。
    /// 导入成功返回 DialogResult.OK（调用方重查一次授权并刷新标题栏）。
    ///
    /// 【纯代码窗高 DPI 三要素】（家规：无 Designer 的窗体三件套）
    /// ①AutoScaleDimensions=6F,12F + AutoScaleMode.Font；
    /// ②SuspendLayout 包裹全部创建、末尾 ResumeLayout(false)；
    /// ③不写死字体字号（跟随继承，防大小眼）。
    /// </summary>
    public class LicenseForm : Sunny.UI.UIForm
    {
        private readonly bool _canEdit;
        private LicenseResult _result;

        private Sunny.UI.UILabel _lblStatus;
        private Sunny.UI.UITextBox _txtMachine;
        private Sunny.UI.UIButton _btnCopy;
        private Sunny.UI.UIButton _btnExport;
        private Sunny.UI.UIButton _btnImport;
        private Sunny.UI.UILabel _lblDetail;
        private Sunny.UI.UIButton _btnClose;
        // 【V1.83.1】只读模式的导入按钮提示：ToolTip 自带窗口句柄，无容器托管，
        // 局部 new 完不管会进终结器线程释放（家规：无容器的手写 Dispose，项目切换窗先例）。
        private ToolTip _tipImport;

        /// <summary>本次是否成功导入过授权（调用方据此重查 + 刷新标题）。</summary>
        public bool Imported { get; private set; }

        public LicenseForm(LicenseResult result, bool canEdit)
        {
            _result = result;
            _canEdit = canEdit;
            SuspendLayout();
            AutoScaleDimensions = new SizeF(6F, 12F);
            AutoScaleMode = AutoScaleMode.Font;
            Text = "软件授权";
            StartPosition = FormStartPosition.CenterParent;
            Size = new Size(560, 360);
            MinimumSize = new Size(480, 320);
            // UIForm 自绘标题占 35px：内容整体下移（家规，见 AGENTS 换肤约定）。
            Padding = new Padding(10, 45, 10, 10);

            _lblStatus = new Sunny.UI.UILabel
            {
                Location = new Point(16, 50),
                Size = new Size(510, 30),
                Font = new Font(Font.FontFamily, Font.Size, FontStyle.Bold),
            };
            Controls.Add(_lblStatus);

            var lblM = new Sunny.UI.UILabel
            {
                Location = new Point(16, 88),
                Size = new Size(510, 22),
                Text = "本机机器码（发给商务签发授权，一机一证）：",
            };
            Controls.Add(lblM);

            _txtMachine = new Sunny.UI.UITextBox
            {
                Location = new Point(16, 112),
                Size = new Size(400, 30),
                ReadOnly = true,
            };
            try { _txtMachine.Text = MachineFingerprint.FormatGrouped(MachineFingerprint.Compute()); }
            catch { _txtMachine.Text = ""; }
            Controls.Add(_txtMachine);

            _btnCopy = new Sunny.UI.UIButton
            {
                Location = new Point(424, 112),
                Size = new Size(100, 30),
                Text = "复制",
            };
            _btnCopy.Click += BtnCopy_Click;
            Controls.Add(_btnCopy);

            _btnExport = new Sunny.UI.UIButton
            {
                Location = new Point(16, 152),
                Size = new Size(150, 32),
                Text = "导出机器码",
            };
            _btnExport.Click += BtnExport_Click;
            Controls.Add(_btnExport);

            _btnImport = new Sunny.UI.UIButton
            {
                Location = new Point(176, 152),
                Size = new Size(150, 32),
                Text = "导入授权文件",
                Enabled = _canEdit,
            };
            if (!_canEdit)
            {
                _btnImport.Text = "导入（管理员）";
                _tipImport = new ToolTip();
                _tipImport.SetToolTip(_btnImport, "导入授权仅管理员可用，请先切换管理员权限。");
            }
            _btnImport.Click += BtnImport_Click;
            Controls.Add(_btnImport);

            _lblDetail = new Sunny.UI.UILabel
            {
                Location = new Point(16, 194),
                Size = new Size(510, 70),
                ForeColor = Color.Gray,
            };
            Controls.Add(_lblDetail);

            _btnClose = new Sunny.UI.UIButton
            {
                Location = new Point(374, 274),
                Size = new Size(150, 32),
                Text = "关闭",
                FillColor = Color.DimGray,
                RectColor = Color.DimGray,
                ForeColor = Color.White,
                Style = Sunny.UI.UIStyle.Custom,
            };
            _btnClose.Click += (s, e) => Close();
            Controls.Add(_btnClose);

            RefreshView();
            ResumeLayout(false);
        }

        /// <summary>刷新状态显示（导入成功后重查一次再调它）。</summary>
        public void RefreshView(LicenseResult fresh = null)
        {
            if (fresh != null) _result = fresh;
            try
            {
                if (_result == null)
                {
                    _lblStatus.Text = "状态：未知";
                    _lblDetail.Text = "";
                    return;
                }
                _lblStatus.Text = "状态：" + _result.Message.Split('\n')[0];
                _lblStatus.ForeColor = _result.Allowed ? Color.Green : Color.Red;
                var sb = new StringBuilder();
                if (_result.Info != null)
                {
                    sb.Append("项目：").Append(string.IsNullOrEmpty(_result.Info.project)
                        ? "通用版" : _result.Info.project).Append("  ");
                    sb.Append("点数：").Append(_result.Info.maxStations).Append("  ");
                    sb.Append("到期：").Append(_result.Info.expiry).Append("  ");
                    sb.Append("编号：").Append(_result.Info.serial);
                }
                else
                {
                    sb.Append("无授权文件（License.lic），当前走试用。正式使用请导入授权。");
                }
                if (!string.IsNullOrEmpty(_result.FeatureWarning))
                    sb.Append("\n注意：").Append(_result.FeatureWarning);
                _lblDetail.Text = sb.ToString();
            }
            catch { }
        }

        private void BtnCopy_Click(object sender, EventArgs e)
        {
            try
            {
                if (!string.IsNullOrEmpty(_txtMachine.Text))
                {
                    Clipboard.SetText(_txtMachine.Text);
                    MessageBox.Show(this, "机器码已复制，请发给商务签发授权。",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "复制失败：" + ex.Message,
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnExport_Click(object sender, EventArgs e)
        {
            try
            {
                using (var dlg = new SaveFileDialog())
                {
                    dlg.Filter = "文本文件|*.txt";
                    dlg.FileName = "机器码_" + Environment.MachineName + ".txt";
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    // 明文导出：机器码本来就是要发给商务的公开信息，不加密；
                    // 包含分组与去分组两行，签发端拷任意一行都能用（Ungroup 归一）。
                    string grouped = _txtMachine.Text;
                    string plain = MachineFingerprint.Ungroup(grouped);
                    File.WriteAllText(dlg.FileName,
                        "机器码（发给商务签发授权）：" + grouped + "\r\n" +
                        "去分组：" + plain + "\r\n" +
                        "计算机名：" + Environment.MachineName + "\r\n" +
                        "导出时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "\r\n",
                        Encoding.UTF8);
                    MessageBox.Show(this, "已导出：\n" + dlg.FileName,
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导出失败：" + ex.Message,
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void BtnImport_Click(object sender, EventArgs e)
        {
            if (!_canEdit)
            {
                MessageBox.Show(this, "导入授权仅管理员可用，请先在【用户权限】中切换为管理员。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            try
            {
                using (var dlg = new OpenFileDialog())
                {
                    dlg.Filter = "授权文件|*.lic|全部文件|*.*";
                    if (dlg.ShowDialog(this) != DialogResult.OK) return;
                    string text = File.ReadAllText(dlg.FileName, Encoding.UTF8);
                    LicenseInfo info;
                    string sig;
                    if (!LicenseInfo.TryParseFile(text, out info, out sig)
                        || !LicenseManager.VerifySignature(info, sig))
                    {
                        MessageBox.Show(this, "该文件不是有效的授权文件（解析失败或签名无效）。",
                            "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    // 分量段原样透传：KeyGen 签发的证没有分量段 → 传 null，
                    // LicenseManager 按本机分量补（本机导入本机证，漂移容忍才有意义）；
                    // 外部拷来的证若自带分量段 → 保留原值（拷到别台机分量对不上，防漂移放水）。
                    List<string> comps = null;
                    try
                    {
                        var root = Newtonsoft.Json.Linq.JObject.Parse(text);
                        var arr = root["components"] as Newtonsoft.Json.Linq.JArray;
                        if (arr != null)
                        {
                            comps = new List<string>();
                            foreach (var t in arr) comps.Add((string)t ?? "");
                        }
                    }
                    catch { comps = null; }
                    if (!LicenseManager.SaveLicenseFile(info, sig, comps))
                    {
                        MessageBox.Show(this, "导入失败：授权文件写不进程序目录（检查写权限）。",
                            "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
                    Imported = true;
                    MessageBox.Show(this, "导入成功（已重查，以最新状态为准）。",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    DialogResult = DialogResult.OK;
                    Close();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "导入失败：" + ex.Message,
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _txtMachine.Dispose(); } catch { }
                try { _btnCopy.Dispose(); } catch { }
                try { _btnExport.Dispose(); } catch { }
                try { _btnImport.Dispose(); } catch { }
                try { _btnClose.Dispose(); } catch { }
                try { _lblStatus.Dispose(); } catch { }
                try { _lblDetail.Dispose(); } catch { }
                try { if (_tipImport != null) _tipImport.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
