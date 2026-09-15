
using System;

namespace AgingTestSystem.Models
{
    /// <summary>
    /// 配方配置模型
    /// 用于存储测试配方的参数配置
    /// 每个配方包含测试所需的各项参数
    /// </summary>
    public class RecipeConfig
    {
        /// <summary>
        /// 配方编号
        /// </summary>
        public int Id { get; set; }

        /// <summary>
        /// 配方名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 负压值设定（单位：kPa，与气压表读数一致）
        /// </summary>
        public decimal NegativePressure { get; set; }

        /// <summary>
        /// 显示模式（烧屏画面记录，自由文本如"白场/RGB循环/棋盘格/视频"）。
        /// 【为什么只是记录不参与判定】画面由治具驱动板/外部 PG 产生，老化架只供电计时，
        /// 软件侧发不出画面。但"这次烧的什么画面"必须进配方才能追溯（LED 行业分
        /// "白平衡老化+视频老化"两段，烧错画面=整批数据不可比）。等现场确认画面方案后，
        /// 再做工位透传/PG 控制（见问题清单改造池 P2-❼）；此前空字符串=没填，不影响任何逻辑。
        /// </summary>
        public string DisplayMode { get; set; }

        /// <summary>
        /// 延时时间（时:分:秒，上电前等待：点启动后先只开真空阀，等够这么久才上电）
        /// </summary>
        public TimeSpan DelayTime { get; set; }

        /// <summary>
        /// 烧屏时间（时:分:秒，上电后老化时长：上电开始计时，跑够这么久自动完成）
        /// </summary>
        public TimeSpan BurnInTime { get; set; }

        /// <summary>
        /// 极限温度（单位：摄氏度）
        /// </summary>
        public decimal LimitTemperature { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 深拷贝（【大扫荡】编辑窗先改副本：校验失败时原对象不受污染；
        /// 以前就地改，校验一半失败前半字段已脏）。
        /// </summary>
        public RecipeConfig Clone()
        {
            return new RecipeConfig
            {
                Id = this.Id,
                Name = this.Name,
                NegativePressure = this.NegativePressure,
                DisplayMode = this.DisplayMode,
                DelayTime = this.DelayTime,
                BurnInTime = this.BurnInTime,
                LimitTemperature = this.LimitTemperature,
                CreateTime = this.CreateTime,
                IsEnabled = this.IsEnabled
            };
        }
    }
}
