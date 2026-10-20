using System;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 送风机定点（除数）校准的纯函数计算。
    /// <para><b>为什么需要它</b>：不同型号的送风机控制屏，温湿度的小数位可能是 0.1（22.2°C）
    /// 也可能是 0.01（22.20°C），而上位机只有一份换算除数（<see cref="Models.DeviceConfig.FanScaleDivisor"/>）。
    /// 除数选错，温度就差 10 倍 / 100 倍。</para>
    /// <para><b>怎么校准</b>：控制屏上显示的温度是权威值。把"上位机当前读到的值"与
    /// "用户在控制屏上读到的真值"一比，就知道该换多大的除数：</para>
    /// <code>
    /// 比例 = 上位机当前显示值 ÷ 控制屏真值      （例：222.0 ÷ 22.20 = 10）
    /// 新除数 = 当前除数 × 比例                  （例：10 × 10 = 100）
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
        /// </summary>
        public const double MatchTolerance = 0.05;

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
    }
}
