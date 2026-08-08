using System;
using System.Collections.Generic;
using System.Windows.Forms;
using BarometerWinform.Models;
using BarometerWinform.Services;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 批量设置配方窗口（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 本窗口用于批量设置配方参数（配方名称、延时时间、启动时间、极限温度），
    /// 点击"加入队列"按钮：
    /// 1. 先把当前配置的配方保存到本地配方存储（Recipes.json，有同名则询问是否覆盖更新）；
    /// 2. 判断当前是否至少选中了一个工位面板（WorkstationPanelView）：
    ///    - 没有任何选中 → 提示"请先选择工位"，配方已保存，可在「参数设置 → 配方管理」中选用，
    ///      或关闭窗口、选中工位后再打开本窗口重新点击"加入队列"应用到选中工位；
    ///    - 有选中 → 把该配方的名称 / 延时开启（延时时间）/ 延时到达（启动时间）应用到所有选中的工位面板。
    /// "关闭窗口"按钮直接关闭本窗体。
    ///
    /// 【数据流转】
    /// 1. 主窗体在弹出本窗口时传入：设备管理器（应用配方到工位）、共享配方列表（_recipes）、
    ///    当前选中的工位编号数组（可能为空）。
    /// 2. 用户在窗口中填写各项配方参数。
    /// 3. 点击"加入队列" → 保存配方到共享列表并落盘 → 应用到选中工位。
    ///
    /// 【界面布局】
    /// ┌─────────────────────────────────────────────┐
    /// │ 批量设置设置配方窗口                         │  ← 标题栏
    /// ├─────────────────────────────────────────────┤
    /// │ 配方名称：[____________]                    │  ← 配方名称输入框
    /// │ 延时时间：[__]:[__]:[__]                    │  ← 延时时间（NumericUpDown，对应延时开启）
    /// │ 启动时间：[__]:[__]:[__]                    │  ← 启动时间（NumericUpDown，对应延时到达）
    /// │ 极限温度：[____] °C                         │  ← 极限温度输入框
    /// ├─────────────────────────────────────────────┤
    /// │         [加入队列]                          │  ← 保存配方 + 应用到选中工位
    /// │         [关闭窗口]                          │  ← 直接关闭
    /// └─────────────────────────────────────────────┘
    ///
    /// 【字段映射（V1.28 与配方管理窗口对齐）】
    /// - 延时时间 → RecipeConfig.DelayTime（工位面板"延时开启"）
    /// - 启动时间 → RecipeConfig.StartTime（工位面板"延时到达"）
    /// - 极限温度 → RecipeConfig.LimitTemperature
    ///
    /// 【注意事项】
    /// 1. 延时时间 / 启动时间均使用三个 NumericUpDown（时:分:秒，V1.28 由 TextBox 改）：
    ///    时 0-99、分 0-59、秒 0-59，控件自带范围限制，无需再校验；
    /// 2. 温度输入框限制为3位数字，范围 0-999°C；
    /// 3. 配方名称不能为空。
    /// </summary>
    public partial class BatchRecipeForm : Form
    {
        /// <summary>
        /// 设备管理器（用于把配方应用（写入工位静态信息）到选中的工位面板）
        /// 可为 null，null 时不执行"应用到工位"，仅保存配方。
        /// </summary>
        private readonly DeviceManager _deviceManager;

        /// <summary>
        /// 主窗体共享的配方列表（保存配方时直接修改，与「参数设置 → 配方管理」共用同一列表）
        /// </summary>
        private readonly List<RecipeConfig> _recipes;

        /// <summary>
        /// 当前选中的工位编号数组（主窗体传入，可能为空表示一个工位都没选中）
        /// </summary>
        private readonly IReadOnlyList<int> _selectedDeviceIds;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="deviceManager">设备管理器（可 null，仅影响"应用到工位"）</param>
        /// <param name="recipes">主窗体共享的配方列表（保存配方时修改并落盘）</param>
        /// <param name="selectedDeviceIds">当前选中的工位编号（允许为空数组）</param>
        public BatchRecipeForm(DeviceManager deviceManager, List<RecipeConfig> recipes,
            IReadOnlyList<int> selectedDeviceIds)
        {
            InitializeComponent();

            _deviceManager = deviceManager;
            _recipes = recipes;
            _selectedDeviceIds = selectedDeviceIds ?? new List<int>();
        }

        /// <summary>
        /// 获取当前窗口输入的配方配置
        /// 从各个输入控件中读取值，校验通过后创建并返回 RecipeConfig 对象。
        /// </summary>
        /// <returns>配方配置对象；验证失败返回 null（已弹窗提示）</returns>
        private RecipeConfig GetCurrentRecipeConfig()
        {
            // 验证配方名称
            string recipeName = txtRecipeName.Text.Trim();
            if (string.IsNullOrEmpty(recipeName))
            {
                MessageBox.Show("请输入配方名称", "输入验证",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRecipeName.Focus();
                return null;
            }

            // 读取延时时间（时:分:秒，NumericUpDown 控件已限制范围，无需额外校验）→ 延时开启
            TimeSpan delayTime = new TimeSpan(
                (int)nudDelayHours.Value, (int)nudDelayMinutes.Value, (int)nudDelaySeconds.Value);

            // 读取启动时间（时:分:秒，NumericUpDown 控件已限制范围，无需额外校验）→ 延时到达
            TimeSpan startTime = new TimeSpan(
                (int)nudStartHours.Value, (int)nudStartMinutes.Value, (int)nudStartSeconds.Value);

            // 延时到达：由"启动时间"输入框填写（V1.28 与配方管理窗口对齐，两个时间都保存）
            TimeSpan delayArriveTime = startTime;

            // 解析极限温度
            decimal limitTemp;
            if (!decimal.TryParse(txtLimitTemp.Text.Trim(), out limitTemp))
            {
                MessageBox.Show("极限温度输入无效，请输入数字", "输入验证",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtLimitTemp.Focus();
                return null;
            }

            // 温度范围验证（0°C ~ 999°C）
            if (limitTemp < 0 || limitTemp > 999)
            {
                MessageBox.Show("极限温度超出范围（0-999°C）", "输入验证",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtLimitTemp.Focus();
                return null;
            }

            // 创建配方配置对象
            // Id 由 RecipeStorage.SaveWithDuplicateCheck 在保存时统一分配，这里留 0。
            // 延时时间 → DelayTime（延时开启），启动时间 → StartTime（延时到达），
            // 与配方管理窗口 / 工位设置窗口的字段映射完全一致（V1.28 对齐）。
            return new RecipeConfig
            {
                Name = recipeName,
                DelayTime = delayTime,
                StartTime = delayArriveTime,
                LimitTemperature = limitTemp,
                CreateTime = DateTime.Now,
                IsEnabled = true
            };
        }

        /// <summary>
        /// 加入队列按钮点击事件
        ///
        /// 【流程】
        /// 1. 校验并构建当前配方（GetCurrentRecipeConfig）；
        /// 2. 保存配方到本地配方列表（SaveWithDuplicateCheck，有同名询问是否覆盖更新）；
        ///    用户取消覆盖 / 保存失败 → 直接返回，不做任何事；
        /// 3. 判断是否选中了工位：
        ///    - 一个工位都没选中 → 提示"请先选择工位"，配方已保存；
        ///    - 有选中 → 把配方的名称 / 延时开启（延时时间）/ 延时到达（启动时间）应用到所有选中工位。
        /// </summary>
        private void btnAddToQueue_Click(object sender, EventArgs e)
        {
            // ---- 1) 校验并构建当前配方 ----
            RecipeConfig recipe = GetCurrentRecipeConfig();
            if (recipe == null)
            {
                return;
            }

            // ---- 2) 保存配方到本地配方存储（有同名则询问是否覆盖更新） ----
            bool saved = RecipeStorage.SaveWithDuplicateCheck(_recipes, recipe);
            if (!saved)
            {
                // 用户取消覆盖 或 保存失败：放弃本次加入队列
                return;
            }

            // ---- 3) 判断是否有选中的工位面板 ----
            if (_selectedDeviceIds == null || _selectedDeviceIds.Count == 0)
            {
                // 一个工位都没选中：配方已保存，提醒用户先选择工位
                MessageBox.Show(
                    "当前没有任何选中的工位面板！\r\n\r\n" +
                    $"配方 \"{recipe.Name}\" 已保存到本地配方列表。\r\n\r\n" +
                    "请关闭本窗口后，在主界面选中至少一个工位，\r\n" +
                    "再打开本窗口点击\"加入队列\"即可应用到选中的工位；\r\n" +
                    "也可以直接在「参数设置 → 配方管理」中选用该配方。",
                    "请先选择工位",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            // ---- 4) 应用到所有选中的工位面板 ----
            int appliedCount = 0;
            foreach (int deviceId in _selectedDeviceIds)
            {
                if (_deviceManager == null) break;

                // 写入工位静态信息（采集线程叠加后，工位面板同步显示配方名称 / 延时开启 / 延时到达）
                _deviceManager.SetStationRecipeName(deviceId, recipe.Name);
                _deviceManager.SetStationDelayTimes(deviceId, recipe.DelayTime, recipe.StartTime);
                appliedCount++;
            }

            // ---- 5) 成功提示 ----
            MessageBox.Show(
                $"配方 \"{recipe.Name}\" 已保存到本地配方列表，\r\n" +
                $"并已应用到 {appliedCount} 个选中的工位面板！",
                "加入队列成功",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// <summary>
        /// 关闭窗口按钮点击事件
        /// 直接关闭当前窗体（不保存、不应用任何内容）。
        /// </summary>
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
