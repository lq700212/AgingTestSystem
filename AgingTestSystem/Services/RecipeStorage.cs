using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using AgingTestSystem.Models;
using Newtonsoft.Json;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 配方持久化服务
    ///
    /// 【功能说明】
    /// 将配方列表序列化为 JSON 文件（程序运行目录下的 Recipes.json），
    /// 供配方管理窗体保存设置时写入、主窗体启动时加载，
    /// 实现"配方修改后重启程序不丢失"。
    ///
    /// 【存储说明】
    /// - 文件路径：程序运行目录下的 Recipes.json（与 Users.json 同级）
    /// - 序列化整个 List&lt;RecipeConfig&gt;，包含每个配方的全部字段
    ///   （配方名称、延时时间、启动时间、极限温度、负压值、启用状态等）
    /// - 文件不存在时 Load 返回 null，由调用方使用空列表
    /// - 文件损坏或格式错误时 Load 返回 null（不抛异常）
    /// </summary>
    public static class RecipeStorage
    {
        /// <summary>
        /// 配方数据文件路径（程序运行目录下的 Recipes.json）
        /// </summary>
        private const string RecipeDataFilePath = "Recipes.json";

        /// <summary>
        /// 从 JSON 文件加载配方列表
        /// </summary>
        /// <returns>配方列表；文件不存在或加载失败返回 null</returns>
        public static List<RecipeConfig> Load()
        {
            try
            {
                if (!File.Exists(RecipeDataFilePath))
                {
                    System.Diagnostics.Debug.WriteLine("[配方存储] 配方数据文件不存在");
                    return null;
                }

                string jsonContent = File.ReadAllText(RecipeDataFilePath);
                List<RecipeConfig> recipes = JsonConvert.DeserializeObject<List<RecipeConfig>>(jsonContent);

                System.Diagnostics.Debug.WriteLine($"[配方存储] 配方数据加载成功，共 {recipes?.Count ?? 0} 个配方");
                return recipes ?? new List<RecipeConfig>();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[配方存储] 加载配方数据失败: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// 将配方列表保存到 JSON 文件
        /// </summary>
        /// <param name="recipes">要保存的配方列表</param>
        /// <returns>保存成功返回 true，失败返回 false</returns>
        public static bool Save(List<RecipeConfig> recipes)
        {
            try
            {
                string jsonContent = JsonConvert.SerializeObject(recipes, Formatting.Indented);
                File.WriteAllText(RecipeDataFilePath, jsonContent);

                System.Diagnostics.Debug.WriteLine($"[配方存储] 配方数据保存成功，共 {recipes?.Count ?? 0} 个配方");
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[配方存储] 保存配方数据失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 保存单个配方到配方列表并落盘（带同名覆盖询问）
        ///
        /// 【用途】批量设置配方窗体、工位设置窗体的"保存/加入队列"按钮共用：
        /// 把用户在界面上配置好的一个配方写入主窗体共享的配方列表并持久化到 Recipes.json，
        /// 使新配置的配方能在「参数设置 → 配方管理」列表中随时选用。
        ///
        /// 【同名处理】
        /// - 列表中没有同名配方 → 直接新增；
        /// - 列表中已有同名配方（忽略大小写）→ 弹窗询问"是否覆盖更新"：
        ///   确定 = 用新配方覆盖旧配方；取消 = 放弃保存。
        ///
        /// 【说明】
        /// - 传入的 recipes 是主窗体共享列表（<see cref="Views.MainForm._recipes"/>），
        ///   本方法会直接修改它，因此配方管理窗口打开后即可看到新配置的配方。
        /// - 覆盖更新时保留原配方的 Id（创建时间用当前时间刷新），避免编号漂移。
        /// </summary>
        /// <param name="recipes">外部共享的配方列表（会被直接修改）</param>
        /// <param name="recipe">要保存的配方</param>
        /// <returns>true=已保存（新增或覆盖成功）；false=用户取消覆盖 / 保存失败 / 参数非法</returns>
        public static bool SaveWithDuplicateCheck(List<RecipeConfig> recipes, RecipeConfig recipe)
        {
            if (recipes == null || recipe == null) return false;

            // 忽略大小写查找同名配方
            int index = recipes.FindIndex(r =>
                string.Equals(r.Name, recipe.Name, StringComparison.OrdinalIgnoreCase));

            if (index >= 0)
            {
                // 同名 → 询问是否覆盖更新
                var result = MessageBox.Show(
                    $"已存在配方 \"{recipe.Name}\"，是否覆盖更新该配方？",
                    "配方已存在",
                    MessageBoxButtons.OKCancel,
                    MessageBoxIcon.Question);

                // 取消 → 放弃保存（不新增、不覆盖）
                if (result != DialogResult.OK) return false;

                // 覆盖：保留原 Id，其余字段用新配方内容刷新
                RecipeConfig existing = recipes[index];
                recipe.Id = existing.Id;
                recipe.CreateTime = DateTime.Now;
                recipes[index] = recipe;
            }
            else
            {
                // 新增：分配一个不冲突的编号后加入列表
                recipe.Id = recipes.Count + 1;
                recipes.Add(recipe);
            }

            // 整个列表落盘
            return Save(recipes);
        }
    }
}
