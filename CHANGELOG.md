# CHANGELOG

## [2026-08-06] V1.15 业务串联 + 冷却送风机接入（老化测试业务流程闭环）

### 改动范围
- **新增** [FanData.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/FanData.cs) — 送风机数据模型 + 运行状态枚举
- **新增** [IFanController.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Interfaces/IFanController.cs) — 送风机控制接口（定值启动/定值停止/读状态）
- **新增** [FanControllerClient.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/FanControllerClient.cs) — 送风机 Modbus TCP 实现（同步版，移植自 ModbusTCPFanControllerTest Demo，带锁 + 断线重连节流 + 连接超时）
- **新增** [MockFanController.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/MockFanController.cs) — 送风机 Mock（无设备可演示）
- **新增** [TestEventLogger.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/TestEventLogger.cs) — 测试事件 CSV 落盘（Logs\TestLog_yyyyMMdd.csv）
- **新增** [DeviceManualForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Dialogs/DeviceManualForm.cs) — 单台手动控制对话框（面板 Set 按钮打开，点动阀/载台电 + 实时 DI）
- [DeviceManager.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/DeviceManager.cs) — 送风机接入（独立定时器轮询，不阻塞 72 台采集）、送风机全局生命周期（首台启动/末台停止）、测试状态机（StartTesting/StopTesting/ResetDevices/StopAll）、真空建立确认、通讯失联报警、老化计时自动停止、事件落盘
- [DeviceConfig.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/DeviceConfig.cs) / [App.config](file:///e:/Project/BarometerWinform/BarometerWinform/App.config) / [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs) — 新增送风机配置（FanEnabled/FanIpAddress/FanPort/FanUnitId/FanTimeoutMs）与业务参数（VacuumConfirmTimeoutMs/CommunicationLossAlarmCount/MaxTestDurationSeconds/UseDiAlarmContact/FanTempAlarmLimitC）
- [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs) / [MainForm.Designer.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.Designer.cs) — 右侧面板改造：送风机监视区（状态/温度/湿度/设定值）、送风机定值启动/停止、启动运行/停止运行/报警复位/全部停止（急停）按钮全部接真实业务、状态栏新增"测试中/在线"统计
- [MockBarometerReader.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/MockBarometerReader.cs) — 模拟压力偏向"真空良好"，状态改由 DeviceManager 统一驱动
- [HistoryRecordForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Dialogs/HistoryRecordForm.cs) — 历史记录由 Mock 数据改为读取真实 CSV 日志文件

### 为什么这么改
- 需求：72 台气压表 + GX-CL140 IO + 冷却送风机（厂商控制屏，只做定值启动/停止）三条链路要在上位机里串成完整的老化测试业务。
- 设计评审（对抗性审查）修正了 4 个关键点：① 送风机是 72 台共享的环境设备，必须"首台启动时定值启动、最后一台停止时才停止"，不能随单台停止而停机；② 开阀后必须等真空建立（真空确认宽限窗口），否则刚开阀的常压会被误判报警；③ 气压表通讯失联必须报警（防止旧值假正常）；④ 报警后需人工复位。

### 优化点
- 送风机用独立定时器轮询（2s），通讯问题不拖累 72 台气压表采集与报警判定。
- 报警/启动/停止/复位/急停全部写入 CSV 日志，历史记录窗体可直接查询，支持追溯。
- 新增"全部停止（急停）"一键兜底：全关阀 + 全断电 + 停送风机。

### 待现场确认项
- 送风机实际 IP/端口（Demo 实测 192.168.1.221:50000，以现场为准）。
- UseDiAlarmContact（DI 报警触点并入判定）默认关闭，需现场确认触点电平后开启。
- 电磁阀阀型（2 位 3 通带排气 / 2 位 2 通）影响停机后泄压，现场确认。



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

## [2026-08-03] 适配 GX-CL140 80DI/160DO 模块容量

### 改动范围
- [DeviceConfig.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/DeviceConfig.cs) / [App.config](file:///e:/Project/BarometerWinform/BarometerWinform/App.config) / [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs) — 默认 IP 改为 192.168.1.20:502，默认 IO 通道数改为 80DI/160DO，并增加 InvertInputs/InvertOutputs 配置读取与一致性校验
- [ModbusTcpIoController.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/ModbusTcpIoController.cs) — 增加输入/输出逻辑取反配置支持（适配 NPN 低有效等现场差异）
- [IoMapBuilder.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/IoMapBuilder.cs) / [BarometerPanelView.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/BarometerPanelView.cs) — 当 TotalInputs != TotalBarometers 时，仍能正确计算输出点内部编号，并扩展“预留点”映射
- [通讯接入说明.md](file:///e:/Project/BarometerWinform/通讯接入说明.md) — 增补 80DI/160DO 与显耀IO表(72DI/144DO) 的关系说明

### 为什么这么改
- 现场 GX-CL140 实际接入的 IO 模块容量为 80DI/160DO，而显耀IO表仅定义了 72DI/144DO；程序需要既能按业务正确联动，也能容纳现场多出的预留通道，避免内部编号计算错位。

## [2026-08-03] 固化 GX-CL140 IO 寄存器位序（现场实测）

### 改动范围
- [ModbusTcpIoController.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Services/ModbusTcpIoController.cs) — 移除“位序/寄存器类型待确认”的 TODO，补充现场确认后的地址段与 bit 位序说明
- [通讯接入说明.md](file:///e:/Project/BarometerWinform/通讯接入说明.md) — 补充 0x1000/0x2000 对应通道范围（80DI/160DO）的表格化说明

### 为什么这么改
- 你在现场已通过 ModbusTCPTest 对 GX-CL140 + DQ50P-S 输出模块做了循环写入测试，确认了：
  - DO 起始 0x2000，每寄存器 16 路，bit0=第1路
  - 5 个 DQ50P-S（160DO）对应 0x2000~0x2009 共 10 个寄存器
  - DI 起始 0x1000，读取 Input Register（0x04），bit0=第1路

## [2026-08-03] 主视图面板布局调整为 8 列 × 9 行

### 改动范围
- [DeviceConfig.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Models/DeviceConfig.cs) / [App.config](file:///e:/Project/BarometerWinform/BarometerWinform/App.config) — PanelColumns/PanelRows 默认值调整为 8/9
- [MainForm.cs](file:///e:/Project/BarometerWinform/BarometerWinform/Views/MainForm.cs) — 更新 TableLayoutPanel 布局说明注释（8×9 + 行全选按钮列）
- [README.md](file:///e:/Project/BarometerWinform/README.md) — 更新配置项默认值说明

### 为什么这么改
- 现场需求为 72 个气压表面板，但希望以“8列×9行”的方式排列显示，便于视觉对齐与操作习惯统一。
