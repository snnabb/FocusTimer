# 专注计时器

一个可直接双击运行的 Windows 倒计时器。发布版本是单文件自包含 EXE，目标电脑不需要安装 .NET。

## 功能

- 设置 0–999 分钟与 0–59 秒
- 开始、暂停、继续、重置
- 到时播放 Windows 系统提示音并显示通知
- 可选窗口置顶

## 运行

双击 `FocusTimer.exe` 即可运行。

## 从源码发布

安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 后，在项目目录执行：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

生成文件：`bin\Release\net8.0-windows\win-x64\publish\FocusTimer.exe`
