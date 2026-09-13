using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using AgingTestSystem.Controls;
using AgingTestSystem.Services.License;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 软件授权窗（【V1.83 新增】机器码导出 + 授权导入；【V1.86】纯代码改 Designer 拆分）。
    ///
    /// 【界面布局】（单列纵向，标签+值左右排；静态边框见 LicenseForm.Designer.cs）
    /// ┌─────────────────────────────────┐
    /// │ 授权状态：[已授权至…/试用剩余…]  │ ← _lblStatus（加粗，红/绿按状态）
    /// │ 机器码：                          │
    /// │ ┌─────────────────────┐(◉)[复制]│ ← _txtMachine（只读，默认●掩码）+ _eyeIcon + _btnCopy
    /// │ [导出机器码] [导入授权文件]       │ ← _btnExport / _btnImport（导入仅管理员）
    /// │ 项目/点数/到期：[…]              │ ← _lblDetail（证里读到的，无证显示试用）
    /// │              [关闭]              │ ← _btnClose（Sunny 灰）
    /// └─────────────────────────────────┘
    ///
    /// 【眼睛显隐】机器码默认掩码（PasswordChar='●'，防路过偷窥）：
    /// 点眼睛 → 明文（PasswordChar='\0'）+ 图标翻找（掩码态=眼睛+斜线，明文态=实心瞳孔）
    /// + 悬停换"点击隐藏"；再点 → 掩码。
    /// 眼睛是 _eyeIcon（EyeIcon 自绘控件），作为 _txtMachine 的子控件内嵌其右缘内侧，
    /// 随输入框 Resize 重定位（PositionEye）。复制/导出读的都是 Text 真值，跟掩码无关
    /// ——遮的是眼睛，不是数据。
    ///
    /// 【两种打开方式】
    /// - 启动闸（Program.Main）：无有效授权阻断时弹出，此时无登录概念，
    ///   canEdit=true（能摸到工控机的人就是实施/管理员），导对了继续进主界面；
    /// - 主界面【关于→软件授权】：canEdit=管理员才可导入，操作员/技术员只读看状态。
    /// 导入成功返回 DialogResult.OK（调用方重查一次授权并刷新标题栏）。
    ///
    /// 【无参构造】只给 VS 设计器预览 + 回归 harness 用：状态未知、导入禁用，
    /// 点导入/复制/导出走正常守卫（弹提示，不抛）。执行键（真正导入）只认带参构造。
    /// </summary>
    public partial class LicenseForm : Sunny.UI.UIForm
    {
        private readonly bool _canEdit;
        private LicenseResult _result;

        // 【V1.83.1】只读模式的导入按钮提示：ToolTip 自带窗口句柄，无容器托管，
        // 局部 new 完不管会进终结器线程释放（家规：无容器的手写 Dispose，项目切换窗先例）。
        private ToolTip _tipImport;

        // 【V1.86】眼睛图标悬停说明：同一个 ToolTip，显隐切换时换文案（动态两套话随状态同步换，家规）。
        // 无容器托管，随窗体手写 Dispose（与 _tipImport 同处）。
        private ToolTip _tipEye;

        /// <summary>掩码态悬停文案（默认态：点一下看明文）。</summary>
        internal const string EyeTipMasked = "点击显示明文（默认隐藏防偷窥）";

        /// <summary>明文态悬停文案（已展开态：点一下藏回去）。</summary>
        internal const string EyeTipShown = "点击隐藏（当前明文显示）";

        /// <summary>本次是否成功导入过授权（调用方据此重查 + 刷新标题）。</summary>
        public bool Imported { get; private set; }

        /// <summary>
        /// 无参构造（VS 设计器预览 + 回归 harness 专用）：
        /// 状态未知、导入禁用；机器码照常回填（本机可算，与授权无关）。
        /// </summary>
        public LicenseForm() : this(null, false)
        {
        }

        public LicenseForm(LicenseResult result, bool canEdit)
        {
            _result = result;
            _canEdit = canEdit;
            InitializeComponent();
            FillMachineCode();
            ApplyEditState();
            RefreshView();
        }

        /// <summary>回填本机机器码（与授权状态无关；算不出置空，不拖垮构造）。</summary>
        private void FillMachineCode()
        {
            try { _txtMachine.Text = MachineFingerprint.FormatGrouped(MachineFingerprint.Compute()); }
            catch { _txtMachine.Text = ""; }
        }

        /// <summary>
        /// 按 canEdit 落导入按钮态 + 挂两个悬停提示 + 把眼睛图标精确挂到显示框上。
        /// 只读模式：按钮禁用 + 改文案点名"管理员"，悬停说明去哪提权；
        /// 眼睛图标：默认掩码态文案（与 Designer 里 PasswordChar='●' 对上）。
        /// 眼睛是 _txtMachine 的子控件（Designer 已 Add），这里补：背景跟输入框、
        /// 抢最前（防被其内层编辑框盖住）、按当前尺寸精定位、并随 Resize 跟随。
        /// 【V1.86复查】初始掩码态由代码显式收敛（PasswordChar='●'+Shown=false，
        /// Designer 只管预览）：VS 重写 Designer 丢一行即"图标说隐藏、框是明文"分叉。
        /// 状态行加粗放代码按当前字号 Bold（家规：Designer 不写死字体，防换肤分叉）。
        /// </summary>
        private void ApplyEditState()
        {
            _btnImport.Enabled = _canEdit;
            if (!_canEdit)
            {
                _btnImport.Text = "导入（管理员）";
                _tipImport = new ToolTip();
                _tipImport.SetToolTip(_btnImport, "导入授权仅管理员可用，请先切换管理员权限。");
            }
            // 状态行加粗：只加粗不动字号（按控件当前字号，V1.78 先例），不进 Designer。
            try { _lblStatus.Font = new Font(_lblStatus.Font, FontStyle.Bold); }
            catch { }
            // 初始态代码收敛：默认掩码防偷窥（Designer 的 '●' 只是预览初值）。
            _txtMachine.PasswordChar = '●';
            _eyeIcon.Shown = false;
            _tipEye = new ToolTip();
            _eyeIcon.BackColor = _txtMachine.BackColor;
            _eyeIcon.BringToFront();
            PositionEye(_txtMachine, _eyeIcon);
            // 【V1.86复查】具名订阅方便 Dispose 退订（匿名 delegate 退订不掉）；
            // BackColorChanged 跟随换肤：ThemeManager.ApplyTo 在构造之后改输入框
            // 底色，不跟的话白底眼睛贴在深色框上当场穿帮。
            _txtMachine.Resize += TxtMachine_Resize;
            _txtMachine.BackColorChanged += TxtMachine_BackColorChanged;
            _tipEye.SetToolTip(_eyeIcon, EyeTipMasked);
        }

        /// <summary>输入框尺寸变了眼睛跟右缘走（具名方法，Dispose 可退订）。</summary>
        private void TxtMachine_Resize(object sender, EventArgs e)
        {
            PositionEye(_txtMachine, _eyeIcon);
        }

        /// <summary>输入框底色变了（换肤）眼睛底同步，否则盖在文本右端露白边。</summary>
        private void TxtMachine_BackColorChanged(object sender, EventArgs e)
        {
            if (_eyeIcon == null || _eyeIcon.IsDisposed) return;
            _eyeIcon.BackColor = _txtMachine.BackColor;
            _eyeIcon.Invalidate();
        }

        /// <summary>
        /// 把眼睛图标贴到输入框右缘内侧并垂直居中。
        /// 为什么：单行文本框文本让不出右侧边距，图标只能以不透明 BackColor 盖在
        /// 文本右端；位置 = 输入框客户区右缘 − 图标宽 − 2px。
        /// 调用时机：构造后一次 + 每次输入框 Resize（输入框随窗体/DPI 缩放都得跟上）。
        /// </summary>
        private static void PositionEye(Control box, Control eye)
        {
            if (box == null || eye == null || eye.IsDisposed) return;
            int x = box.ClientSize.Width - eye.Width - 2;   // 距右缘 2px
            int y = (box.ClientSize.Height - eye.Height) / 2; // 垂直居中
            if (x < 0) x = 0; if (y < 0) y = 0;
            eye.Location = new Point(x, y);
            eye.BackColor = box.BackColor;   // 与输入框底色一致，盖在文本右端不突兀
        }

        /// <summary>
        /// 眼睛图标点击：掩码 ⇄ 明文切换（只动显示层，不动 Text 真值）。
        /// 掩码态（PasswordChar != '\0'）→ 明文（'\0'）+ 图标翻实心瞳孔；
        /// 明文态 → 掩码（'●'）+ 图标翻回"眼睛+斜线"。
        /// 悬停文案同步换，看到的是哪态、提示的就是反向动作。
        /// </summary>
        private void BtnEye_Click(object sender, EventArgs e)
        {
            try
            {
                if (_txtMachine.PasswordChar != '\0')
                {
                    _txtMachine.PasswordChar = '\0';
                    _eyeIcon.Shown = true;
                    if (_tipEye != null) _tipEye.SetToolTip(_eyeIcon, EyeTipShown);
                }
                else
                {
                    _txtMachine.PasswordChar = '●';
                    _eyeIcon.Shown = false;
                    if (_tipEye != null) _tipEye.SetToolTip(_eyeIcon, EyeTipMasked);
                }
                // Sunny UITextBox 切 PasswordChar 后内层编辑框不重绘（harness 实锤：
                // 内外值都在 79 长，但框里空白；Invalidate(true)+Update 也刷不出来）：
                // 把文本重推一次（先清空破 Sunny 的相等守卫），WM_SETTEXT 逼内层当场重画。
                // 读 Text 的复制/导出走外层缓存，不受这一进一出的影响。
                string keep = _txtMachine.Text ?? "";
                _txtMachine.Text = "";
                _txtMachine.Text = keep;
            }
            catch { /* 显隐切不动不影响授权主流程 */ }
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
                // 【V1.86复查】Message 可能为 null（构造/反序列化缺字段），旧代码直接
                // Split 即 NRE，又被外层 catch 吞掉导致状态行残留旧文案。空即"未知"。
                string head = (_result.Message ?? "").Split('\n')[0];
                if (string.IsNullOrWhiteSpace(head)) head = "未知";
                _lblStatus.Text = "状态：" + head;
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
                // 【V1.86复查】指纹算不出时 Text 为空：旧代码静默啥都不干，用户以为
                // 点坏了。空即明示，不碰剪贴板。
                if (string.IsNullOrEmpty(_txtMachine.Text))
                {
                    MessageBox.Show(this, "本机机器码为空（指纹采集失败），无法复制，请联系技术支持。",
                        "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                Clipboard.SetText(_txtMachine.Text);
                MessageBox.Show(this, "机器码已复制，请发给商务签发授权。",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                    // 【V1.86复查】空机器码不落盘：旧代码会落一个只有表头的空文件，
                    // 商务拿到空文件还以为导出来了。空即拦。
                    if (string.IsNullOrEmpty(_txtMachine.Text))
                    {
                        MessageBox.Show(this, "本机机器码为空（指纹采集失败），无法导出，请联系技术支持。",
                            "提示", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }
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

        private void BtnClose_Click(object sender, EventArgs e)
        {
            Close();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                // 输入框事件先退订再释放（R7：活得比订阅久的 handler 要成对收；
                // 这里同寿也退，保持"订阅/退订同文件"可审计）。
                try
                {
                    if (_txtMachine != null)
                    {
                        _txtMachine.Resize -= TxtMachine_Resize;
                        _txtMachine.BackColorChanged -= TxtMachine_BackColorChanged;
                    }
                }
                catch { }
                // _eyeIcon 是 _txtMachine 的子控件，随 _txtMachine.Dispose 一并释放，
                // 这里不再单独 dispose（先在父前释放是父的事）。
                try { _txtMachine.Dispose(); } catch { }
                try { _btnCopy.Dispose(); } catch { }
                try { _btnExport.Dispose(); } catch { }
                try { _btnImport.Dispose(); } catch { }
                try { _btnClose.Dispose(); } catch { }
                try { _lblStatus.Dispose(); } catch { }
                try { _lblMachine.Dispose(); } catch { }
                try { _lblDetail.Dispose(); } catch { }
                try { if (_tipImport != null) _tipImport.Dispose(); } catch { }
                try { if (_tipEye != null) _tipEye.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }
    }
}
