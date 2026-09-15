using System;
using System.Collections.Generic;
using System.IO;
using AgingTestSystem.Models;
using Newtonsoft.Json;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 配方持久化服务
    /// 【功能说明】
    /// 将配方列表序列化为 JSON 文件（程序运行目录下的 Recipes.json），
    /// 供配方管理窗体保存设置时写入、主窗体启动时加载，
    /// 实现"配方修改后重启程序不丢失"。
    /// 【存储说明】
    /// - 文件路径：程序运行目录下的 Recipes.json（与 Users.json 同级）
    /// - 序列化整个 List&lt;RecipeConfig&gt;，包含每个配方的全部字段
    ///   （配方名称、延时时间、烧屏时间、极限温度、负压值、启用状态等）
    /// - 文件不存在时 Load 返回 null，由调用方使用空列表
    /// - 文件损坏或格式错误时 Load 返回 null（不抛异常）
    /// </summary>
    public static class RecipeStorage
    {
        /// <summary>
        /// 配方数据文件路径（跟项目走：Projects/&lt;当前项目&gt;/Recipes.json，
        /// 经 ProjectProfile 解析；切项目即换配方文件）。
        /// </summary>
        private static string RecipeDataFilePath
        {
            get { return ProjectProfile.ResolveDataPath("Recipes.json", true); }
        }

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

                string jsonContent = AtomicFile.SafeReadAllText(RecipeDataFilePath);
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
        /// 将配方列表保存到 JSON 文件（【大扫荡】原子写，无写半截截断）。
        /// </summary>
        /// <param name="recipes">要保存的配方列表</param>
        /// <returns>保存成功返回 true，失败返回 false</returns>
        public static bool Save(List<RecipeConfig> recipes)
        {
            try
            {
                string jsonContent = JsonConvert.SerializeObject(recipes, Formatting.Indented);
                AtomicFile.WriteAllText(RecipeDataFilePath, jsonContent);

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
        /// 按名找同名配方下标（忽略大小写；空名返回 -1）。
        /// 【大扫荡】覆盖确认框搬到调用方 UI：Services 层弹 MessageBox 会卡死后台线程，
        /// 且单测一碰就挂起等点确认。本方法给 UI 做"问不问"判断用。
        /// </summary>
        public static int FindDuplicateIndex(List<RecipeConfig> recipes, string name)
        {
            if (recipes == null || string.IsNullOrWhiteSpace(name)) return -1;
            return recipes.FindIndex(r =>
                r != null && string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// 保存单个配方到配方列表并落盘（无 UI：同名时按 overwrite 决定覆盖/拒绝）。
        /// 【用途】批量设置配方窗体、工位设置窗体的"保存/加入队列"按钮共用：
        /// 调用方先用 <see cref="FindDuplicateIndex"/> 查同名、同名时自己弹窗问，
        /// 用户确认覆盖才传 overwrite=true（取消=直接返回，不调本方法）。
        /// 【同名处理】
        /// - 无同名 → 新增（Id=Max+1，防删除塌号撞号）；
        /// - 有同名 + overwrite=true → 覆盖（保留原 Id，CreateTime 刷新）；
        /// - 有同名 + overwrite=false → 拒绝（列表不动，返回 false）。
        /// 【失败回滚】先改内存再落盘，落盘失败则内存恢复原样（以前 Id 改了回不来，
        /// 列表与文件分叉）。空名配方直接拒绝（以前 null 名互判"同名"）。
        /// </summary>
        /// <param name="recipes">外部共享的配方列表（成功时才被修改）</param>
        /// <param name="recipe">要保存的配方</param>
        /// <param name="overwrite">同名时是否覆盖</param>
        /// <returns>true=已保存（新增或覆盖成功）；false=拒绝覆盖 / 保存失败 / 参数非法</returns>
        public static bool SaveRecipe(List<RecipeConfig> recipes, RecipeConfig recipe, bool overwrite)
        {
            if (recipes == null || recipe == null) return false;
            if (string.IsNullOrWhiteSpace(recipe.Name)) return false;

            int index = FindDuplicateIndex(recipes, recipe.Name);
            if (index >= 0 && !overwrite) return false;

            // 先备份，落盘失败回滚（内存与文件永不分叉）
            RecipeConfig backup = null;
            bool added = false;
            if (index >= 0)
            {
                // 覆盖：保留原 Id，其余字段用新配方内容刷新
                backup = recipes[index];
                recipe.Id = backup.Id;
                recipe.CreateTime = DateTime.Now;
                recipes[index] = recipe;
            }
            else
            {
                // 新增：分配一个不冲突的编号后加入列表
                // 用 Max(Id)+1 而不是 Count+1：删除中间配方后 Count 会"塌"，
                // 如剩 {Id=2} 时 Count+1 又得 2 造成撞号；Max+1 永不回退。
                int nextId = 1;
                foreach (RecipeConfig r in recipes)
                {
                    if (r != null && r.Id >= nextId) nextId = r.Id + 1;
                }
                recipe.Id = nextId;
                recipes.Add(recipe);
                added = true;
            }

            if (Save(recipes)) return true;

            // 落盘失败：内存回滚
            if (added) recipes.Remove(recipe);
            else recipes[index] = backup;
            return false;
        }
    }
}
