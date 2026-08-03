# CHANGELOG

## [2026-08-03] 接入真实通讯链路（Modbus RTU + Modbus TCP）并实现报警联动

### 改动范围
- [ModbusRtuBarometerReader.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/ModbusRtuBarometerReader.cs) — 新增气压表 Modbus RTU 读取实现（RS485→USB）
- [ModbusTcpIoController.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/ModbusTcpIoController.cs) — 新增 IO Modbus TCP 读写实现（GX-CL140 方式）
- [DeviceManager.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/DeviceManager.cs) — 增加 Mock/真实通讯切换、合并 IO 状态、报警边沿触发输出联动
- [DeviceConfig.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/DeviceConfig.cs) / [App.config](file:///e:/Project/BarometerWinform/BarometerWinform/App.config) / [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs) — 新增并加载通讯/阈值相关配置项
- [CommonParameterForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Dialogs/CommonParameterForm.cs) — 报警阈值写回内存配置（仍未做持久化）
- [BarometerWinform.csproj](file:///e:/Project/BarometerWinform/BarometerWinform/BarometerWinform.csproj) — 移除已合并的串口通讯 Demo 编译项

### 为什么这么改
- 现场目标是“气压阈值报警 → 输出控制(关阀/断电)”，必须把串口 RTU 与 TCP IO 打通到主程序主链路，而不是停留在测试 Demo。
- 原有 DeviceManager 固定使用 Mock，导致即使配置了串口/TCP 参数也无法进入真实通讯路径。

### 优化点
- 通过 `UseMockCommunication` 开关实现“无设备可跑 Mock，有设备可切真实”的开发/交付模式。
- IO 采用批量读（ReadAllInputs/ReadAllOutputs）并回填到每个 BarometerData，UI 面板能显示真实 IO 状态。
- 报警采用“边沿触发”避免持续报警时每周期重复下发 DO。

### 踩坑记录
- 气压表寄存器单位/缩放、IO 寄存器位序/是否取反等信息必须以现场说明书为准，已在代码中用 TODO 留口待通线验证。

