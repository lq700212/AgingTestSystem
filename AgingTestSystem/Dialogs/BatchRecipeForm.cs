using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Models;
using AgingTestSystem.Services;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 批量设置配方窗口（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 本窗口用于批量设置配方参数（配方名称、延时时间、烧屏时间、极限温度、
    /// 负压阈值、显示模式），
    /// 点击"加入队列"按钮：
    /// 1. 先把当前配置的配方保存到本地配方存储（Recipes.json，有同名则询问是否覆盖更新）；
    /// 2. 判断当前是否至少选中了一个工位面板（WorkstationPanelView）：
    ///    - 没有任何选中 → 提示"请先选择工位"，配方已保存，可在「参数设置 → 配方管理」中选用，
    ///      或关闭窗口、选中工位后再打开本窗口重新点击"加入队列"应用到选中工位；
    ///    - 有选中 → 把该配方的名称 / 延时时间 / 烧屏时间 /
    ///      负压阈值 / 显示模式应用到所有选中的工位面板。
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
    /// │ 配方名称：[下拉选择▼]                    │  ← 配方下拉（V1.88.13：只能选库中配方）
    /// │ 延时时间：[__]:[__]:[__]                    │  ← 延时时间（NumericUpDown，对应延时时间）
    /// │ 烧屏时间：[__]:[__]:[__]                    │  ← 烧屏时间（NumericUpDown，对应烧屏时间）
    /// │ 极限温度：[____] °C                         │  ← 极限温度输入框
    /// │ 负压阈值：[____] kPa                        │  ← V1.66：配方真空工艺要求
    /// │ 显示模式：[____________]                    │  ← V1.66：烧屏画面记录
    /// ├─────────────────────────────────────────────┤
    /// │         [加入队列]                          │  ← 保存配方 + 应用到选中工位
    /// │         [关闭窗口]                          │  ← 直接关闭
    /// └─────────────────────────────────────────────┘
    ///
    /// 【字段映射（V1.28 与配方管理窗口对齐）】
    /// - 延时时间 → RecipeConfig.DelayTime（工位面板"延时时间"）
    /// - 烧屏时间 → RecipeConfig.BurnInTime（工位面板"烧屏时间"）
    /// - 极限温度 → RecipeConfig.LimitTemperature
    /// - 负压阈值 → RecipeConfig.NegativePressure（V1.66；必填实数，新建默认=全局阈值）
    /// - 显示模式 → RecipeConfig.DisplayMode（V1.66；自由文本，只追溯不判定）
    ///
    /// 【注意事项】
    /// 1. 延时时间 / 烧屏时间均使用三个 NumericUpDown（时:分:秒，V1.28 由 TextBox 改）：
    ///    时 0-99、分 0-59、秒 0-59，控件自带范围限制，无需再校验；
    /// 2. 温度输入框限制为3位数字，范围 0-999°C；
    /// 3. 配方名称不能为空（下拉没选即空，照旧拦截）。
    /// 4. 配方名称是下拉选择（V1.88.13 由输入框改，自动检索 Provider 同步删除）：
    ///    只能选配方库中已有的配方，选中后自动回填延时时间、烧屏时间、极限温度、
    ///    负压阈值、显示模式；新建配方走「参数设置 → 配方管理」。
    /// </summary>
    public partial class BatchRecipeForm : Sunny.UI.UIForm
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
        /// 悬停说明（【V1.73 新增】每个设置项都挂 tooltip，超 40 字走
        /// SettingsForm.WrapTooltip 换行，全仓统一口径）。
        /// </summary>
        private ToolTip _tip;

        /// <summary>
        /// 显示模式行是否显示（【V1.75 新增】构造时按开关定死，Fill 认它。
        /// 不读 cmb.Visible——窗体没 Show 时 Visible 读恒 false，读它下拉永远是空的）。
        /// </summary>
        private readonly bool _displayModeShown;

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

            // 【V1.88.13】配方下拉填项（只能从库里选，手输错名串配方从根上堵死；
            // 新建配方走「参数设置 → 配方管理」。默认不选中，由用户亲手选）。
            FillRecipeCombo();
            cmbRecipeName.SelectedIndexChanged += CmbRecipeName_SelectedIndexChanged;

            // 【V1.66】负压阈值框新建默认值=全局阈值（项目未上线无老包袱，所见即所得）；
            // _deviceManager 为 null（纯保存模式）时用 DeviceConfig 类默认值（-5kPa）。
            decimal defaultPressure = _deviceManager != null
                ? _deviceManager.Config.AlarmPressureThresholdKPa
                : new DeviceConfig().AlarmPressureThresholdKPa;
            txtNegativePressure.Text = defaultPressure.ToString("0.#");

            // 【V1.74】显示模式下拉填项（构造时填字典；改字典重开本窗即换）
            // 【V1.75】开关关时整行隐藏 + 布局收缩（显示行是输入表末 Percent 行：
            // 行高改 Absolute 0 + 窗高缩 37，行隙/按钮区原样保留）。
            DeviceConfig displayCfg = _deviceManager != null ? _deviceManager.Config : null;
            _displayModeShown = DisplayModeOptions.ShouldShowDisplayMode(displayCfg);
            lblDisplayModeLabel.Visible = _displayModeShown;
            cmbDisplayMode.Visible = _displayModeShown;
            if (_displayModeShown)
            {
                FillDisplayModes(null);
            }
            else
            {
                cmbDisplayMode.Text = "";
                tableLayoutPanelInput.RowStyles[5].SizeType = SizeType.Absolute;
                tableLayoutPanelInput.RowStyles[5].Height = 0;
                this.ClientSize = new Size(this.ClientSize.Width, this.ClientSize.Height - 37);
                this.MinimumSize = this.ClientSize;
            }

            SetupTooltips();
        }

        /// <summary>
        /// 给 6 个设置项挂悬停说明（标签+输入框都挂，悬停哪边都看得到）。
        /// 文案规则（小白能看懂）：每条=是什么+举例+填错会怎样；时间轴按真实流程写
        /// （点启动→只开阀→延时时间到+真空到位→上电→跑够烧屏时间→自动完成）；
        /// 极限温度量程按本窗文本框写（0~999℃），不抄配方管理窗 0~300 数字框口径。
        /// </summary>
        private void SetupTooltips()
        {
            // 本窗 Designer 没建 components 容器（历史原因：从没放过 ToolTip 类控件），
            // 这里补建一个，后续 Dispose 走容器自动释放（与 StationSettingsForm 同口径）。
            if (this.components == null) this.components = new System.ComponentModel.Container();
            _tip = new ToolTip(this.components);
            _tip.ShowAlways = true;
            // 说明偏长，悬停提示多停留 15 秒（默认 5 秒看不完）。
            _tip.AutoPopDelay = 15000;
            SetTip(new Control[] { lblRecipeNameLabel, cmbRecipeName },
                "配方名称：从下拉选择已有配方（只能选库里有的，打错字串配方的事从根上堵死），" +
                "选中后自动回填延时、温度、负压、显示模式。新建配方走「参数设置 → 配方管理」。");
            SetTip(new Control[] { lblDelayTime1Label, tableLayoutPanelDelay1 },
                "延时时间：上电前等待。点启动后先只开真空阀（不上电），等够这么久才上电，" +
                "例如00:00:30=开阀30秒后上电，给吸附留稳定时间。填0=真空一到位立刻上电。" +
                "对应工位面板延时时间。");
            SetTip(new Control[] { lblBurnInTimeLabel, tableLayoutPanelBurnIn },
                "烧屏时间：上电后老化时长。上电开始计时，跑够这么久自动完成" +
                "（下电+关阀+PASS待取料），例如08:00:00=跑8小时。填00:00:00=不用配方时长、" +
                "走全局时长；全局也是0才一直跑、只能手动停。对应工位面板烧屏时间。");
            SetTip(new Control[] { lblLimitTempLabel, txtLimitTemp },
                "极限温度：该配方的温度上限（本窗0~999℃）。只存档追溯：" +
                "面板不显示、不参与自动判定，填错不影响运行，但以后查配方看到的就是这个数。");
            SetTip(new Control[] { lblNegativePressureLabel, txtNegativePressure },
                "负压阈值：真空到位线（kPa）。例如填-5：表读到-7（比-5更负）=吸住了、可上电；" +
                "读到-3（更接近0）=没吸住，宽限到了报真空失败。新建默认填全局阈值，" +
                "下发后启动时定格，改配方不影响在测。");
            SetTip(new Control[] { lblDisplayModeLabel, cmbDisplayMode },
                "显示模式：这次烧屏跑的画面。只记档追溯：面板不显示、不参与判定，" +
                "但启动/报警日志里会记，方便事后查这批烧的什么画面。可选：" +
                string.Join("/", DisplayModeOptions.Resolve(
                    _deviceManager != null ? _deviceManager.Config : null).ToArray()) +
                "（字典在系统设置→工艺策略里改；保存时按字典校验）。");
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
        /// 配方下拉填项（【V1.88.13 新增】构造时调一次：选项 = 配方库全部配方名。
        /// 默认不选中（SelectedIndex=-1），保存时空名字照旧拦截，逼用户亲手选）。
        /// </summary>
        private void FillRecipeCombo()
        {
            cmbRecipeName.Items.Clear();
            if (_recipes != null)
            {
                foreach (RecipeConfig r in _recipes)
                {
                    if (r != null && !string.IsNullOrWhiteSpace(r.Name))
                    {
                        cmbRecipeName.Items.Add(r.Name.Trim());
                    }
                }
            }
            cmbRecipeName.SelectedIndex = -1;
            cmbRecipeName.Text = "";
        }

        /// <summary>
        /// 配方下拉选择变化（【V1.88.13 新增】按选中名查库回填参数；
        /// 未选中/名字在库中已无（库被改过）直接返回，不动现有输入）。
        /// </summary>
        private void CmbRecipeName_SelectedIndexChanged(object sender, EventArgs e)
        {
            string name = cmbRecipeName.Text.Trim();
            if (string.IsNullOrEmpty(name) || _recipes == null) return;
            foreach (RecipeConfig r in _recipes)
            {
                if (r != null && string.Equals(r.Name, name, StringComparison.Ordinal))
                {
                    OnRecipeSelected(r);
                    return;
                }
            }
        }

        /// <summary>
        /// 配方下拉选中回调（V1.29 自动检索时代新增，V1.88.13 转下拉后只回填参数：
        /// 名字已是下拉选中的，不用再写一遍）。
        /// 用户从下拉中选择一个配方后，自动填写延时时间、烧屏时间、极限温度。
        /// </summary>
        /// <param name="recipe">选中的配方</param>
        private void OnRecipeSelected(RecipeConfig recipe)
        {
            if (recipe == null) return;

            // 回填延时时间（时:分:秒）
            nudDelayHours.Value = Math.Max(nudDelayHours.Minimum,
                Math.Min(nudDelayHours.Maximum, (decimal)(int)recipe.DelayTime.TotalHours));
            nudDelayMinutes.Value = Math.Max(nudDelayMinutes.Minimum,
                Math.Min(nudDelayMinutes.Maximum, (decimal)recipe.DelayTime.Minutes));
            nudDelaySeconds.Value = Math.Max(nudDelaySeconds.Minimum,
                Math.Min(nudDelaySeconds.Maximum, (decimal)recipe.DelayTime.Seconds));

            // 回填烧屏时间（时:分:秒）
            nudBurnInHours.Value = Math.Max(nudBurnInHours.Minimum,
                Math.Min(nudBurnInHours.Maximum, (decimal)(int)recipe.BurnInTime.TotalHours));
            nudBurnInMinutes.Value = Math.Max(nudBurnInMinutes.Minimum,
                Math.Min(nudBurnInMinutes.Maximum, (decimal)recipe.BurnInTime.Minutes));
            nudBurnInSeconds.Value = Math.Max(nudBurnInSeconds.Minimum,
                Math.Min(nudBurnInSeconds.Maximum, (decimal)recipe.BurnInTime.Seconds));

            // 回填极限温度
            txtLimitTemp.Text = recipe.LimitTemperature.ToString("0.#");

            // 【V1.66】回填负压阈值 + 显示模式（配方一定有实数，直接显示；显示模式 null→空串）
            txtNegativePressure.Text = recipe.NegativePressure.ToString("0.#");
            // 【V1.74】下拉回填（字典 + 遗留值追加，看得见存时拦）
            FillDisplayModes(recipe.DisplayMode, true);
        }

        /// <summary>
        /// 显示模式下拉填项（【V1.74 新增】字典驱动；字典走本机生效配置，
        /// 无 manager（纯保存模式）时读项目文件。selectIt=true 时选中给定值。）
        /// </summary>
        private void FillDisplayModes(string selectedAfterFill, bool selectIt = false)
        {
            // 【V1.75】隐藏态守卫（同工位窗）：隐藏=恒空，防遗留值堵死保存。
            // 认 _displayModeShown 字段（不读 Visible，见字段注释）。
            if (!_displayModeShown)
            {
                cmbDisplayMode.Text = "";
                return;
            }
            var options = DisplayModeOptions.Resolve(
                _deviceManager != null ? _deviceManager.Config : null);
            if (selectIt)
            {
                options = DisplayModeOptions.WithLegacy(options, selectedAfterFill);
            }
            cmbDisplayMode.Items.Clear();
            foreach (string o in options) cmbDisplayMode.Items.Add(o);
            if (selectIt) cmbDisplayMode.Text = (selectedAfterFill ?? "").Trim();
        }

        /// <summary>
        /// 获取当前窗口输入的配方配置
        /// 从各个输入控件中读取值，校验通过后创建并返回 RecipeConfig 对象。
        /// </summary>
        /// <returns>配方配置对象；验证失败返回 null（已弹窗提示）</returns>
        private RecipeConfig GetCurrentRecipeConfig()
        {
            // 验证配方名称（下拉必选一项；没选=空，照旧拦截）
            string recipeName = cmbRecipeName.Text.Trim();
            if (string.IsNullOrEmpty(recipeName))
            {
                MessageBox.Show("请从下拉选择配方名称", "输入验证",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbRecipeName.Focus();
                return null;
            }

            // 读取延时时间（时:分:秒，NumericUpDown 控件已限制范围，无需额外校验）→ 延时时间
            TimeSpan delayTime = new TimeSpan(
                (int)nudDelayHours.Value, (int)nudDelayMinutes.Value, (int)nudDelaySeconds.Value);

            // 读取烧屏时间（时:分:秒，NumericUpDown 控件已限制范围，无需额外校验）→ 烧屏时间
            // （V1.28 与配方管理窗口对齐，两个时间都保存）
            TimeSpan burnInTime = new TimeSpan(
                (int)nudBurnInHours.Value, (int)nudBurnInMinutes.Value, (int)nudBurnInSeconds.Value);

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

            // 【V1.66】解析负压阈值（kPa，必填实数）：与公共参数窗同口径 ±9999。
            // 不搞"0=用全局"魔法——新建默认已填全局值，用户看到的就是存的。
            decimal negativePressure;
            if (!decimal.TryParse(txtNegativePressure.Text.Trim(), out negativePressure))
            {
                MessageBox.Show("负压阈值输入无效，请输入数字", "输入验证",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNegativePressure.Focus();
                return null;
            }
            if (negativePressure < -9999 || negativePressure > 9999)
            {
                MessageBox.Show("负压阈值超出范围（-9999~9999 kPa）", "输入验证",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtNegativePressure.Focus();
                return null;
            }

            // 【V1.74】显示模式字典校验（Q20：空=清空允许，字典内=存规范写法，
            // 字典外拦并报出全部选项；字典走本机生效配置，无 manager 时读项目文件）
            string canonicalMode, modeErr;
            if (!DisplayModeOptions.ValidateInput(cmbDisplayMode.Text,
                DisplayModeOptions.Resolve(_deviceManager != null ? _deviceManager.Config : null),
                out canonicalMode, out modeErr))
            {
                MessageBox.Show(modeErr, "输入验证",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbDisplayMode.Focus();
                return null;
            }

            // 创建配方配置对象
            // Id 由 RecipeStorage.SaveWithDuplicateCheck 在保存时统一分配，这里留 0。
            // 延时时间 → DelayTime（延时时间），烧屏时间 → BurnInTime（烧屏时间），
            // 与配方管理窗口 / 工位设置窗口的字段映射完全一致（V1.28 对齐）。
            return new RecipeConfig
            {
                Name = recipeName,
                DelayTime = delayTime,
                BurnInTime = burnInTime,
                LimitTemperature = limitTemp,
                NegativePressure = negativePressure,
                DisplayMode = canonicalMode,
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
        ///    - 有选中 → 把配方的名称 / 延时时间 / 烧屏时间应用到所有选中工位。
        /// </summary>
        private void btnAddToQueue_Click(object sender, EventArgs e)
        {
            // ---- 1) 校验并构建当前配方 ----
            RecipeConfig recipe = GetCurrentRecipeConfig();
            if (recipe == null)
            {
                return;
            }

            // ---- 2) 保存配方到本地配方存储（同名先问是否覆盖） ----
            if (RecipeStorage.FindDuplicateIndex(_recipes, recipe.Name) >= 0)
            {
                var confirm = MessageBox.Show(
                    $"已存在配方 \"{recipe.Name}\"，是否覆盖更新该配方？",
                    "配方已存在",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question);
                if (confirm != DialogResult.OK) return;   // 取消覆盖：放弃本次加入队列
            }
            bool saved = RecipeStorage.SaveRecipe(_recipes, recipe, true);
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

                // 写入工位静态信息（采集线程叠加后，工位面板同步显示配方名称 / 延时时间 / 烧屏时间）
                // 【V1.59】配方的负压值一并下发：启动测试时作为该工位的真空到位/报警阈值
                // 【V1.66】显示模式一并下发：烧屏画面追溯（采集叠加到 BarometerData.DisplayMode）
                _deviceManager.SetStationRecipe(deviceId, recipe.Name, recipe.NegativePressure, recipe.DisplayMode);
                _deviceManager.SetStationDelayTimes(deviceId, recipe.DelayTime, recipe.BurnInTime);
                appliedCount++;
            }

            // ---- 5) 成功提示 ----
            // 【V1.74】定格护栏（Q18 接受定格语义后的小提示）：在测工位按启动瞬间定格的
            // 旧参数跑完，本次下发只影响新启动——有交集才提示，无交集不打扰。
            string testingNote = BuildTestingNote();
            MessageBox.Show(
                $"配方 \"{recipe.Name}\" 已保存到本地配方列表，\r\n" +
                $"并已应用到 {appliedCount} 个选中的工位面板！" + testingNote,
                "加入队列成功",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// <summary>
        /// 定格提示文案（【V1.74 新增】纯逻辑可单测：选中工位与在测工位有交集才提示）。
        /// 在测判定走 DeviceManager.GetTestingDeviceIds（与项目切换禁切同口径）；
        /// manager 为 null/异常按"无在测"处理（纯保存模式不打扰）。
        /// </summary>
        private string BuildTestingNote()
        {
            try
            {
                if (_deviceManager == null || _selectedDeviceIds == null) return "";
                int[] testing = _deviceManager.GetTestingDeviceIds();
                if (testing == null || testing.Length == 0) return "";
                foreach (int id in _selectedDeviceIds)
                {
                    foreach (int t in testing)
                    {
                        if (id == t)
                        {
                            return "\r\n\r\n注意：选中工位中有在测任务，在测按启动时定格参数跑完，" +
                                "本次修改仅对新启动生效。";
                        }
                    }
                }
            }
            catch { /* 在测查询失败按无在测处理，不阻断加入队列 */ }
            return "";
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
