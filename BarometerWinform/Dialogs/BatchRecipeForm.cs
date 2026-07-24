using System;
using System.Collections.Generic;
using System.Windows.Forms;
using BarometerWinform.Models;

namespace BarometerWinform.Dialogs
{
    /// <summary>
    /// 批量设置配方窗口（业务逻辑部分）
    ///
    /// 【功能说明】
    /// 本窗口用于批量设置配方参数，支持用户输入配方名称、延时时间、启动时间、极限温度等参数，
    /// 点击"加入队列"按钮将当前配置加入配方队列，后续可批量应用到多个选中的气压表面板。
    ///
    /// 【界面布局】
    /// ┌─────────────────────────────────────────────┐
    /// │ 批量设置设置配方窗口                         │  ← 标题栏
    /// ├─────────────────────────────────────────────┤
    /// │ 配方名称：[____________]                    │  ← 配方名称输入框
    /// │ 延时时间1：[__]:[__]:[__]                   │  ← 延时时间1（时:分:秒）
    /// │ 延时时间2：[__]:[__]:[__]                   │  ← 延时时间2（时:分:秒）
    /// │ 启动时间：[__]:[__]:[__]                    │  ← 启动时间（时:分:秒）
    /// │ 极限温度：[____] °C                         │  ← 极限温度输入框
    /// ├─────────────────────────────────────────────┤
    /// │         [加入队列]                          │  ← 加入队列按钮
    /// │         [关闭窗口]                          │  ← 关闭窗口按钮
    /// └─────────────────────────────────────────────┘
    ///
    /// 【数据流转】
    /// 1. 用户在窗口中填写各项配方参数
    /// 2. 点击"加入队列"按钮，参数验证通过后创建 RecipeConfig 对象
    /// 3. RecipeConfig 对象加入配方队列列表（_recipeQueue）
    /// 4. 触发 OnRecipeAdded 事件，通知主窗体有新配方加入队列
    /// 5. 用户可继续添加更多配方到队列，或点击"关闭窗口"退出
    ///
    /// 【参数说明】
    /// - 配方名称：测试配方的名称标识，用于区分不同配方（如 ABCDEFGH）
    /// - 延时时间1：第一个延时阶段的时长（格式：时:分:秒，对应 RecipeConfig.DelayStartTime）
    /// - 延时时间2：第二个延时阶段的时长（格式：时:分:秒，对应 RecipeConfig.DelayArriveTime）
    /// - 启动时间：测试启动前的等待时长（格式：时:分:秒，暂存于额外字段）
    /// - 极限温度：测试过程中的温度上限值（单位：摄氏度，对应 RecipeConfig.LimitTemperature）
    ///
    /// 【注意事项】
    /// 1. 所有时间输入框限制为2位数字，温度输入框限制为3位数字
    /// 2. 输入验证：小时0-23，分钟0-59，秒0-59，温度0-999
    /// 3. 配方名称不能为空
    /// </summary>
    public partial class BatchRecipeForm : Form
    {
        /// <summary>
        /// 配方队列列表
        /// 存储用户通过本窗口添加的所有配方配置
        /// 实际项目中可扩展为持久化存储（如数据库、JSON文件）
        /// </summary>
        private readonly List<RecipeConfig> _recipeQueue;

        /// <summary>
        /// 配方加入队列事件
        /// 当用户点击"加入队列"按钮并成功添加配方时触发
        /// 参数为新添加的 RecipeConfig 对象，便于主窗体获取配方信息
        /// </summary>
        public event EventHandler<RecipeConfig> OnRecipeAdded;

        /// <summary>
        /// 构造函数
        /// 初始化窗口控件和配方队列
        /// </summary>
        public BatchRecipeForm()
        {
            InitializeComponent();
            _recipeQueue = new List<RecipeConfig>();
        }

        /// <summary>
        /// 获取当前配方队列
        /// 用于主窗体获取用户添加的所有配方
        /// </summary>
        /// <returns>配方队列列表的副本（避免外部直接修改内部状态）</returns>
        public List<RecipeConfig> GetRecipeQueue()
        {
            return new List<RecipeConfig>(_recipeQueue);
        }

        /// <summary>
        /// 清空配方队列
        /// 用于主窗体在批量应用配方后清空队列
        /// </summary>
        public void ClearRecipeQueue()
        {
            _recipeQueue.Clear();
        }

        /// <summary>
        /// 获取当前窗口输入的配方配置
        /// 从各个输入控件中读取值，创建并返回 RecipeConfig 对象
        /// </summary>
        /// <returns>配方配置对象，如果验证失败返回 null</returns>
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

            // 解析延时时间1（时:分:秒）
            TimeSpan delayTime1;
            if (!TryParseTimeSpan(txtDelay1Hour.Text, txtDelay1Minute.Text, txtDelay1Second.Text, out delayTime1))
            {
                MessageBox.Show("延时时间1输入无效，请输入有效的时:分:秒", "输入验证",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            // 解析延时时间2（时:分:秒）
            TimeSpan delayTime2;
            if (!TryParseTimeSpan(txtDelay2Hour.Text, txtDelay2Minute.Text, txtDelay2Second.Text, out delayTime2))
            {
                MessageBox.Show("延时时间2输入无效，请输入有效的时:分:秒", "输入验证",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

            // 解析启动时间（时:分:秒）
            TimeSpan startTime;
            if (!TryParseTimeSpan(txtStartHour.Text, txtStartMinute.Text, txtStartSecond.Text, out startTime))
            {
                MessageBox.Show("启动时间输入无效，请输入有效的时:分:秒", "输入验证",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return null;
            }

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
            // 注意：根据需求，启动时间暂未映射到 RecipeConfig 的现有字段
            // 实际项目中可根据业务需求扩展 RecipeConfig 模型添加启动时间字段
            return new RecipeConfig
            {
                Id = _recipeQueue.Count + 1,
                Name = recipeName,
                DelayStartTime = delayTime1,
                DelayArriveTime = delayTime2,
                LimitTemperature = limitTemp,
                CreateTime = DateTime.Now,
                IsEnabled = true
            };
        }

        /// <summary>
        /// 尝试解析时间输入为 TimeSpan
        /// 验证小时、分钟、秒的有效性
        /// </summary>
        /// <param name="hourText">小时输入文本</param>
        /// <param name="minuteText">分钟输入文本</param>
        /// <param name="secondText">秒输入文本</param>
        /// <param name="timeSpan">解析成功后的 TimeSpan 对象</param>
        /// <returns>解析是否成功</returns>
        private bool TryParseTimeSpan(string hourText, string minuteText, string secondText, out TimeSpan timeSpan)
        {
            timeSpan = TimeSpan.Zero;

            // 解析小时（0-23）
            if (!int.TryParse(hourText.Trim(), out int hour) || hour < 0 || hour > 23)
            {
                return false;
            }

            // 解析分钟（0-59）
            if (!int.TryParse(minuteText.Trim(), out int minute) || minute < 0 || minute > 59)
            {
                return false;
            }

            // 解析秒（0-59）
            if (!int.TryParse(secondText.Trim(), out int second) || second < 0 || second > 59)
            {
                return false;
            }

            // 创建 TimeSpan 对象
            timeSpan = new TimeSpan(hour, minute, second);
            return true;
        }

        /// <summary>
        /// 加入队列按钮点击事件
        /// 获取当前输入的配方配置，验证通过后加入队列
        /// </summary>
        private void btnAddToQueue_Click(object sender, EventArgs e)
        {
            // 获取当前配方配置
            RecipeConfig recipe = GetCurrentRecipeConfig();
            if (recipe == null)
            {
                return;
            }

            // 加入配方队列
            _recipeQueue.Add(recipe);

            // 写入日志（输出到控制台，实际项目中可写入日志文件）
            System.Diagnostics.Debug.WriteLine(
                $"[批量设置配方] 配方已加入队列: {recipe.Name}（延时1: {recipe.DelayStartTime}, 延时2: {recipe.DelayArriveTime}, 极限温度: {recipe.LimitTemperature}°C）");

            // 触发配方加入事件，通知主窗体
            OnRecipeAdded?.Invoke(this, recipe);

            // 弹出成功提示
            MessageBox.Show(
                $"配方 \"{recipe.Name}\" 已成功加入队列！\n\n" +
                $"队列当前配方数量: {_recipeQueue.Count}",
                "加入队列成功",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }

        /// <summary>
        /// 关闭窗口按钮点击事件
        /// 关闭窗口并返回 DialogResult.OK
        /// </summary>
        private void btnClose_Click(object sender, EventArgs e)
        {
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}