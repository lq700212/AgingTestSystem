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
