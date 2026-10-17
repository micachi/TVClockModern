# TVClock Modern

📖 用户手册：[简体中文](docs/USER_MANUAL.zh-Hans.md) / [日本語](docs/USER_MANUAL.md) / [English](docs/USER_MANUAL.en.md) / [한국어](docs/USER_MANUAL.ko.md)

<p align="center">
  <img src="assets/icon-512.png" width="128" alt="TVClock Modern">
</p>

> **注意：本项目与 TVClock 及其作者无任何关联。**
> 这是一个独立的开源项目，不使用任何原始代码、资源或商标。

一款常驻 Windows 桌面的数字时钟。采用**净室方式从零实现**的通用桌面时钟。
基于 .NET 8 / WPF 构建。单个 exe、常驻运行、低负载、抗崩溃设计。

![.NET 8.0](https://img.shields.io/badge/.NET-8.0--windows-blue) ![platform](https://img.shields.io/badge/platform-Windows%2010%2F11-lightgrey)

## 特性

- **小部件式常驻**：置顶 / 无边框 / 背景透明 / 不显示在 Alt-Tab / 单个 exe
- **多显示器支持**：在所有显示器上同时显示、指定显示器、自动跟随显示器配置变化、Per-Monitor V2 DPI
- **丰富的显示**：字体 / 大小 / 颜色 / 描边 / 阴影 / 不透明度 / 时间日期格式自由设定
- **逐字符渲染**：可调整字符间距与分隔符间距
- **粗体**：可选择以粗体显示时间和日期（“文字”页签）
- **切换效果**：横向翻转 / 纵向翻转 / 上滑 / 下滑 / 淡入淡出 / 交叉淡化
- **鼠标接近淡化**：鼠标靠近时变透明。按下透明解除键（Ctrl 等）可临时进行操作
- **点击穿透**：可操作时钟下方的窗口（托盘图标始终可用，因此不会被"锁在外面"）
- **更新通知**：每 24 小时在启动时自动检查一次 GitHub Releases。气泡通知＋“信息”页签，可前往发布页面
- **多语言界面**：日本語 / English / 简体中文 / 한국어（在“行为”页签切换，立即生效）
- **位置预设**：左上 / 右上 / 左下 / 右下 / 居中。位数变化时自动重新对齐
- **设置导入 / 导出**：以 JSON 格式随身携带

## 截图

**桌面常驻**（调整了描边、字符间距、分隔符间距的示例）

![](docs/shot-clock.png)

**设置界面**（更改通过"应用"按钮生效）

![](docs/shot-settings.png)

**时间切换效果** — 每当秒数变化时，数字都会以动画方式切换（效果时长 50–500 ms）：

| 效果 | 动画 |
|---|---|
| 横向翻转 | ![](docs/effect-flipx.gif) |
| 纵向翻转 | ![](docs/effect-flipy.gif) |
| 上滑 | ![](docs/effect-slideup.gif) |
| 下滑 | ![](docs/effect-slidedown.gif) |
| 淡入淡出 | ![](docs/effect-fade.gif) |
| 交叉淡化 | ![](docs/effect-crossfade.gif) |

## 抗崩溃设计

- 三重异常处理器（`Dispatcher` / `AppDomain` / `TaskScheduler`）— UI 异常会被记录并继续运行；致命退出时**自动自我重启**
- 设置文件采用**原子写入 + 三级回退**（主文件 → 备份 → 默认值），损坏的文件会被隔离
- **秒同步计时器**（每秒唤醒一次）、淡化由 GPU 合成、接近监测仅为 60 Hz 轮询
- 日志：`%LOCALAPPDATA%\TVClockModern\logs\`（自动轮转保留 7 天）

## 运行环境

- Windows 10 / 11 (x64)
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)（自包含版本无需）

## 构建

```powershell
# 依赖框架（约 150 KB）
dotnet publish -c Release -o publish

# 自包含单个 exe（无需运行时）
dotnet publish -c Release -o publish-sc -p:SelfContained=true
```

## 使用方法

| 操作 | 行为 |
|---|---|
| 启动 | `publish\TVClockModern.exe` |
| 拖动 | 移动（位置自动保存） |
| 双击 | 设置界面 |
| 右键点击 | 菜单 |
| 任务托盘 | 全部功能（解除点击穿透也在此操作） |
| `--settings` 参数 | 带设置界面启动（用于自动化） |

设置保存在 `%APPDATA%\TVClockModern\settings.json`。

**详细用法请参阅[用户手册](docs/USER_MANUAL.md)。**
（全部设置项说明、格式说明符列表、多显示器行为、故障排除等）

变更历史请参阅 [CHANGELOG.md](CHANGELOG.md)。

## 免责声明（无担保、按现状提供）

本软件**按"现状"（AS IS）提供**。不作出任何明示或默示的保证，包括但不限于对适销性、特定用途适用性和不侵权的保证。

- 对于因使用或无法使用本软件而产生的任何损害（数据丢失、故障、金钱损失等），作者不承担任何责任
- 由于常驻软件的特性，根据您的环境可能出现意外行为。在部署到重要工作环境之前，请先确认运行状况
- 请自行负责管理设置文件和日志

## 许可证

[MIT](LICENSE)

## 关于名称

"TVClock Modern"这一名称是为了表明本项目是一个受现有桌面时钟应用启发的**独立项目**。本项目不属于以下任何一种情况：

- 对现有 TVClock（或其他时钟软件）的分支、修改或反汇编
- 与现有软件的作者或权利人的合作、认可或赞助
- 使用现有软件的资源（图像、图标、代码）
