# 专注计时器

一个可直接双击运行的 Windows 正计时与倒计时工具。发布版本是单文件自包含 EXE，目标电脑不需要安装 .NET。

## 功能

- 极简深色界面：高对比度大数字、圆形进度和克制的系统色
- 倒计时：5 / 10 / 25 / 45 分钟预设，也可自定义小时、分钟和秒
- 秒表：百分秒精度、暂停、复位和多次计次记录
- 支持声音开关、窗口置顶和到时提醒
- 快捷键：空格开始/暂停，R 复位，1/2 切换模式，L 记录计次

## 运行

双击 `FocusTimer.exe` 即可运行。

## 从源码发布

安装 [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) 后，在项目目录执行：

```powershell
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

生成文件：`bin\Release\net8.0-windows\win-x64\publish\FocusTimer.exe`
