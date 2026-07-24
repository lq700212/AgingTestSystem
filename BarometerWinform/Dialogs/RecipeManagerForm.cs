using System;
using System.Collections.Generic;
using System.Windows.Forms;
using BarometerWinform.Models;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 配方管理窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 管理老化测试配方，包括：
    /// - 查看配方列表（左侧DataGridView表格，只显示序号和配方名称）
    /// - 选中配方后在右侧显示详细信息（配方名称、延时时间、启动时间、极限温度）
    /// - 添加配方
    /// - 更新配方
    /// - 删除配方
    /// - 保存设置（整体保存所有配方配置）
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
    /// │ │ │───────┼─────────────│ │  │ │ 配方名称：ABCDEFGH   │ │   │
    /// │ │ │ 1     │ ABCDEFGH    │ │  │ │ 延时时间：1:20:30    │ │   │
    /// │ │ │ 2     │ BBVJKNVK    │ │  │ │ 启动时间：2:10:5     │ │   │
    /// │ │ │ 3     │ RFTYHYJWF   │ │  │ │ 极限温度：50℃        │ │   │
    /// │ │ │ 4     │ WFRWGYJUK   │ │  │ ├─────────────────────┤ │   │
    /// │ │ │ 5     │ FGYJKIewF   │ │  │ │ [添加] [更新] [删除] │ │   │
    /// │ │ │(带滚动条)           │ │  │ └─────────────────────┘ │   │
    /// │ │ └─────────────────────┘ │  └─────────────────────────┘   │
    /// │ └─────────────────────────┘                                │
    /// ├─────────────────────────────────────────────────────────────┤
    /// │ [保存设置] (横跨整个底部)                                    │
    /// └─────────────────────────────────────────────────────────────┘
    ///
    /// 【数据流转】
    /// 1. 窗体初始化时加载配方列表到左侧表格
    /// 2. 用户点击左侧表格某行，右侧显示对应配方的详细信息
    /// 3. 用户点击添加/更新/删除按钮，操作配方数据
    /// 4. 用户点击保存设置按钮，保存所有配方配置（持久化预留）
    ///
    /// 【配方字段】
    /// - 序号：行号（从1开始）
    /// - 配方名称：配方的名称标识
    /// - 延时时间：延时开启时间（时:分:秒）
    /// - 启动时间：延时到达时间（时:分:秒）
    /// - 极限温度：测试极限温度（单位：℃）
    ///
    /// 【预留说明】
    /// 1. 配方持久化未实现（当前仅在内存中维护）
    /// 2. 添加/更新配方的详细编辑功能预留（当前简化为提示信息）
    /// </summary>
    public partial class RecipeManagerForm : Form
    {
        /// <summary>
        /// 配方列表（内存中维护）
        /// 实际项目中应替换为持久化存储（数据库/文件）
        /// </summary>
        private readonly List<RecipeConfig> _recipes;

        /// <summary>
        /// 当前选中的配方
        /// </summary>
        private RecipeConfig _selectedRecipe;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="recipes">外部传入的配方列表，修改将反映到外部</param>
        /// <exception cref="System.ArgumentNullException">recipes 为 null 时抛出</exception>
        public RecipeManagerForm(List<RecipeConfig> recipes)
        {
            InitializeComponent();

            _recipes = recipes ?? throw new System.ArgumentNullException(nameof(recipes),
                "配方列表不能为 null，请传入外部维护的列表实例");

            LoadRecipesToGrid();

            if (_recipes.Count > 0)
            {
                dgvRecipes.Rows[0].Selected = true;
                UpdateRecipeSettings(_recipes[0]);
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
        /// 更新右侧配方设置区域的显示内容
        /// </summary>
        /// <param name="recipe">配方对象</param>
        private void UpdateRecipeSettings(RecipeConfig recipe)
        {
            if (recipe == null)
            {
                // 清空显示
                lblRecipeNameLabel.Text = string.Empty;
                lblRecipeNameValue.Text = string.Empty;
                lblDelayTimeValue.Text = string.Empty;
                lblStartTimeValue.Text = string.Empty;
                lblLimitTempValue.Text = string.Empty;
                _selectedRecipe = null;
                return;
            }

            _selectedRecipe = recipe;

            // 更新显示内容
            lblRecipeNameLabel.Text = recipe.Name;
            lblRecipeNameValue.Text = recipe.Name;

            // 延时时间：格式化为 时:分:秒
            lblDelayTimeValue.Text = FormatTimeSpan(recipe.DelayStartTime);

            // 启动时间：格式化为 时:分:秒
            lblStartTimeValue.Text = FormatTimeSpan(recipe.DelayArriveTime);

            // 极限温度：添加℃单位
            lblLimitTempValue.Text = $"{recipe.LimitTemperature}℃";
        }

        /// <summary>
        /// 将TimeSpan格式化为 时:分:秒 格式
        /// 去除前导零，如 01:20:30 → 1:20:30
        /// </summary>
        /// <param name="timeSpan">时间跨度</param>
        /// <returns>格式化后的时间字符串</returns>
        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            string hours = timeSpan.Hours.ToString();
            string minutes = timeSpan.Minutes.ToString("D2");
            string seconds = timeSpan.Seconds.ToString("D2");

            return $"{hours}:{minutes}:{seconds}";
        }

        /// <summary>
        /// 配方列表表格点击事件
        /// 选中某行后更新右侧配方设置区域
        /// </summary>
        private void dgvRecipes_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0 && e.RowIndex < _recipes.Count)
            {
                RecipeConfig recipe = _recipes[e.RowIndex];
                UpdateRecipeSettings(recipe);
            }
        }

        /// <summary>
        /// 添加按钮点击事件
        /// 弹出配方编辑窗体添加新配方
        /// </summary>
        private void btnAdd_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "添加配方功能预留。\n\n" +
                "后续实现：\n" +
                "1. 弹出配方编辑窗体\n" +
                "2. 用户填写配方参数（配方名称、延时时间、启动时间、极限温度）\n" +
                "3. 保存到配方列表",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 更新按钮点击事件
        /// 更新选中的配方
        /// </summary>
        private void btnUpdate_Click(object sender, EventArgs e)
        {
            if (_selectedRecipe == null)
            {
                MessageBox.Show("请先选择要更新的配方", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(
                $"更新配方功能预留。\n\n" +
                $"当前选中配方：{_selectedRecipe.Name}\n\n" +
                "后续实现：\n" +
                "1. 弹出配方编辑窗体加载当前配方数据\n" +
                "2. 用户修改配方参数\n" +
                "3. 保存更新",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 删除按钮点击事件
        /// 删除选中的配方
        /// </summary>
        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (_selectedRecipe == null)
            {
                MessageBox.Show("请先选择要删除的配方", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var result = MessageBox.Show(
                $"确定要删除配方 \"{_selectedRecipe.Name}\" 吗？",
                "确认删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                MessageBox.Show("删除功能预留，待持久化实现后补充",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// 保存设置按钮点击事件
        /// 保存所有配方配置（持久化预留）
        /// </summary>
        private void btnSaveSettings_Click(object sender, EventArgs e)
        {
            MessageBox.Show(
                "保存设置功能预留。\n\n" +
                "后续实现：\n" +
                "1. 将配方列表持久化到文件或数据库\n" +
                "2. 记录保存时间戳\n" +
                "3. 提示保存成功",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);

            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}