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
    /// - 查看配方列表（DataGridView表格显示）
    /// - 新增配方
    /// - 编辑已有配方
    /// - 删除配方
    ///
    /// 配方字段（基于 RecipeConfig 模型）：
    /// - 编号、名称、负压值、延时开启、延时到达、极限温度
    ///
    /// 【预留说明】
    /// 1. 配方持久化未实现（当前仅在内存中维护）
    /// 2. 配方导入/导出功能预留（可考虑Excel/JSON导入导出）
    /// 3. 实际配方字段需根据现场工艺要求确认是否需要扩展
    /// </summary>
    public partial class RecipeManagerForm : Form
    {
        /// <summary>
        /// 配方列表（内存中维护）
        /// 实际项目中应替换为持久化存储（数据库/文件）
        /// </summary>
        private readonly List<RecipeConfig> _recipes;

        /// <summary>
        /// 构造函数
        /// 修复 M12：参数校验，禁止传入 null
        /// 原实现 _recipes = recipes ?? new List<RecipeConfig>() 会在 null 时创建新列表
        /// 导致后续所有增删改操作只修改新列表，外部永远看不到，是隐藏 bug
        /// </summary>
        /// <param name="recipes">外部传入的配方列表，修改将反映到外部</param>
        /// <exception cref="System.ArgumentNullException">recipes 为 null 时抛出</exception>
        public RecipeManagerForm(List<RecipeConfig> recipes)
        {
            InitializeComponent();
            // 修复 M12：null 时抛出明确异常，而不是静默创建新列表
            _recipes = recipes ?? throw new System.ArgumentNullException(nameof(recipes),
                "配方列表不能为 null，请传入外部维护的列表实例");

            LoadRecipesToGrid();
        }

        /// <summary>
        /// 加载配方列表到DataGridView
        /// </summary>
        private void LoadRecipesToGrid()
        {
            dgvRecipes.Rows.Clear();

            foreach (var recipe in _recipes)
            {
                dgvRecipes.Rows.Add(
                    recipe.Id,
                    recipe.Name,
                    recipe.NegativePressure,
                    recipe.DelayStartTime.ToString(@"hh\:mm\:ss"),
                    recipe.DelayArriveTime.ToString(@"hh\:mm\:ss"),
                    recipe.LimitTemperature
                );
            }
        }

        /// <summary>
        /// 新增按钮点击事件
        /// 弹出配方编辑窗体（这里简化为输入对话框）
        /// </summary>
        private void btnAdd_Click(object sender, EventArgs e)
        {
            // 【预留】应弹出专门的配方编辑窗体进行详细配置
            // 当前简化为提示信息
            MessageBox.Show(
                "新增配方功能预留。\n\n" +
                "后续实现：\n" +
                "1. 弹出配方编辑窗体\n" +
                "2. 用户填写配方参数\n" +
                "3. 保存到配方列表（持久化）",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 编辑按钮点击事件
        /// </summary>
        private void btnEdit_Click(object sender, EventArgs e)
        {
            if (dgvRecipes.CurrentRow == null)
            {
                MessageBox.Show("请先选择要编辑的配方", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // 【预留】应弹出配方编辑窗体加载选中配方
            MessageBox.Show(
                "编辑配方功能预留。\n\n" +
                "后续实现：\n" +
                "1. 获取选中行的配方数据\n" +
                "2. 弹出配方编辑窗体加载数据\n" +
                "3. 用户修改后保存",
                "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// 删除按钮点击事件
        /// </summary>
        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (dgvRecipes.CurrentRow == null)
            {
                MessageBox.Show("请先选择要删除的配方", "提示",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var result = MessageBox.Show(
                "确定要删除选中的配方吗？",
                "确认删除",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result == DialogResult.Yes)
            {
                // 【预留】实际删除逻辑
                // TODO: 从 _recipes 中移除对应配方，并刷新表格
                MessageBox.Show("删除功能预留，待持久化实现后补充",
                    "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        /// <summary>
        /// 关闭按钮点击事件
        /// </summary>
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
