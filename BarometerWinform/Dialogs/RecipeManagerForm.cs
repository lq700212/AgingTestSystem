using System;
using System.Collections.Generic;
using System.Windows.Forms;
using BarometerWinform.Models;
using BarometerWinform.Services;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 配方管理窗体（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 管理老化测试配方，包括：
    /// - 查看配方列表（左侧DataGridView表格，只显示序号和配方名称）
    /// - 选中配方后右侧显示该配方的设置内容，并可编辑
    ///   （配方名称、延时时间、启动时间、极限温度）
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
    /// │ │ │ 2     │ BBVJKNVK    │ │  │ │ 启动时间：[ ][ ][ ] │ │   │
    /// │ │ │ 3     │ RFTYHYJWF   │ │  │ │ 极限温度：[____]℃   │ │   │
    /// │ │ │ 4     │ WFRWGYJUK   │ │  │ ├─────────────────────┤ │   │
    /// │ │ │ 5     │ FGYJKIewF   │ │  │ │ [添加] [更新] [删除] │ │   │
    /// │ │ │(带滚动条)           │ │  │ └─────────────────────┘ │   │
    /// │ │ └─────────────────────┘ │  └─────────────────────────┘   │
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
    /// - 延时时间：延时开启时间（时:分:秒）
    /// - 启动时间：延时到达时间（时:分:秒）
    /// - 极限温度：测试极限温度（单位：℃）
    ///
    /// 【持久化】
    /// 添加/更新/删除每次操作成功后自动通过 <see cref="RecipeStorage"/> 把整个配方列表
    /// 写入程序运行目录下的 Recipes.json；主窗体启动时加载该文件（见 MainForm.LoadRecipes）。
    /// </summary>
    public partial class RecipeManagerForm : Form
    {
        /// <summary>
        /// 配方列表（由外部传入，修改会反映到外部列表）
        /// </summary>
        private readonly List<RecipeConfig> _recipes;

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
                nudStartHours.Value = nudStartHours.Minimum;
                nudStartMinutes.Value = nudStartMinutes.Minimum;
                nudStartSeconds.Value = nudStartSeconds.Minimum;
                nudLimitTemp.Value = nudLimitTemp.Minimum;
                return;
            }

            txtRecipeName.Text = recipe.Name;

            // 延时时间 → 时/分/秒
            SetTimeInputs(nudDelayHours, nudDelayMinutes, nudDelaySeconds, recipe.DelayTime);

            // 启动时间 → 时/分/秒
            SetTimeInputs(nudStartHours, nudStartMinutes, nudStartSeconds, recipe.StartTime);

            // 极限温度（超出 NumericUpDown 范围时钳制到边界）
            nudLimitTemp.Value = Math.Max(nudLimitTemp.Minimum,
                Math.Min(nudLimitTemp.Maximum, recipe.LimitTemperature));
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
            recipe.StartTime = new TimeSpan(
                (int)nudStartHours.Value, (int)nudStartMinutes.Value, (int)nudStartSeconds.Value);
            recipe.LimitTemperature = nudLimitTemp.Value;
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
                    $"已存在配方 \"{_recipes[existingIndex].Name}\"，是否更新该配方？",
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
                MessageBox.Show("请输入或选择要更新的配方名称", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txtRecipeName.Focus();
                return;
            }

            int index = FindRecipeIndex(name);
            if (index < 0)
            {
                MessageBox.Show($"列表中不存在配方 \"{name}\"，请点击\"添加\"新增配方", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            UpdateRecipeAt(index);
        }

        /// <summary>
        /// 用当前输入框内容更新列表中第 index 个配方
        /// （btnAdd 重名确定 与 btnUpdate 共用此逻辑）
        /// </summary>
        /// <param name="index">配方在列表中的索引</param>
        private void UpdateRecipeAt(int index)
        {
            if (index < 0 || index >= _recipes.Count) return;

            RecipeConfig target = _recipes[index];
            if (!TryApplyInputToRecipe(target))
            {
                return;
            }

            PersistRecipes();
            LoadRecipesToGrid();
            SelectRecipeRow(index);
            MessageBox.Show($"配方 \"{target.Name}\" 已更新", "提示",
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
