
using System;
using AgingTestSystem.Interfaces;
using AgingTestSystem.Models;

namespace AgingTestSystem.Services
{
    /// <summary>
    /// 载台电流表真实驱动桩（Q2 通用骨架的真实端占位）。
    /// 【为什么是个桩】电表还没选型（串口表还是网口表、什么协议、几个回路全未知），
    /// 没有协议写不出驱动。本桩的作用是"占住真实端的位置"，让 DeviceManager 的
    /// Mock/真实二选一接线现在就能写完、回归现在就能跑：
    /// - UseMockCommunication=true → MockPowerMeter（有数）；
    /// - UseMockCommunication=false → 本桩（连不上、读 null，但绝不抛异常拖垮采集）。
    /// 【电表到货后怎么做】把本桩改成真驱动（参考 FanControllerClient 的写法）：
    /// Connect 里建连接、ReadAllCurrents 里按 deviceId 读电流填数组，
    /// 失败一路填 NaN（上层跳过，不断追溯链）。上层业务零改动。
    /// </summary>
    public class PowerMeterClient : IPowerMeter
    {
        /// <summary>连接状态（桩：永远 false，电表没到）</summary>
        private bool _isConnected;

        public bool IsConnected => _isConnected;

        public event EventHandler<string> OnError;

        public bool Connect(DeviceConfig config)
        {
            // 桩：直接失败并说明原因（不 sleep、不建连接，失败要快，防拖慢启动）
            _isConnected = false;
            try
            {
                OnError?.Invoke(this,
                    "载台电表尚未选型：PowerMeterClient 还是占位桩。请先确定电表型号与通讯协议，再实现真实驱动。");
            }
            catch { /* 事件订阅方异常不能反噬连接流程 */ }
            return false;
        }

        public void Disconnect()
        {
            _isConnected = false;
        }

        public bool ReconnectNow()
        {
            if (_isConnected) return true;
            // 桩没有可重连的设备：直接返回 false（不刷屏，Connect 里的提示只在显式连接时发一次）
            return false;
        }

        public float[] ReadAllCurrents(int deviceCount)
        {
            // 桩：没连上就没有数，返回 null（= 读失败，与接口注释、MockPowerMeter 断开语义、
            // DeviceManager"读失败填 NaN 追溯不断"三方一致；上层 null 安全，永不抛）。
            if (deviceCount <= 0) return new float[0];
            if (!_isConnected) return null;
            var result = new float[deviceCount];
            for (int i = 0; i < deviceCount; i++) result[i] = float.NaN;
            return result;
        }

        public void Dispose()
        {
            _isConnected = false;
        }
    }
}
