using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using AgingTestSystem.Models;
using AgingTestSystem.Services;

namespace AgingTestSystem.Dialogs
{
    /// <summary>
    /// 配方管理窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 管理老化测试配方，包括：
    /// - 查看配方列表（左侧DataGridView表格，只显示序号和配方名称）
    /// - 选中配方后右侧显示该配方的设置内容，并可编辑
    ///   （配方名称、延时时间、烧屏时间、极限温度、负压阈值、显示模式）
    /// - 添加配方：名称与已有配方重名时询问是否更新已有配方
    /// - 更新配方：按当前配方名称找到列表中对应配方并更新其设置
    /// - 删除配方：按当前配方名称找到列表中对应配方并确认删除
    ///
    /// 【持久化（V1.27 起）】
    /// 添加 / 更新 / 删除 三个按钮在每次操作成功后都会自动把整个配方列表
    /// 持久化到本地 Recipes.json（见 <see cref="RecipeStorage.Save"/>），
    /// 因此不再需要独立的"保存设置"按钮（V1.27 已移除）。
    ///
    /// 【界面布局】
    /// ┌─────────────────────────────────────────────────────────────┐
    /// │ 配方管理窗口                                               │ ← 标题栏
    /// ├─────────────────────────────────────────────────────────────┤
    /// │ TableLayoutPanel (2列)                                      │
    /// │ ┌─────────────────────────┐  ┌─────────────────────────┐   │
    /// │ │ PanelLeft (左侧列表区)  │  │ PanelRight (右侧设置区) │   │
    /// │ │ ┌─────────────────────┐ │  │ ┌─────────────────────┐ │   │
    /// │ │ │ 序号  │ 配方名称     │ │  │ │ 配方设置            │ │   │
    /// │ │ │───────┼─────────────│ │  │ │ 配方名称：[_______]  │ │   │
    /// │ │ │ 1     │ ABCDEFGH    │ │  │ │ 延时时间：[ ][ ][ ] │ │   │
    /// │ │ │ 2     │ BBVJKNVK    │ │  │ │ 烧屏时间：[ ][ ][ ] │ │   │
    /// │ │ │ 3     │ RFTYHYJWF   │ │  │ │ 极限温度：[____]℃   │ │   │
    /// │ │ │ 4     │ WFRWGYJUK   │ │  │ │ 负压阈值：[____]kPa │ │   │ ← V1.66
    /// │ │ │ 5     │ FGYJKIewF   │ │  │ │ 显示模式：[_______] │ │   │ ← V1.66
    /// │ │ │(带滚动条)           │ │  │ ├─────────────────────┤ │   │
    /// │ │ └─────────────────────┘ │  │ │ [添加] [更新] [删除] │ │   │
    /// │ └─────────────────────────┘  │ └─────────────────────┘ │   │
    /// │ └─────────────────────────┘                                │
    /// └─────────────────────────────────────────────────────────────┘
    ///
    /// 【数据流转】
    /// 1. 窗体初始化时把传入的配方列表加载到左侧表格
    /// 2. 用户点击左侧表格某行，右侧输入框同步显示对应配方的设置内容
    /// 3. 用户在右侧输入框编辑后点击 添加/更新/删除，操作配方数据并自动落盘
    ///
    /// 【配方字段】
    /// - 序号：行号（从1开始）
    /// - 配方名称：配方的名称标识
    /// - 延时时间：上电前等待（时:分:秒）
    /// - 烧屏时间：上电后老化时长（时:分:秒）
    /// - 极限温度：测试极限温度（单位：℃）
    /// - 负压阈值：配方真空工艺要求（单位：kPa，V1.66；新建默认=全局阈值）
    /// - 显示模式：烧屏画面记录（自由文本，V1.66；只追溯不判定）
    ///
    /// 【持久化】
    /// 添加/更新/删除每次操作成功后自动通过 <see cref="RecipeStorage"/> 把整个配方列表
    /// 写入程序运行目录下的 Recipes.json；主窗体启动时加载该文件（见 MainForm.LoadRecipes）。
    /// </summary>
    public partial class RecipeManagerForm : Sunny.UI.UIForm
    {
        /// <summary>
        /// 配方列表（由外部传入，修改会反映到外部列表）
        /// </summary>
        private readonly List<RecipeConfig> _recipes;

        /// <summary>
        /// 新建配方时负压阈值输入框的默认值（【V1.66】= 全局 AlarmPressureThresholdKPa）。
        /// 项目未上线、无老配方包袱：新建所见即所得，不搞"0=用全局"魔法值。
        /// </summary>
        private readonly decimal _defaultNegativePressureKPa;

        /// <summary>
        /// 生效配置（【V1.75 新增】显示模式字典与开关走它；可为 null，
        /// null 时 Resolve 读当前项目文件——主窗体传 _config，测试传参即定）。
        /// </summary>
        private readonly DeviceConfig _displayConfig;

        /// <summary>
        /// 显示模式行是否显示（【V1.75 新增】构造时按开关定死，Fill/回填认它。
        /// 不读 cmb.Visible——窗体没 Show 时 Visible 读恒 false（V1.73 血泪），
        /// 读它 Fill 永远进隐藏分支，下拉永远是空的）。
        /// </summary>
        private readonly bool _displayModeShown;

        /// <summary>
        /// 悬停说明（每个配方项都挂 tooltip，超 40 字走
        /// SettingsForm.WrapTooltip 换行，全仓统一口径；
        /// 标签+输入框两边都挂；随 components 容器自动释放）。
        /// </summary>
        private ToolTip _tip;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="recipes">外部传入的配方列表，修改将反映到外部列表实例</param>
        /// <param name="defaultNegativePressureKPa">新建配方负压阈值默认值（传全局阈值）</param>
        /// <param name="config">生效配置（可选，不传读项目文件；显示模式开关与字典走它）</param>
        /// <exception cref="System.ArgumentNullException">recipes 为 null 时抛出</exception>
        public RecipeManagerForm(List<RecipeConfig> recipes, decimal defaultNegativePressureKPa,
            DeviceConfig config = null)
        {
            InitializeComponent();

            _recipes = recipes ?? throw new System.ArgumentNullException(nameof(recipes),
                "配方列表不能为 null，请传入外部维护的列表实例");
            _defaultNegativePressureKPa = defaultNegativePressureKPa;
            _displayConfig = config;

            LoadRecipesToGrid();

            if (_recipes.Count > 0)
            {
                dgvRecipes.Rows[0].Selected = true;
                UpdateRecipeSettings(_recipes[0]);
            }

            // 【V1.74】显示模式下拉填项（字典选项代码填，R8a 禁 Designer 写 AddRange）：
            // 字典改了重开本窗即换（窗体短命，不做热更）。
            // 【V1.75】开关关时整行隐藏 + 布局收缩（显示行是末行：按钮上移 38 + 窗高缩 38）。
            _displayModeShown = DisplayModeOptions.ShouldShowDisplayMode(_displayConfig);
            lblDisplayMode.Visible = _displayModeShown;
            cmbDisplayMode.Visible = _displayModeShown;
            if (_displayModeShown)
            {
                FillDisplayModes(null);
            }
            else
            {
                cmbDisplayMode.Text = "";
                btnAdd.Top -= DisplayModeRowHeight;
                btnUpdate.Top -= DisplayModeRowHeight;
                btnDelete.Top -= DisplayModeRowHeight;
                this.ClientSize = new Size(this.ClientSize.Width,
                    this.ClientSize.Height - DisplayModeRowHeight);
                this.MinimumSize = this.ClientSize;
            }

            // 每个配方项都挂悬停说明（标签+输入框两边都挂，超 40 字自动换行）。
            SetupTooltips();
        }

        /// <summary>
        /// 显示模式行高（【V1.75 新增】隐藏时布局收缩量 = 该行高 38px：
        /// 显示行 combo Y=241 高 29 → 底 270，按钮 Y=280（10px 间隙），
        /// 收缩后按钮 Y=242，窗高同步缩 38，行隙/边距原样保留）。
        /// </summary>
        private const int DisplayModeRowHeight = 38;

        /// <summary>
        /// 显示模式下拉填项（【V1.74 新增】字典驱动；遗留值参数供回填时带上旧值）。
        /// selectedAfterFill 为 null = 不动当前选择（构造时用）；非 null = 填完选中它
        /// （回填时用，遗留值不在字典则追加末尾，保证看得见）。
        /// </summary>
        private void FillDisplayModes(string selectedAfterFill, bool selectIt = false)
        {
            // 【V1.75】隐藏态守卫（同工位窗）：隐藏=恒空，防遗留值堵死保存。
            // 认 _displayModeShown 字段，不读 Visible（未 Show 恒 false，见字段注释）。
            if (!_displayModeShown)
            {
                cmbDisplayMode.Text = "";
                return;
            }
            var options = DisplayModeOptions.Resolve(_displayConfig);
            if (selectIt)
            {
                options = DisplayModeOptions.WithLegacy(options, selectedAfterFill);
            }
            cmbDisplayMode.Items.Clear();
            foreach (string o in options) cmbDisplayMode.Items.Add(o);
            if (selectIt) cmbDisplayMode.Text = (selectedAfterFill ?? "").Trim();
        }

        /// <summary>
        /// 给 6 个配方项挂悬停说明（标签+输入框两边都挂，悬停哪边都看得到）。
        /// 文案规则（小白能看懂）：每条=是什么+举例+填错会怎样；时间轴按真实流程写
        /// （点启动→只开阀→延时时间到+真空到位→上电→跑够烧屏时间→自动完成）；
        /// 量程按本窗控件写（极限温度 0~300℃ / 负压 ±9999kPa / 时间时0-99分秒0-59），
        /// 与批量窗（0~999℃文本框）/工位窗（0~300℃数字框）各按各的控件写，不互相抄。
        /// 超 40 字走 SettingsForm.WrapTooltip 换行（全仓唯一入口，不手写截断）。
        /// </summary>
        private void SetupTooltips()
        {
            // 本窗 Designer 没建 components 容器（历史原因：从没放过 ToolTip 类控件），
            // 这里补建一个，后续 Dispose 走容器自动释放（与 BatchRecipeForm 同口径）。
            if (this.components == null) this.components = new System.ComponentModel.Container();
            _tip = new ToolTip(this.components);
            _tip.ShowAlways = true;
            // 说明偏长，悬停提示多停留 15 秒（默认 5 秒看不完）。
            _tip.AutoPopDelay = 15000;
            SetTip(new Control[] { lblRecipeName, txtRecipeName },
                "配方名称：延时、温度、负压等一整套参数打包存一个名字，选用、下发都认它。" +
                "重名点添加时会询问是否覆盖更新。");
            SetTip(new Control[] { lblDelayTime, nudDelayHours, nudDelayMinutes, nudDelaySeconds },
                "延时时间：上电前等待。点启动后先只开真空阀（不上电），等够这么久才上电，" +
                "例如00:00:30=开阀30秒后上电，给吸附留稳定时间。填0=真空一到位立刻上电。" +
                "对应工位面板延时时间。");
            SetTip(new Control[] { lblBurnInTime, nudBurnInHours, nudBurnInMinutes, nudBurnInSeconds },
                "烧屏时间：上电后老化时长。上电开始计时，跑够这么久自动完成" +
                "（下电+关阀+PASS待取料），例如08:00:00=跑8小时。填00:00:00=不用配方时长、" +
                "走全局时长；全局也是0才一直跑、只能手动停。对应工位面板烧屏时间。");
            SetTip(new Control[] { lblLimitTemp, nudLimitTemp },
                "极限温度：该配方的温度上限（本窗0~300℃，1位小数）。只存档追溯：" +
                "面板不显示、不参与自动判定，填错不影响运行，但以后查配方看到的就是这个数。");
            SetTip(new Control[] { lblNegativePressure, nudNegativePressure },
                "负压阈值：真空到位线（kPa，±9999）。例如填-5：表读到-7（比-5更负）=吸住了、可上电；" +
                "读到-3（更接近0）=没吸住，宽限到了报真空失败。新建默认填全局阈值，" +
                "下发后启动时定格，改配方不影响在测。");
            SetTip(new Control[] { lblDisplayMode, cmbDisplayMode },
                "显示模式：这次烧屏跑的画面。只记档追溯：面板不显示、不参与判定，" +
                "但启动/报警日志里会记，方便事后查这批烧的什么画面。可选：" +
                string.Join("/", DisplayModeOptions.Resolve(_displayConfig).ToArray()) +
                "（字典在系统设置→工艺策略里改）。");
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
        /// 加载配方列表到DataGridView
        /// 只显示序号和配方名称两列
        /// </summary>
        private void LoadRecipesToGrid()
        {
            dgvRecipes.Rows.Clear();

            int seq = 1;
            foreach (var recipe in _recipes)
            {
                dgvRecipes.Rows.Add(seq++, recipe.Name);
            }
        }

        /// <summary>
        /// 把指定配方的设置内容同步到右侧输入框
        /// recipe 为 null 时清空所有输入框
        /// </summary>
        /// <param name="recipe">配方对象（null 表示清空）</param>
        private void UpdateRecipeSettings(RecipeConfig recipe)
        {
            if (recipe == null)
            {
                txtRecipeName.Clear();
                nudDelayHours.Value = nudDelayHours.Minimum;
                nudDelayMinutes.Value = nudDelayMinutes.Minimum;
                nudDelaySeconds.Value = nudDelaySeconds.Minimum;
                nudBurnInHours.Value = nudBurnInHours.Minimum;
                nudBurnInMinutes.Value = nudBurnInMinutes.Minimum;
                nudBurnInSeconds.Value = nudBurnInSeconds.Minimum;
                nudLimitTemp.Value = nudLimitTemp.Minimum;
                // 【V1.66】清空时负压回到新建默认值（全局阈值），显示模式清空
                nudNegativePressure.Value = ClampPressure(_defaultNegativePressureKPa);
                // 【V1.74】下拉清空=选空（Text="" 即 SelectedIndex=-1）；顺手重填字典，
                // 把之前回填追加的遗留值清掉（下拉选项永远等于干净字典）。
                FillDisplayModes(null);
                cmbDisplayMode.Text = "";
                return;
            }

            txtRecipeName.Text = recipe.Name;

            // 延时时间 → 时/分/秒
            SetTimeInputs(nudDelayHours, nudDelayMinutes, nudDelaySeconds, recipe.DelayTime);

            // 烧屏时间 → 时/分/秒
            SetTimeInputs(nudBurnInHours, nudBurnInMinutes, nudBurnInSeconds, recipe.BurnInTime);

            // 极限温度（超出 NumericUpDown 范围时钳制到边界）
            nudLimitTemp.Value = Math.Max(nudLimitTemp.Minimum,
                Math.Min(nudLimitTemp.Maximum, recipe.LimitTemperature));

            // 【V1.66】负压阈值 + 显示模式（超出范围钳制；显示模式 null→空串）
            nudNegativePressure.Value = ClampPressure(recipe.NegativePressure);
            // 【V1.74】下拉回填：字典选项 + 遗留值追加（老配方字典外文本看得见，存时拦整改）
            FillDisplayModes(recipe.DisplayMode, true);
        }

        /// <summary>
        /// 负压阈值钳制到输入框范围（【V1.66】±9999，超出时取边界，避免设 Value 越界抛异常）
        /// </summary>
        private decimal ClampPressure(decimal value)
        {
            return Math.Max(nudNegativePressure.Minimum,
                Math.Min(nudNegativePressure.Maximum, value));
        }

        /// <summary>
        /// 把 TimeSpan 拆分为 时/分/秒 写入三个 NumericUpDown
        /// 超出 NumericUpDown 范围时钳制到边界
        /// </summary>
        private void SetTimeInputs(NumericUpDown nudHours, NumericUpDown nudMinutes,
            NumericUpDown nudSeconds, TimeSpan timeSpan)
        {
            nudHours.Value = Math.Max(nudHours.Minimum,
                Math.Min(nudHours.Maximum, (decimal)(int)timeSpan.TotalHours));
            nudMinutes.Value = Math.Max(nudMinutes.Minimum,
                Math.Min(nudMinutes.Maximum, timeSpan.Minutes));
            nudSeconds.Value = Math.Max(nudSeconds.Minimum,
                Math.Min(nudSeconds.Maximum, timeSpan.Seconds));
        }

        /// <summary>
        /// 把当前输入框的内容写入 RecipeConfig 对象
        /// 配方名称为空时返回 false 并提示
        /// </summary>
        /// <param name="recipe">要写入的目标配方对象</param>
        /// <returns>写入成功返回 true，失败返回 false</returns>
        private bool TryApplyInputToRecipe(RecipeConfig recipe)
        {
            string name = txtRecipeName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入配方名称", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRecipeName.Focus();
                return false;
            }

            recipe.Name = name;
            recipe.DelayTime = new TimeSpan(
                (int)nudDelayHours.Value, (int)nudDelayMinutes.Value, (int)nudDelaySeconds.Value);
            recipe.BurnInTime = new TimeSpan(
                (int)nudBurnInHours.Value, (int)nudBurnInMinutes.Value, (int)nudBurnInSeconds.Value);
            recipe.LimitTemperature = nudLimitTemp.Value;
            // 【V1.66】负压阈值与显示模式一并写入：以前这里漏写 NegativePressure，
            // 新建配方该值恒 0，下发后真空保护≈关闭。现在存什么定格什么。
            recipe.NegativePressure = nudNegativePressure.Value;
            // 【V1.74】显示模式字典校验（Q20：空=清空允许，字典内=存规范写法，
            // 字典外拦并报出全部选项；本窗无配置对象，读项目文件字典）
            string canonicalMode, modeErr;
            if (!DisplayModeOptions.ValidateInput(cmbDisplayMode.Text,
                DisplayModeOptions.Resolve(_displayConfig), out canonicalMode, out modeErr))
            {
                MessageBox.Show(modeErr, "输入验证",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                cmbDisplayMode.Focus();
                return false;
            }
            recipe.DisplayMode = canonicalMode;
            return true;
        }

        /// <summary>
        /// 在配方列表中查找指定名称对应的索引（忽略大小写）
        /// </summary>
        /// <param name="name">配方名称</param>
        /// <returns>找到返回索引，未找到返回 -1</returns>
        private int FindRecipeIndex(string name)
        {
            return _recipes.FindIndex(r =>
                string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 刷新表格并用指定行的配方内容刷新右侧输入框
        /// </summary>
        private void SelectRecipeRow(int index)
        {
            if (index >= 0 && index < dgvRecipes.Rows.Count)
            {
                dgvRecipes.ClearSelection();
                dgvRecipes.Rows[index].Selected = true;
                UpdateRecipeSettings(_recipes[index]);
            }
        }

        /// <summary>
        /// 配方列表表格点击事件
        /// 选中某行后右侧输入框同步显示该配方的设置内容
        /// </summary>
        private void dgvRecipes_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _recipes.Count)
            {
                UpdateRecipeSettings(_recipes[e.RowIndex]);
            }
        }

        /// <summary>
        /// 添加按钮点击事件
        ///
        /// 【重名处理】
        /// 如果当前输入的配方名称已存在于列表中，弹窗询问"是否更新该配方"：
        /// - 确定 → 走与"更新"相同的逻辑（把输入内容覆盖到已有配方）
        /// - 取消 → 不做任何操作
        /// 名称不存在时，把当前输入内容作为新配方加入列表。
        /// </summary>
        private void btnAdd_Click(object sender, EventArgs e)
        {
            string name = txtRecipeName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入配方名称", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRecipeName.Focus();
                return;
            }

            // 检查是否与已有配方重名
            int existingIndex = FindRecipeIndex(name);
            if (existingIndex >= 0)
            {
                var result = MessageBox.Show(
                    $"已存在配方 \"{_recipes[existingIndex].Name}\"，是否更新覆盖该配方？",
                    "配方已存在",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question);

                // 确定 → 走与"更新"相同的逻辑
                if (result == DialogResult.OK)
                {
                    UpdateRecipeAt(existingIndex);
                }
                return;
            }

            // 名称不存在 → 新增配方
            var newRecipe = new RecipeConfig { CreateTime = DateTime.Now };
            if (!TryApplyInputToRecipe(newRecipe))
            {
                return;
            }

            _recipes.Add(newRecipe);
            PersistRecipes();
            LoadRecipesToGrid();
            SelectRecipeRow(_recipes.Count - 1);
            MessageBox.Show($"配方 \"{newRecipe.Name}\" 已添加", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 更新按钮点击事件
        /// 按当前输入的配方名称找到列表中对应配方，用输入内容覆盖更新
        /// </summary>
        private void btnUpdate_Click(object sender, EventArgs e)
        {
            string name = txtRecipeName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入配方名称", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRecipeName.Focus();
                return;
            }

            int index = FindRecipeIndex(name);
            if (index < 0)
            {
                // 列表中没有同名配方 → 直接添加（相当于"添加"功能）
                var newRecipe = new RecipeConfig { CreateTime = DateTime.Now };
                if (!TryApplyInputToRecipe(newRecipe))
                {
                    return;
                }

                _recipes.Add(newRecipe);
                PersistRecipes();
                LoadRecipesToGrid();
                SelectRecipeRow(_recipes.Count - 1);
                MessageBox.Show($"配方 \"{newRecipe.Name}\" 已添加", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            UpdateRecipeAt(index);
        }

        /// <summary>
        /// 用当前输入框内容更新列表中第 index 个配方
        /// （btnAdd 重名确定 与 btnUpdate 共用此逻辑）。
        /// 【大扫荡】先改副本：校验失败原对象不受污染（以前就地改，
        /// 字典校验不过时前 5 个字段已脏，内存与界面分叉）。
        /// </summary>
        /// <param name="index">配方在列表中的索引</param>
        private void UpdateRecipeAt(int index)
        {
            if (index < 0 || index >= _recipes.Count) return;

            RecipeConfig target = _recipes[index];
            RecipeConfig draft = target != null ? target.Clone() : new RecipeConfig();
            if (!TryApplyInputToRecipe(draft))
            {
                return;
            }
            _recipes[index] = draft;

            PersistRecipes();
            LoadRecipesToGrid();
            SelectRecipeRow(index);
            // 【V1.74】定格说明（Q18）：配方库更新只影响新启动，在测按旧参数跑完——
            // 本窗无 deviceManager 查不了在测，写死静态说明（不弹窗分支、不打扰）。
            MessageBox.Show($"配方 \"{draft.Name}\" 已更新\r\n（在测工位按启动时定格参数跑完，仅对新启动生效）", "提示",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 删除按钮点击事件
        ///
        /// 【删除条件】
        /// 按当前输入的配方名称判断（不论手动输入还是自动读取）：
        /// - 名称为空 → 提示先输入或选择配方
        /// - 名称在列表中不存在 → 提示列表中没有该配方
        /// - 名称在列表中存在 → 弹窗确认删除（确定/取消）
        /// </summary>
        private void btnDelete_Click(object sender, EventArgs e)
        {
            string name = txtRecipeName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            {
                MessageBox.Show("请输入或选择要删除的配方名称", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRecipeName.Focus();
                return;
            }

            int index = FindRecipeIndex(name);
            if (index < 0)
            {
                MessageBox.Show($"列表中不存在配方 \"{name}\"", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"确定要删除配方 \"{_recipes[index].Name}\" 吗？",
                "确认删除",
                MessageBoxButtons.OKCancel,
                MessageBoxIcon.Question);

            if (result == DialogResult.OK)
            {
                _recipes.RemoveAt(index);
                PersistRecipes();
                LoadRecipesToGrid();
                UpdateRecipeSettings(null);
                MessageBox.Show("配方已删除", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// 把当前配方列表持久化到本地 Recipes.json
        /// 供 添加 / 更新 / 删除 成功后自动调用（V1.27：操作即落盘，替代原"保存设置"按钮）。
        /// </summary>
        /// <returns>保存成功返回 true；失败返回 false（已弹窗提示）</returns>
        private bool PersistRecipes()
        {
            if (RecipeStorage.Save(_recipes))
            {
                return true;
            }

            MessageBox.Show("配方保存到本地文件失败，请检查文件写入权限", "错误",
                MessageBoxButtons.OK, MessageBoxIcon.Error);
            return false;
        }
    }
}
