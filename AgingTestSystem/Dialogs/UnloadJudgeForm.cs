using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using AgingTestSystem.Models;
using AgingTestSystem.Services;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 下料判定窗体（Q22 PendingReview 配套）。
    /// 【界面布局】（UIForm 自绘蓝标题，内容整体下移 35px；标签具名可预览）
    /// ┌──────────────────────────────────┐
    /// │ 下料判定（UIForm 蓝标题）          │
    /// │ _lblScope：送判 N 台可判 M 跳过 K  │ ← 灰字两行，构造回填
    /// │ _rbPass ○ PASS  _rbFail ○ FAIL    │ ← PASS 默认选中
    /// │ _lblCode 不良代码：[_txtDefectCode]│ ← FAIL 必填
    /// │ _lblDisp 处置：[_cmbDisposition ▼] │ ← FAIL 必选=Dispositions
    /// │ [_btnExecute 执行判定] [_btnClose]  │ ← 蓝主操作 / 灰关闭(Cancel)
    /// │ _lblResult：已判定 M 台跳过 K 台    │ ← 蓝字，明细见 CSV
    /// └──────────────────────────────────┘
    /// 【流程】执行 → DeviceManager.RecordUnloadJudge（只收 Completed 台 →
    /// 逐台写"下料判定"CSV 事件 → 回空闲）。判定结果以 CSV/历史查询为准，
    /// 面板回空闲后不再保留（既有追溯链，见 RecordUnloadJudge 注释）。
    /// 【权限】操作员可操作（下料是操作员的活）；AutoPass 模式主窗体根本不让进。
    /// </summary>
    public partial class UnloadJudgeForm : Sunny.UI.UIForm
    {
        /// <summary>FAIL 处置选项（重测=回到待测，报废/降级/让步=出厂口径，由质量定）</summary>
        public static readonly string[] Dispositions = new string[]
        {
            "重测", "报废", "降级", "让步放行"
        };

        private readonly int[] _deviceIds;
        private readonly DeviceManager _deviceManager;

        /// <summary>
        /// 无参构造（仅 VS 设计器预览用：设计器必须调无参构造实例化，运行时一律走带参构造）。
        /// 给空快照占位（0 台送判），InitializeComponent 里的占位文本会被 BuildScopeText 覆盖为空快照文案，
        /// 预览不空白；判定按钮在空快照下点执行会因 manager 为 null 直接返回（见 BtnExecute_Click 守卫）。
        /// </summary>
        public UnloadJudgeForm()
            : this(new int[0], null)
        {
        }

        /// <param name="deviceIds">选中的工位号（主窗体已判空）</param>
        /// <param name="deviceManager">设备管理器（执行判定 + 读完成态）</param>
        public UnloadJudgeForm(int[] deviceIds, DeviceManager deviceManager)
        {
            _deviceIds = deviceIds ?? new int[0];
            _deviceManager = deviceManager;

            // 静态边框搬进 UnloadJudgeForm.Designer.cs，
            // 这里只回填"要吃构造参数"的那一项（范围文案依赖 deviceIds/deviceManager）。
            InitializeComponent();
            // 处置下拉选项在这里填：Designer 里写 Items.AddRange(Dispositions)
            // 会引用本类的静态字段，设计器 CodeDom 反序列化认不出致预览加载失败，
            // 所以 Designer 只留空下拉，运行时由构造填（4 项=Dispositions，回归锁个数）。
            _cmbDisposition.Items.AddRange(Dispositions);
            _lblScope.Text = BuildScopeText();
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
            // 设计器无参构造下 manager 为 null（仅预览/构造冒烟），直接提示返回，不 NRE。
            if (_deviceManager == null)
            {
                MessageBox.Show("设计预览模式，无设备管理器，不执行判定。", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
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
