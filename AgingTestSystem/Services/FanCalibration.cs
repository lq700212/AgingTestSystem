using System;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 送风机定点校准的目标项（温度 / 湿度）。
    /// 【为什么要分项】厂商手册里温湿度的小数位本来就可能不同（常见 温度×0.01、湿度×0.1），
    /// 温湿度各存各的斜率（<see cref="Models.DeviceConfig.FanTempScale"/> /
    /// <see cref="Models.DeviceConfig.FanHumidityScale"/>），校准也必须一项一项来：
    /// 一次只校一项，互不牵连，也不会出现"两路推出的除数不一致没法落盘"的死结。
    /// </summary>
    public enum FanCalibrationTarget
    {
        /// <summary>温度（主界面长按温度值进；系统设置里改温度斜率）</summary>
        Temperature = 0,

        /// <summary>湿度（主界面不显示湿度，所以入口在系统设置 → 冷却送风机 → 校准湿度）</summary>
        Humidity = 1
    }

    /// <summary>
    /// 送风机定点（斜率）校准的纯函数计算。
    /// <para><b>为什么需要它</b>：不同型号的送风机控制屏，温度的小数位可能是 0.1（22.2°C）
    /// 也可能是 0.01（22.20°C），而上位机的换算比例要跟屏一致，错了就差 10 倍 / 100 倍。</para>
    /// <para><b>怎么校准</b>：控制屏上显示的温度是权威值。把"上位机当前读到的值"与
    /// "用户在控制屏上读到的真值"一比，就知道该换多大的比例：</para>
    /// <code>
    /// 比例 = 上位机当前显示值 ÷ 控制屏真值      （例：222.0 ÷ 22.20 = 10）
    /// 新除数 = 当前除数 × 比例                  （例：10 × 10 = 100）
    /// 新斜率 = 1 ÷ 新除数                       （例：1 ÷ 100 = 0.01）
    /// </code>
    /// <para>控制屏的小数位是仪表侧参数，协议里读不到"当前除数"字段，
    /// 所以只能这样反推（详见 <see cref="Services.FanControllerClient"/>）。</para>
    /// <para><b>为什么是纯函数</b>：换算判定（归一到 10 的幂、容差、越界）决定"校准成功还是失败"，
    /// 必须能单测，不能埋在窗体事件里。</para>
    /// </summary>
    public static class FanCalibration
    {
        /// <summary>定点除数下限（1 = 整数精度，最小可用档）</summary>
        public const int MinDivisor = 1;

        /// <summary>定点除数上限（10000 = 0.0001 精度，超出无实际意义）</summary>
        public const int MaxDivisor = 10000;

        /// <summary>
        /// 归一容差（相对偏差 5%）。
        /// 为什么留容差：温度是活读数，"上位机读到的瞬间"与"用户在控制屏上看的瞬间"天然差零点几度；
        /// 卡死 0% 容差会让现场明明读对了却被判失败。5% 覆盖约 1.1°C@22.2 的读数差，
        /// 又远小于"读错一位"造成的 10 倍/100 倍偏差（那两个分别偏 90%/99%）。
        /// 【唯一出处】与配置合法性判断共用一个容差（<see cref="Models.DeviceConfig.FanScaleMatchTolerance"/>）。
        /// </summary>
        public const double MatchTolerance = Models.DeviceConfig.FanScaleMatchTolerance;

        /// <summary>
        /// 由"上位机当前显示值 + 控制屏真值 + 当前除数"推算新的定点除数。
        /// </summary>
        /// <param name="displayedValue">上位机当前读到的值（已按当前除数换算，如 22.2 / 222.0）</param>
        /// <param name="screenValue">用户在送风机控制屏上读到的真值（如 22.20）</param>
        /// <param name="currentDivisor">当前使用的定点除数（非法时按缺省 <see cref="Models.DeviceConfig.DefaultFanScaleDivisor"/>）</param>
        /// <param name="newDivisor">推算出的新除数（成功时）</param>
        /// <param name="error">失败原因（成功时为 null），可直接展示给用户</param>
        /// <returns>true=推算成功</returns>
        public static bool TryComputeDivisor(double displayedValue, double screenValue,
            int currentDivisor, out int newDivisor, out string error)
        {
            newDivisor = 0;
            error = null;

            if (double.IsNaN(displayedValue) || double.IsInfinity(displayedValue) || displayedValue <= 0)
            {
                error = "上位机当前读数无效（送风机可能未连接），无法校准。";
                return false;
            }
            if (double.IsNaN(screenValue) || double.IsInfinity(screenValue) || screenValue <= 0)
            {
                error = "请输入送风机控制屏上读到的数值（须大于 0）。";
                return false;
            }

            int divisor = (currentDivisor >= MinDivisor && currentDivisor <= MaxDivisor)
                ? currentDivisor
                : Models.DeviceConfig.DefaultFanScaleDivisor;

            // 比例 = 上位机显示值 ÷ 控制屏真值（显示值偏大 10 倍 → 比例 10 → 除数放大 10 倍）
            double ratio = displayedValue / screenValue;
            double candidate = divisor * ratio;

            int normalized;
            string normalizeError;
            if (!TryNormalizeToPowerOfTen(candidate, out normalized, out normalizeError))
            {
                error = normalizeError;
                return false;
            }

            newDivisor = normalized;
            return true;
        }

        /// <summary>
        /// 把候选除数归一到最近的 10 的整数次幂（1 / 10 / 100 / 1000 / 10000）。
        /// 【为什么必须归一】定点除数只可能落在这些档位上（0.1 / 0.01 / 0.001 精度），
        /// 推算出来的 99.5 或 1004 都是读数误差造成的，归一后才落回合法档位。
        /// 偏差超过 <see cref="MatchTolerance"/>（如推算出 110）判失败——那是输入读错了，不是误差。
        /// </summary>
        /// <param name="candidate">候选除数（浮点）</param>
        /// <param name="divisor">归一后的除数（成功时）</param>
        /// <param name="error">失败原因（成功时为 null）</param>
        /// <returns>true=归一成功</returns>
        public static bool TryNormalizeToPowerOfTen(double candidate, out int divisor, out string error)
        {
            divisor = 0;
            error = null;

            if (double.IsNaN(candidate) || double.IsInfinity(candidate) || candidate <= 0)
            {
                error = "推算结果无效，请确认填入的是送风机控制屏上的数值。";
                return false;
            }

            int power = (int)Math.Round(Math.Log10(candidate));
            if (power < 0) power = 0;                       // 10^-1 = 0.1 低于下限，按 1 档再判容差
            double scaled = Math.Pow(10, power);
            if (scaled > MaxDivisor)
            {
                error = "推算出的定点比例超出可用范围（1 / 10 / 100 / 1000 / 10000），请确认输入值。";
                return false;
            }

            double deviation = Math.Abs(candidate / scaled - 1.0);
            if (deviation > MatchTolerance)
            {
                error = "推算出的定点比例（" + candidate.ToString("0.###") + "）不在标准档位" +
                        "（1 / 10 / 100 / 1000 / 10000）附近，请确认填入的是送风机控制屏上的数值。";
                return false;
            }

            divisor = (int)scaled;
            return true;
        }

        /// <summary>
        /// 按**斜率**口径算新斜率（配置存储与界面显示用的就是斜率）。
        /// 【为什么不直接拿斜率算】内部除法走整数除数（raw ÷ divisor），数值行为与旧实现逐位一致；
        /// 斜率与除数的换算只在这一层发生一次，避免两套算法算出两个答案。
        /// </summary>
        /// <param name="displayedValue">上位机当前读到的值（已按当前斜率换算，如 22.2 / 222.0）</param>
        /// <param name="screenValue">用户在送风机控制屏上读到的真值（如 22.20）</param>
        /// <param name="currentScale">当前使用的斜率（0.1 / 0.01 / 0.001 / 1）</param>
        /// <param name="newScale">推算出的新斜率（成功时）</param>
        /// <param name="error">失败原因（成功时为 null）</param>
        /// <returns>true=推算成功</returns>
        public static bool TryComputeScale(double displayedValue, double screenValue, double currentScale,
            out double newScale, out string error)
        {
            newScale = 0;
            int currentDivisor = Models.DeviceConfig.ScaleToDivisor(currentScale);
            int newDivisor;
            if (!TryComputeDivisor(displayedValue, screenValue, currentDivisor, out newDivisor, out error))
            {
                return false;
            }
            newScale = 1.0 / newDivisor;
            return true;
        }

        /// <summary>目标项的配置 key（App.config key / 内存属性名同名）</summary>
        /// <param name="target">校准目标项</param>
        /// <returns>配置 key</returns>
        public static string GetScaleKey(FanCalibrationTarget target)
        {
            return target == FanCalibrationTarget.Humidity ? "FanHumidityScale" : "FanTempScale";
        }

        /// <summary>目标项的中文名（界面文案）</summary>
        /// <param name="target">校准目标项</param>
        /// <returns>显示名</returns>
        public static string GetDisplayName(FanCalibrationTarget target)
        {
            return target == FanCalibrationTarget.Humidity ? "湿度" : "温度";
        }

        /// <summary>目标项的显示单位</summary>
        /// <param name="target">校准目标项</param>
        /// <returns>单位（°C / %RH）</returns>
        public static string GetUnit(FanCalibrationTarget target)
        {
            return target == FanCalibrationTarget.Humidity ? "%RH" : "°C";
        }

        /// <summary>取目标项当前的斜率（配置为 null 时回缺省）</summary>
        /// <param name="config">设备配置</param>
        /// <param name="target">校准目标项</param>
        /// <returns>当前斜率</returns>
        public static double GetScale(Models.DeviceConfig config, FanCalibrationTarget target)
        {
            if (config == null) return Models.DeviceConfig.DefaultFanScale;
            return target == FanCalibrationTarget.Humidity
                ? config.FanHumidityScale
                : config.FanTempScale;
        }

        /// <summary>取目标项对应的实时读数（温度或湿度）</summary>
        /// <param name="data">送风机数据（null 表示未连接）</param>
        /// <param name="target">校准目标项</param>
        /// <returns>读数</returns>
        public static double GetReading(Models.FanData data, FanCalibrationTarget target)
        {
            if (data == null) return 0;
            return target == FanCalibrationTarget.Humidity ? data.Humidity : data.Temperature;
        }
    }
}
