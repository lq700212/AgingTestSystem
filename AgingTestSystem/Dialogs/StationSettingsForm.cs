using System;
using System.Collections.Generic;
using System.Windows.Forms;
using AgingTestSystem.Models;
using AgingTestSystem.Services;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 工位设置窗口（业务逻辑部分）—— V1.18 新增，V1.26 完善按钮业务，V1.28 延时/启动时间改 NumericUpDown
    ///
    /// 【功能说明】
    /// 点击工位面板上的"设置"按钮（btnSet）后弹出本窗口，
    /// 用于查看 / 设置单个工位的测试相关参数：
    /// 状态、SN、配方、延时时间、启动时间、极限温度。
    ///
    /// 【界面布局】
    /// ┌────────────────────────────────────────────────┐
    /// │ 工位设置窗口 NO 1                                │  ← 标题（带工位编号）
    /// ├────────────────────────────────┬───────────────┤
    /// │ 左侧设置列（整体居中）          │ 右侧按钮列      │
    /// │  状态:                  [空闲] │ [破空]        │
    /// │  SN:                    [___] │ [下电]        │
    /// │  配方:                  [___] │ [保存]        │
    /// │  延时时间:            [__]:[__]:[__] │ [加入对列]     │
    /// │  启动时间:            [__]:[__]:[__] │ [关闭窗口]     │
    /// │  极限温度:              [___] │               │
    /// └────────────────────────────────┴───────────────┘
    ///
    /// 【按钮语义（V1.26）】
    /// - 保存（btnSave）：把当前配置应用到本工位面板（写入 DeviceManager 工位静态信息）
    ///   + 缓存配置（下次打开该工位设置窗口自动回填）+ 保存配方到本地配方列表（有同名询问覆盖）；
    /// - 加入对列（btnAddToQueue）：把当前配置应用到本工位面板 + 保存配方到本地配方列表
    ///   （与"保存"语义一致，仅提示文案不同）；
    /// - 下电（btnPowerOff）：关闭本工位载台上电输出（下电）；
    /// - 破空（btnBreakVacuum）：业务暂未确认，保留 TODO；
    /// - 关闭窗口（btnClose）：直接关闭本窗体。
    ///
    /// 【字段映射】
    /// - 延时时间 → 延时开启（DelayTime）
    /// - 启动时间 → 延时到达（StartTime）
    /// - 极限温度 → 配方配置的 LimitTemperature（缓存 / 配方存储，工位面板无此显示）
    ///
    /// 【时间输入（V1.28）】
    /// 延时时间 / 启动时间各用三个 NumericUpDown（时:分:秒，冒号分隔，样式与 RecipeManagerForm 一致）：
    /// 时 0-99、分 0-59、秒 0-59，控件自带范围限制无需再校验；
    /// 读取时用 GetTimeSpan 组合三个框，回填时用 SetTimeInputs 拆分并钳制到控件范围。
    ///
    /// 【数据来源】
    /// 构造时传入设备管理器与共享配方列表，从缓存（优先）或采集缓存读取当前工位数据回显；
    /// SN / 配方 / 延时来自工位静态信息叠加后的缓存（与工位面板一致）。
    /// </summary>
    public partial class StationSettingsForm : Form
    {
        /// <summary>设备管理器（用于读写工位数据 / 载台上电输出）</summary>
        private readonly DeviceManager _deviceManager;

        /// <summary>设备配置（计算 IO 输出点编号：载台上电输出）</summary>
        private readonly DeviceConfig _config;

        /// <summary>主窗体共享的配方列表（保存配方时直接修改，与「参数设置 → 配方管理」共用）</summary>
        private readonly List<RecipeConfig> _recipes;

        /// <summary>当前操作的工位编号（1 ~ TotalBarometers）</summary>
        private readonly int _deviceId;

        /// <summary>配方名称自动检索提供者（V1.29 新增，使用后需释放）</summary>
        private RecipeAutoCompleteProvider _recipeAutoComplete;

        /// <summary>
        /// 构造函数
        /// 初始化界面，设置标题为"工位设置窗口 NO X"，并从缓存 / 采集缓存回显当前工位数据。
        /// </summary>
        /// <param name="deviceManager">设备管理器（可为 null，null 时仅显示空输入框）</param>
        /// <param name="config">设备配置</param>
        /// <param name="recipes">主窗体共享的配方列表（保存配方时修改并落盘）</param>
        /// <param name="deviceId">工位编号（从1开始）</param>
        public StationSettingsForm(DeviceManager deviceManager, DeviceConfig config,
            List<RecipeConfig> recipes, int deviceId)
        {
            InitializeComponent();
            _deviceManager = deviceManager;
            _config = config;
            _recipes = recipes;
            _deviceId = deviceId;

            // 窗口标题带工位编号，如"工位设置窗口 NO 1"
            this.Text = $"工位设置窗口 NO {deviceId}";

            // 从缓存 / 采集缓存读取当前工位数据并回显到输入框
            LoadStationData();

            // 初始化配方名称自动检索（V1.29 新增）
            _recipeAutoComplete = new RecipeAutoCompleteProvider(
                txtRecipe,
                _recipes,
                OnRecipeSelected);
        }

        /// <summary>
        /// 配方自动检索选中回调（V1.29 新增）
        /// 用户从自动检索列表中选择一个配方后，自动填写配方名称、延时时间、启动时间、极限温度。
        /// </summary>
        /// <param name="recipe">选中的配方</param>
        private void OnRecipeSelected(RecipeConfig recipe)
        {
            if (recipe == null) return;

            txtRecipe.Text = recipe.Name;

            // 回填延时时间（时:分:秒）
            nudDelayHours.Value = Math.Max(nudDelayHours.Minimum,
                Math.Min(nudDelayHours.Maximum, (decimal)(int)recipe.DelayTime.TotalHours));
            nudDelayMinutes.Value = Math.Max(nudDelayMinutes.Minimum,
                Math.Min(nudDelayMinutes.Maximum, (decimal)recipe.DelayTime.Minutes));
            nudDelaySeconds.Value = Math.Max(nudDelaySeconds.Minimum,
                Math.Min(nudDelaySeconds.Maximum, (decimal)recipe.DelayTime.Seconds));

            // 回填启动时间（时:分:秒）
            nudStartHours.Value = Math.Max(nudStartHours.Minimum,
                Math.Min(nudStartHours.Maximum, (decimal)(int)recipe.StartTime.TotalHours));
            nudStartMinutes.Value = Math.Max(nudStartMinutes.Minimum,
                Math.Min(nudStartMinutes.Maximum, (decimal)recipe.StartTime.Minutes));
            nudStartSeconds.Value = Math.Max(nudStartSeconds.Minimum,
                Math.Min(nudStartSeconds.Maximum, (decimal)recipe.StartTime.Seconds));

            // 回填极限温度
            txtTemp.Text = recipe.LimitTemperature.ToString("0.#");
        }

        /// <summary>
        /// 回显当前工位数据
        ///
        /// 【回填优先级（V1.26）】
        /// 1. 若该工位存在上次"保存"的配置缓存（StationSettingsCache）→ 全部从缓存回填
        ///    （下次点击该工位"设置"按钮自动回填上一次缓存的信息）；
        /// 2. 无缓存 → 从设备管理器采集缓存读取（SN / 配方 / 延时来自工位静态信息叠加，与工位面板一致）；
        ///    采集未开始 / 离线时保持输入框为空。
        /// 状态（空闲/选中/繁忙/故障）始终读实时数据。
        /// </summary>
        private void LoadStationData()
        {
            BarometerData data = _deviceManager?.GetBarometerData(_deviceId);

            // 状态始终读实时数据（空闲/选中/繁忙/故障）
            if (data != null)
            {
                txtState.Text = GetStateText(data);
            }

            // 1) 优先回填该工位上次保存的配置缓存
            StationCacheEntry cached = StationSettingsCache.Get(_deviceId);
            if (cached != null)
            {
                txtSN.Text = cached.SerialNumber;
                txtRecipe.Text = cached.RecipeName;
                SetTimeInputs(nudDelayHours, nudDelayMinutes, nudDelaySeconds, cached.DelayTime);
                SetTimeInputs(nudStartHours, nudStartMinutes, nudStartSeconds, cached.StartTime);
                txtTemp.Text = cached.LimitTemperature.ToString("0.#");
                return;
            }

            // 2) 无缓存：从设备管理器实时数据回填
            if (data == null) return;

            txtSN.Text = data.SerialNumber;
            txtRecipe.Text = data.RecipeName;
            SetTimeInputs(nudDelayHours, nudDelayMinutes, nudDelaySeconds, data.DelayTime);
            SetTimeInputs(nudStartHours, nudStartMinutes, nudStartSeconds, data.StartTime);
        }

        /// <summary>
        /// 计算工位的当前状态文本（中文：空闲 / 选中 / 繁忙 / 故障，V1.18 由英文改中文）
        /// 规则与工位面板工作状态一致：
        /// - 故障 → 故障
        /// - 测试中 → 繁忙
        /// - 空闲且载台已上电 → 选中
        /// - 空闲且载台未上电 → 空闲
        /// </summary>
        /// <param name="data">工位数据</param>
        /// <returns>状态文本</returns>
        private string GetStateText(BarometerData data)
        {
            if (data.Status == DeviceStatus.Fault) return "故障";
            if (data.Status == DeviceStatus.Testing) return "繁忙";

            // 载台上电输出状态（OutputStatus[1]）
            bool carrierPower = data.OutputStatus != null &&
                                data.OutputStatus.Length >= 2 &&
                                data.OutputStatus[1];
            return carrierPower ? "选中" : "空闲";
        }

        /// <summary>
        /// 破空按钮点击事件
        /// 具体业务功能（如：开启真空电磁阀释放负压 / 手动排空）待确认后实现。
        /// </summary>
        private void btnBreakVacuum_Click(object sender, EventArgs e)
        {
            // TODO: 破空功能待确认后实现（例如：开启该工位真空电磁阀释放负压 / 手动排空）
        }

        /// <summary>
        /// 下电按钮点击事件（V1.26 实现）
        ///
        /// 【功能】关闭当前工位的载台上电输出（下电）。
        /// 载台上电输出内部编号 = TotalInputs + TotalBarometers + deviceId
        /// （IO 映射：每台 1 输入 + 2 输出：真空电磁阀 + 载台上电，见 IoMapBuilder）。
        ///
        /// 【处理】
        /// - 当前为已上电 → 下发关闭命令，提示已下电；
        /// - 当前已处于下电状态 → 仅提示，不重复下发。
        /// </summary>
        private void btnPowerOff_Click(object sender, EventArgs e)
        {
            if (_deviceManager == null || _config == null)
            {
                MessageBox.Show("设备管理器未就绪，无法下电", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 载台上电输出内部编号（依据IO分配表：Y110~Y217）
            int carrierOutputId = _config.TotalInputs + _config.TotalBarometers + _deviceId;

            // 读取当前状态，仅当下电状态为"已上电"时才下发关闭命令
            bool isCarrierPoweredOn = _deviceManager.GetOutput(carrierOutputId);
            if (isCarrierPoweredOn)
            {
                _deviceManager.SetOutput(carrierOutputId, false);
                MessageBox.Show($"工位 {_deviceId} 载台已下电", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show($"工位 {_deviceId} 载台当前已处于下电状态", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// 保存按钮点击事件（V1.26 完善）
        ///
        /// 【功能】
        /// 1. 把当前录入的 SN / 配方 / 延时开启 / 延时到达 写入设备管理器工位静态信息，
        ///    采集线程下次叠加后，工位面板（SN / 配方 / 延时显示）即同步更新；
        /// 2. 把当前配置缓存到 StationSettingsCache（下次点击该工位"设置"按钮自动回填）；
        /// 3. 把当前配方（名称 / 延时 / 极限温度）保存到本地配方列表
        ///    （有同名询问是否覆盖更新，配方名称为空时跳过）；
        /// 4. 提示并关闭窗口。
        ///
        /// 【说明】
        /// - SN / 配方：可空，空串视为清空。
        /// - 延时开启 / 延时到达：各用三个 NumericUpDown（时:分:秒，V1.28），
        ///   默认 00:00:00（TimeSpan.Zero），控件范围 时0-99/分0-59/秒0-59 无需再校验。
        /// - 启动时间输入在本窗体对应"延时到达"（与 RecipeManagerForm 语义一致）。
        /// </summary>
        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!CommitConfig("保存"))
            {
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// 加入对列按钮点击事件（V1.26 实现）
        ///
        /// 【功能】把当前配置好的信息加载到对应工位的 WorkstationPanelView 上
        /// （与"保存"一致：写入设备管理器工位静态信息 → 采集叠加 → 工位面板更新），
        /// 并把当前配方保存到本地配方列表（有同名询问覆盖更新）。
        ///
        /// 【与"保存"的区别】"保存"额外把配置写入工位配置缓存（下次打开自动回填）；
        /// 本按钮同样写入缓存，保证下次打开也能回填，两按钮提示文案不同。
        /// </summary>
        private void btnAddToQueue_Click(object sender, EventArgs e)
        {
            if (!CommitConfig("加入对列"))
            {
                return;
            }

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        /// <summary>
        /// 提交配置到当前工位（"保存"与"加入对列"共用）
        ///
        /// 【流程】
        /// 1. 校验设备管理器就绪、组合延时开启 / 延时到达；
        /// 2. 写入设备管理器工位静态信息（SN / 配方 / 延时）→ 工位面板同步更新；
        /// 3. 写入工位配置缓存（StationSettingsCache，下次打开自动回填）；
        /// 4. 配方名称非空时，把当前配方保存到本地配方列表（有同名询问是否覆盖更新）。
        /// </summary>
        /// <param name="actionName">操作名称（用于成功提示文案，如"保存"/"加入对列"）</param>
        /// <returns>true=提交成功；false=校验失败或用户取消覆盖</returns>
        private bool CommitConfig(string actionName)
        {
            if (_deviceManager == null)
            {
                MessageBox.Show("设备管理器未就绪，无法提交配置", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }

            // ---- 1) 组合延时开启 / 延时到达（各三个 NumericUpDown，V1.28；控件已限范围无需校验） ----
            TimeSpan delayStart = GetTimeSpan(nudDelayHours, nudDelayMinutes, nudDelaySeconds);
            TimeSpan delayArrive = GetTimeSpan(nudStartHours, nudStartMinutes, nudStartSeconds);

            // ---- 2) 应用配置到当前工位（写入工位静态信息，采集叠加后工位面板更新） ----
            _deviceManager.SetStationSerialNumber(_deviceId, txtSN.Text);
            _deviceManager.SetStationRecipeName(_deviceId, txtRecipe.Text);
            _deviceManager.SetStationDelayTimes(_deviceId, delayStart, delayArrive);

            // ---- 3) 缓存配置（下次打开该工位设置窗口自动回填） ----
            StationSettingsCache.Save(new StationCacheEntry
            {
                DeviceId = _deviceId,
                SerialNumber = txtSN.Text.Trim(),
                RecipeName = txtRecipe.Text.Trim(),
                DelayTime = delayStart,
                StartTime = delayArrive,
                LimitTemperature = ParseTemperature()
            });

            // ---- 4) 保存配方到本地配方列表（同名询问覆盖更新；配方名称为空则跳过） ----
            if (!string.IsNullOrWhiteSpace(txtRecipe.Text))
            {
                SaveCurrentRecipe(delayStart, delayArrive);
            }

            // ---- 5) 提示 ----
            MessageBox.Show(
                $"工位 {_deviceId} {actionName}成功！\r\n" +
                $"SN: {(string.IsNullOrWhiteSpace(txtSN.Text) ? "（空）" : txtSN.Text.Trim())}\r\n" +
                $"配方: {(string.IsNullOrWhiteSpace(txtRecipe.Text) ? "（空）" : txtRecipe.Text.Trim())}\r\n" +
                $"延时开启: {GetTimeText(delayStart)}\r\n" +
                $"延时到达: {GetTimeText(delayArrive)}\r\n" +
                $"极限温度: {(string.IsNullOrWhiteSpace(txtTemp.Text) ? "（空）" : txtTemp.Text.Trim())}°C",
                $"{actionName}成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

            return true;
        }

        /// <summary>
        /// 把当前窗口的配方（名称 / 延时 / 极限温度）保存到本地配方列表
        /// 有同名配方时由 SaveWithDuplicateCheck 询问是否覆盖更新
        /// </summary>
        /// <param name="delayStart">延时开启时间</param>
        /// <param name="delayArrive">延时到达时间</param>
        private void SaveCurrentRecipe(TimeSpan delayStart, TimeSpan delayArrive)
        {
            var recipe = new RecipeConfig
            {
                Name = txtRecipe.Text.Trim(),
                DelayTime = delayStart,
                StartTime = delayArrive,
                LimitTemperature = ParseTemperature(),
                CreateTime = DateTime.Now,
                IsEnabled = true
            };

            RecipeStorage.SaveWithDuplicateCheck(_recipes, recipe);
        }

        /// <summary>
        /// 解析极限温度输入（非法 / 为空返回 0）
        /// </summary>
        /// <returns>极限温度数值（摄氏度）</returns>
        private decimal ParseTemperature()
        {
            if (decimal.TryParse(txtTemp.Text.Trim(), out decimal temp))
            {
                return temp;
            }
            return 0m;
        }

        /// <summary>
        /// 组合三个 NumericUpDown（时/分/秒）为 TimeSpan（V1.28 新增，替代原文本解析）
        /// 控件 Maximum 已限制范围（时0-99 / 分0-59 / 秒0-59），无需额外校验。
        /// </summary>
        /// <param name="hours">时输入框</param>
        /// <param name="minutes">分输入框</param>
        /// <param name="seconds">秒输入框</param>
        /// <returns>组合后的 TimeSpan（始终有效，默认 00:00:00）</returns>
        private static TimeSpan GetTimeSpan(NumericUpDown hours, NumericUpDown minutes, NumericUpDown seconds)
        {
            return new TimeSpan((int)hours.Value, (int)minutes.Value, (int)seconds.Value);
        }

        /// <summary>
        /// 用 TimeSpan 回填三个 NumericUpDown（时/分/秒）（V1.28 新增）
        /// 值超出控件范围时钳制到 Min/Max，避免设置 Value 越界抛异常。
        /// </summary>
        /// <param name="hours">时输入框</param>
        /// <param name="minutes">分输入框</param>
        /// <param name="seconds">秒输入框</param>
        /// <param name="time">要回填的时间（如工位静态信息的延时开启 / 延时到达）</param>
        private static void SetTimeInputs(NumericUpDown hours, NumericUpDown minutes, NumericUpDown seconds, TimeSpan time)
        {
            hours.Value = Clamp(hours, time.Hours);
            minutes.Value = Clamp(minutes, time.Minutes);
            seconds.Value = Clamp(seconds, time.Seconds);
        }

        /// <summary>
        /// 把数值钳制到 NumericUpDown 的 Min ~ Max 范围（V1.28 新增）
        /// </summary>
        /// <param name="nud">目标控件</param>
        /// <param name="value">原始数值</param>
        /// <returns>钳制后的数值</returns>
        private static decimal Clamp(NumericUpDown nud, int value)
        {
            return Math.Max(nud.Minimum, Math.Min(nud.Maximum, value));
        }

        /// <summary>
        /// 把 TimeSpan 格式化为 时:分:秒 文本（V1.28 新增，供成功提示文案使用）
        /// </summary>
        /// <param name="time">时间</param>
        /// <returns>格式化的时间文本（如 01:10:20）</returns>
        private static string GetTimeText(TimeSpan time)
        {
            return string.Format(@"{0:00}:{1:00}:{2:00}", time.Hours, time.Minutes, time.Seconds);
        }

        /// <summary>
        /// 关闭窗口按钮点击事件
        /// 直接关闭本窗口。
        /// </summary>
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.Close();
        }
    }
}
