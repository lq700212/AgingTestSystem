@echo off
rem 老化测试系统一键激活（双击即跑，默认永久激活；只用 Windows 自带 PowerShell，免装环境）
rem 试用30天：auto_activate.bat -Mode trial
rem 只算码不写盘：auto_activate.bat -DryRun
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0auto_activate.ps1" %*
