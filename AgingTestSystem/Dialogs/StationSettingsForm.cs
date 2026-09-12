using System;
using System.Collections.Generic;
using System.Drawing;
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
    /// 状态、SN、配方、延时时间、启动时间、极限温度、负压阈值、显示模式。
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
    /// │  负压阈值:           [___]kPa │               │ ← V1.66
    /// │  显示模式:              [___] │               │ ← V1.66
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
    /// - 负压阈值 → 本工位真空工艺要求（V1.66；回填优先级 缓存 > 配方 > 全局，
    ///   下发=框里是什么就是什么，启动定格，存什么用什么）
    /// - 显示模式 → 配方 DisplayMode（V1.66；烧屏画面记录，只追溯不判定）
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
    public partial class StationSettingsForm : Sunny.UI.UIForm
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
        /// 显示模式行是否显示（【V1.75 新增】构造时按开关定死，Fill/回填认它。
        /// 不读 cmb.Visible——窗体没 Show 时 Visible 读恒 false，读它下拉永远是空的）。
        /// </summary>
        private readonly bool _displayModeShown;

        /// <summary>
        /// 悬停说明（【V1.73 新增】每个设置项+动作按钮都挂 tooltip，超 40 字走
        /// SettingsForm.WrapTooltip 换行，全仓统一口径；随 components 自动释放）。
        /// </summary>
        private ToolTip _tip;

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

            // 【V1.73】本机没装破空阀（VentValveEnabled=false，现状）时手动"破空"按钮
            // 直接隐藏：点了也没硬件可写，留着只会让人误会功能可用。
            // 有阀项目打开开关后按钮出现（手动破空具体动作等现场确认后实现，见 btnBreakVacuum_Click）。
            btnBreakVacuum.Visible = ShouldShowBreakVacuum(_config);

            // 窗口标题带工位编号，如"工位设置窗口 NO 1"
            this.Text = $"工位设置窗口 NO {deviceId}";

            // 从缓存 / 采集缓存读取当前工位数据并回显到输入框
            LoadStationData();

            // 【V1.74】显示模式下拉先填字典（LoadStationData 的回填分支会按需重填+选中；
            // 无缓存无数据直接返回时靠这一行保证下拉不空）。
            // 【V1.75】开关关时整行隐藏 + 布局收缩（显示行是左列末行 Y=326，
            // 右侧按钮止于 Y=254，窗高缩 40（370→330）边距原样保留）。
            _displayModeShown = DisplayModeOptions.ShouldShowDisplayMode(_config);
            lblDisplayMode.Visible = _displayModeShown;
            cmbDisplayMode.Visible = _displayModeShown;
            if (!_displayModeShown)
            {
                cmbDisplayMode.Text = "";
                this.ClientSize = new Size(this.ClientSize.Width, this.ClientSize.Height - 40);
                this.MinimumSize = this.ClientSize;
            }
            else if (cmbDisplayMode.Items.Count == 0) FillDisplayModes(null);

            // 初始化配方名称自动检索（V1.29 新增）
            _recipeAutoComplete = new RecipeAutoCompleteProvider(
                txtRecipe,
                _recipes,
                OnRecipeSelected);

            SetupTooltips();
        }

        /// <summary>
        /// 破空按钮是否显示（【V1.73 新增】纯函数：有阀才显示，回归可单测。
        /// 注：窗体没 Show 时 Control.Visible 读出来恒 false，所以用例测这个函数，
        /// 不直接读按钮 Visible——否则"有阀显示"永远红，见 V1.73 回归注释）。
        /// </summary>
        internal static bool ShouldShowBreakVacuum(DeviceConfig config)
        {
            return config != null && config.VentValveEnabled;
        }

        /// <summary>
        /// 给 8 个设置项 + 破空/下电按钮挂悬停说明（标签+输入框都挂，悬停哪边都看得到）。
        /// </summary>
        private void SetupTooltips()
        {
            _tip = new ToolTip(this.components);
            _tip.ShowAlways = true;
            SetTip(new Control[] { lblState, txtState },
                "状态：只读，当前工位实时状态（空闲/选中/繁忙/故障/已完成）。");
            SetTip(new Control[] { lblSN, txtSN },
                "SN：该工位的产品序列号，空表示未绑定。0时长与空SN能不能启动，在工艺策略里配。");
            SetTip(new Control[] { lblRecipe, txtRecipe },
                "配方：该工位当前配方。输入时自动检索已有配方，选中后自动回填延时、温度、负压、显示模式。");
            SetTip(new Control[] { lblDelay, nudDelayHours, nudDelayMinutes, nudDelaySeconds },
                "延时时间：对应工位面板延时开启。上电后等这么久才开始计时老化（时:分:秒）。");
            SetTip(new Control[] { lblStart, nudStartHours, nudStartMinutes, nudStartSeconds },
                "启动时间：对应工位面板延时到达，与延时时间共同决定上电时序（时:分:秒）。");
            SetTip(new Control[] { lblTemp, nudTemp },
                "极限温度：该工位温度上限（0~300°C）。只记录追溯，不参与自动判定。");
            SetTip(new Control[] { lblPressure, nudPressure },
                "负压阈值：该工位真空到位判定阈值（kPa）。回填优先级：上次保存>配方>全局；保存即下发，启动时定格。");
            SetTip(new Control[] { lblDisplayMode, cmbDisplayMode },
                "显示模式：下拉选择本次烧屏跑的显示画面，只追溯不判定。可选：" +
                string.Join("/", DisplayModeOptions.Resolve(_config).ToArray()) +
                "（字典在系统设置→工艺策略里改；保存时按字典校验）。");
            SetTip(new Control[] { btnBreakVacuum },
                "破空：手动释放负压方便取料。本机未装破空阀时按钮自动隐藏（VentValveEnabled开关）。");
            SetTip(new Control[] { btnPowerOff },
                "下电：关闭本工位载台上电输出。");
        }

        /// <summary>给一组控件挂同一条说明（超 40 字自动换行）。</summary>
        private void SetTip(Control[] controls, string text)
        {
            string tip = SettingsForm.WrapTooltip(text);
            foreach (Control c in controls)
            {
                if (c != null) _tip.SetToolTip(c, tip);
            }
        }

        /// <summary>
        /// 配方自动检索选中回调（V1.29 新增）
        /// 用户从自动检索列表中选择一个配方后，自动填写配方名称、延时时间、启动时间、
        /// 极限温度、负压阈值、显示模式（V1.66 补后两项）。
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

            // 回填极限温度（超出 NumericUpDown 范围时钳制到边界，与配方管理窗一致）
            nudTemp.Value = Math.Max(nudTemp.Minimum,
                Math.Min(nudTemp.Maximum, recipe.LimitTemperature));

            // 【V1.66】回填负压阈值 + 显示模式（配方一定有实数，直接显示；显示模式 null→空串）
            nudPressure.Value = Math.Max(nudPressure.Minimum,
                Math.Min(nudPressure.Maximum, recipe.NegativePressure));
            // 【V1.74】下拉回填（字典 + 遗留值追加）
            FillDisplayModes(recipe.DisplayMode, true);
        }

        /// <summary>
        /// 显示模式下拉填项（【V1.74 新增】字典驱动，字典走本窗生效配置。
        /// selectIt=true 时选中给定值，遗留值追加末尾保证看得见）。
        /// </summary>
        private void FillDisplayModes(string selectedAfterFill, bool selectIt = false)
        {
            // 【V1.75】隐藏态守卫：开关关时回填（选配方/缓存）不得写值，
            // 否则遗留值进框→保存校验拦→隐藏功能反而堵死保存。隐藏=恒空。
            // 认 _displayModeShown 字段（不读 Visible，见字段注释）。
            if (!_displayModeShown)
            {
                cmbDisplayMode.Text = "";
                return;
            }
            var options = DisplayModeOptions.Resolve(_config);
            if (selectIt)
            {
                options = DisplayModeOptions.WithLegacy(options, selectedAfterFill);
            }
            cmbDisplayMode.Items.Clear();
            foreach (string o in options) cmbDisplayMode.Items.Add(o);
            if (selectIt) cmbDisplayMode.Text = (selectedAfterFill ?? "").Trim();
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
                nudTemp.Value = Math.Max(nudTemp.Minimum,
                    Math.Min(nudTemp.Maximum, cached.LimitTemperature));
                // 【V1.66】负压/显示模式回填优先级：缓存（非0/非空）> 配方（按缓存配方名命中）> 全局/空
                nudPressure.Value = Math.Max(nudPressure.Minimum,
                    Math.Min(nudPressure.Maximum, ResolveCachedPressure(cached)));
                // 【V1.74】下拉回填（字典 + 遗留值追加）
                FillDisplayModes(ResolveCachedDisplayMode(cached), true);
                return;
            }

            // 2) 无缓存：从设备管理器实时数据回填
            if (data == null) return;

            txtSN.Text = data.SerialNumber;
            txtRecipe.Text = data.RecipeName;
            SetTimeInputs(nudDelayHours, nudDelayMinutes, nudDelaySeconds, data.DelayTime);
            SetTimeInputs(nudStartHours, nudStartMinutes, nudStartSeconds, data.StartTime);
            // 【V1.66】无缓存时负压/显示模式按面板配方名找配方：命中用配方的，
            // 否则全局/空。避免框里留 Designer 默认 0 被下发成阈值 0（负压域里≈关保护）。
            RecipeConfig recipeHit = FindRecipe(data.RecipeName);
            decimal fallbackPressure = recipeHit != null ? recipeHit.NegativePressure
                : (_config != null ? _config.AlarmPressureThresholdKPa : 0m);
            nudPressure.Value = Math.Max(nudPressure.Minimum,
                Math.Min(nudPressure.Maximum, fallbackPressure));
            // 【V1.74】下拉回填（字典 + 遗留值追加）
            FillDisplayModes(recipeHit?.DisplayMode, true);
        }

        /// <summary>
        /// 解析回填用负压阈值（【V1.66 新增】优先级：缓存非0 > 配方命中 > 全局）。
        /// 缓存是上次亲手存的值最可信；0 说明没存过（老缓存/从未保存），
        /// 此时按缓存里的配方名找配方，命中用配方的，都没有用全局——框里永远是实数。
        /// </summary>
        private decimal ResolveCachedPressure(StationCacheEntry cached)
        {
            if (cached.NegativePressure != 0m) return cached.NegativePressure;
            RecipeConfig hit = FindRecipe(cached.RecipeName);
            if (hit != null) return hit.NegativePressure;
            return _config != null ? _config.AlarmPressureThresholdKPa : 0m;
        }

        /// <summary>
        /// 解析回填用显示模式（【V1.66 新增】优先级：缓存非空 > 配方命中 > 空串）。
        /// </summary>
        private string ResolveCachedDisplayMode(StationCacheEntry cached)
        {
            if (!string.IsNullOrEmpty(cached.DisplayMode)) return cached.DisplayMode;
            RecipeConfig hit = FindRecipe(cached.RecipeName);
            return hit?.DisplayMode ?? "";
        }

        /// <summary>
        /// 按配方名检索本地配方列表（忽略大小写，空名/未命中返回 null）
        /// </summary>
        private RecipeConfig FindRecipe(string recipeName)
        {
            if (string.IsNullOrWhiteSpace(recipeName) || _recipes == null) return null;
            foreach (RecipeConfig r in _recipes)
            {
                if (string.Equals(r.Name, recipeName.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    return r;
                }
            }
            return null;
        }

        /// <summary>
        /// 计算工位的当前状态文本（中文：空闲 / 选中 / 繁忙 / 故障 / 已完成，V1.18 由英文改中文）
        /// 规则与工位面板工作状态一致（见 WorkstationGridView.ApplyData）：
        /// - 故障 → 故障
        /// - 测试中 → 繁忙
        /// - 已完成·待取料 → 已完成（V1.64.2补：以前漏了这一支，完成台会误显示为空闲）
        /// - 空闲且载台已上电 → 选中
        /// - 空闲且载台未上电 → 空闲
        /// </summary>
        /// <param name="data">工位数据</param>
        /// <returns>状态文本</returns>
        private string GetStateText(BarometerData data)
        {
            if (data.Status == DeviceStatus.Fault) return "故障";
            if (data.Status == DeviceStatus.Testing) return "繁忙";
            if (data.Status == DeviceStatus.Completed) return "已完成";

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

            // 【V1.74】显示模式字典校验（Q20：空=清空允许，字典内=存规范写法并回写框，
            // 字典外拦；一次校验管住下面三处写入：下发/缓存/配方）。
            string canonicalMode, modeErr;
            if (!DisplayModeOptions.ValidateInput(cmbDisplayMode.Text,
                DisplayModeOptions.Resolve(_config), out canonicalMode, out modeErr))
            {
                MessageBox.Show(modeErr, "输入验证",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbDisplayMode.Focus();
                return false;
            }
            cmbDisplayMode.Text = canonicalMode;

            // ---- 1) 组合延时开启 / 延时到达（各三个 NumericUpDown，V1.28；控件已限范围无需校验） ----
            TimeSpan delayStart = GetTimeSpan(nudDelayHours, nudDelayMinutes, nudDelaySeconds);
            TimeSpan delayArrive = GetTimeSpan(nudStartHours, nudStartMinutes, nudStartSeconds);

            // ---- 2) 应用配置到当前工位（写入工位静态信息，采集叠加后工位面板更新） ----
            _deviceManager.SetStationSerialNumber(_deviceId, txtSN.Text);

            // 【V1.66】下发=框里是什么就是什么：LoadStationData 回填已保证框里是实数
            // （优先级 缓存 > 配方 > 全局），不再按配方名二次检索。空配方名=清空（含负压/显示模式）。
            // 启动测试时负压值定格为该工位的真空到位判定/报警阈值（配方优先、全局兜底指"没下发时"，
            // 下发了就以框值为准——框里永远有数，不存在"没下发"）。
            _deviceManager.SetStationRecipe(_deviceId, txtRecipe.Text, nudPressure.Value, cmbDisplayMode.Text);
            _deviceManager.SetStationDelayTimes(_deviceId, delayStart, delayArrive);

            // ---- 3) 缓存配置（下次打开该工位设置窗口自动回填） ----
            StationSettingsCache.Save(new StationCacheEntry
            {
                DeviceId = _deviceId,
                SerialNumber = txtSN.Text.Trim(),
                RecipeName = txtRecipe.Text.Trim(),
                DelayTime = delayStart,
                StartTime = delayArrive,
                LimitTemperature = ParseTemperature(),
                NegativePressure = nudPressure.Value,
                DisplayMode = cmbDisplayMode.Text.Trim()
            });

            // ---- 4) 保存配方到本地配方列表（同名询问覆盖更新；配方名称为空则跳过） ----
            if (!string.IsNullOrWhiteSpace(txtRecipe.Text))
            {
                SaveCurrentRecipe(delayStart, delayArrive);
            }

            // ---- 5) 提示 ----
            // 【V1.74】定格护栏（Q18）：本工位在测时，本次下发仅对新启动生效——
            // 判定口径与项目切换禁切一致（GetTestingDeviceIds 含本工位）。
            string testingNote = "";
            try
            {
                int[] testing = _deviceManager.GetTestingDeviceIds();
                if (testing != null)
                {
                    foreach (int t in testing)
                    {
                        if (t == _deviceId)
                        {
                            testingNote = "\r\n注意：该工位正在测试，按启动时定格参数跑完，" +
                                "本次修改仅对新启动生效。";
                            break;
                        }
                    }
                }
            }
            catch { /* 在测查询失败按无在测处理，不阻断保存 */ }
            MessageBox.Show(
                $"工位 {_deviceId} {actionName}成功！\r\n" +
                $"SN: {(string.IsNullOrWhiteSpace(txtSN.Text) ? "（空）" : txtSN.Text.Trim())}\r\n" +
                $"配方: {(string.IsNullOrWhiteSpace(txtRecipe.Text) ? "（空）" : txtRecipe.Text.Trim())}\r\n" +
                $"延时开启: {GetTimeText(delayStart)}\r\n" +
                $"延时到达: {GetTimeText(delayArrive)}\r\n" +
                $"极限温度: {nudTemp.Value:0.#}°C\r\n" +
                $"负压阈值: {nudPressure.Value:0.#}kPa\r\n" +
                $"显示模式: {(string.IsNullOrWhiteSpace(cmbDisplayMode.Text) ? "（空）" : cmbDisplayMode.Text.Trim())}" +
                testingNote,
                $"{actionName}成功", MessageBoxButtons.OK, MessageBoxIcon.Information);

            return true;
        }

        /// <summary>
        /// 把当前窗口的配方（名称 / 延时 / 极限温度 / 负压阈值 / 显示模式）保存到本地配方列表
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
                NegativePressure = nudPressure.Value,
                DisplayMode = cmbDisplayMode.Text.Trim(),
                CreateTime = DateTime.Now,
                IsEnabled = true
            };

            RecipeStorage.SaveWithDuplicateCheck(_recipes, recipe);
        }

        /// <summary>
        /// 读取极限温度输入（【V1.63】txtTemp 文本框改为 nudTemp 数字框后恒合法：
        /// 控件已限 0~300/1 位小数，非法输入根本进不来，V1.62 的"非法存 0"问题
        /// 从输入端消除。本方法保留一层薄封装，使 CommitConfig/SaveCurrentRecipe
        /// 两处调用点不用动）。
        /// </summary>
        /// <returns>极限温度数值（摄氏度）</returns>
        private decimal ParseTemperature()
        {
            return nudTemp.Value;
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
            // 【V1.62】时必须用 TotalHours：time.Hours 是"小时分量"（0~23），
            // 25 小时会回填成 1（与 RecipeManagerForm 的 TotalHours 写法不一致，
            // 现对齐）。分/秒本就是分量（0~59），保持不动。
            hours.Value = Clamp(hours, (int)time.TotalHours);
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
            // 【V1.62】与 SetTimeInputs 对齐用 TotalHours：25 小时显示 "25:00:00"
            // 而不是截断的 "01:00:00"（成功提示文案与回填值一致，不再各说各话）。
            return string.Format(@"{0:00}:{1:00}:{2:00}", (int)time.TotalHours, time.Minutes, time.Seconds);
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
