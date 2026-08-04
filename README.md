# 专注计时器

一个可直接双击运行的 Windows 正计时与倒计时工具。发布版本是单文件自包含 EXE，目标电脑不需要安装 .NET。

## 功能

- 简约浅色界面：圆润卡片、克制留白和微软雅黑字体
- 倒计时：5 / 25 / 45 分钟预设，也可自定义 1 秒至 99 小时 59 分 59 秒
- 正计时：精确到百分之一秒
- 开始、暂停、继续、重置和到时提醒
- 窗口可自由调整大小与最大化，界面自适应布局
- 支持窗口置顶；快捷键为空格、R、1、2
- 计时使用单调时钟，系统时间调整不会造成计时跳变

## 运行

双击 `FocusTimer.exe` 即可运行。

## 从源码发布

安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 后，在项目目录执行：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

生成文件：`bin\Release\net8.0-windows\win-x64\publish\FocusTimer.exe`
