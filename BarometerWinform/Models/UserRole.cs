namespace BarometerWinform.Models
{
    /// <summary>
    /// 用户角色枚举
    ///
    /// 【说明】
    /// 定义系统中三种用户角色（权限等级由低到高）：
    /// - 操作员（Operator）：基础权限，只能查看数据和执行常规操作
    /// - 技术员（Technician）：中等权限，可操作通讯设置和配方参数
    /// - 管理员（Administrator）：最高权限，可修改其他用户的用户名和密码
    ///
    /// 【用途】
    /// 1. 用于登录时标识当前用户身份
    /// 2. 用于按钮权限控制（如通讯设置、配方参数按钮仅技术员/管理员可操作）
    /// </summary>
    public enum UserRole
    {
        /// <summary>操作员：基础权限（默认登录状态）</summary>
        Operator = 0,

        /// <summary>技术员：中等权限（可操作通讯设置和配方参数）</summary>
        Technician = 1,

        /// <summary>管理员：最高权限（可管理其他用户账号）</summary>
        Administrator = 2
    }
}
