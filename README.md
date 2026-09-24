# Windows 内存清理工具 (WinMemoryCleaner)

[![](https://img.shields.io/badge/WINDOWS-VISTA%20%E2%80%93%2011-blue?style=for-the-badge)](#windows-内存清理工具-winmemorycleaner) [![](https://img.shields.io/badge/SERVER-2008%20%E2%80%93%202025-blue?style=for-the-badge)](#windows-内存清理工具-winmemorycleaner) [![](https://img.shields.io/github/license/zhengwuji/WinMemoryCleaner?color=2ea44f&style=for-the-badge)](/LICENSE) [![](https://img.shields.io/github/downloads/zhengwuji/WinMemoryCleaner/total?color=orange&style=for-the-badge)](https://github.com/zhengwuji/WinMemoryCleaner/releases/latest)

**WinMemoryCleaner** 是一款完全免费、开源的 Windows 系统内存智能优化与深度清理神器。软件直接调用 Windows 原生底层 API 接口，对系统各类内存缓存区（包括待机列表、进程工作集、系统缓存等）进行精准清理与整理，有效解决大型游戏卡顿掉帧、开发编译卡死、多任务运行迟缓以及软件内存泄漏问题。

本工具为纯绿色单文件便携软件，无需安装、解压即用，体积小巧，界面现代友好，功能强悍。

[![](./docs/assets/images/main-window.png)](#windows-内存清理工具-winmemorycleaner)

---

## 💾 软件下载

[![](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fapi.github.com%2Frepos%2Fzhengwuji%2FWinMemoryCleaner%2Freleases%2Flatest&query=%24.tag_name&label=Latest%20Release&style=for-the-badge)](https://github.com/zhengwuji/WinMemoryCleaner/releases/latest/download/WinMemoryCleaner.exe)

* **官方最新 Release 下载**：[点击前往 GitHub Releases 页面](https://github.com/zhengwuji/WinMemoryCleaner/releases/latest)
  * `WinMemoryCleaner.exe`：单文件独立程序（即点即用，推荐）
  * `WinMemoryCleaner.zip`：便携绿色压缩包（含说明文档与变更日志）

### 🍫 常见包管理器安装

你也可以通过常用命令行包管理器快速安装与更新：

#### Chocolatey
```cmd
choco install winmemorycleaner
```

#### Scoop
```cmd
scoop bucket add extras
scoop install extras/winmemorycleaner
```

#### WinGet
```cmd
winget install IgorMundstein.WinMemoryCleaner
```

---

## 🚀 核心功能一览

| 功能名称 | 功能说明 |
|:---|:---|
| **窗口置顶 (Always on Top)** | 将主程序窗口固定在屏幕最前，方便随时观察并进行内存整理。 |
| **智能定时/阈值自动优化** | 支持两种自动模式：定时自动清理（如每隔 X 小时），或当剩余物理内存低于设定百分比时自动触发深度清理。 |
| **自动检查更新 (Auto Update)** | 每 24 小时自动静默检查是否有最新可用版本，保持功能最新。 |
| **清理后自动关闭** | 手动点击优化完成后，程序自动退出，不占用任何多余系统资源。 |
| **关闭至系统托盘** | 点击窗口右上角关闭按钮时，自动最小化到系统右下角托盘区常驻后台。 |
| **极简紧凑模式 (Compact Mode)** | 一键将窗口折叠为微型迷你悬浮监控条，仅保留最核心指标。 |
| **界面字体缩放调节** | 适配 2K/4K/高分屏及大字体缩放，界面清晰不糊。 |
| **全局一键优化热键** | 任意界面随时按下全局热键（默认 `Ctrl+Shift+M`，支持自定义）即可瞬时清理内存。 |
| **快捷键独立开关** | 可根据个人习惯自由启用或禁用全局热键。 |
| **全语言国际化支持** | 内置包含简体中文、繁体中文、英文、日语、韩语、德语、法语在内的 30+ 种语言。 |
| **进程排除保护名单** | 支持自定义关键进程黑白名单，在批量修整工作集时跳过重要程序。 |
| **低优先级静默运行** | 调低程序执行清理时的 CPU 优先级，避免在老旧电脑上因高频清理导致系统瞬时卡顿。 |
| **开机自动自启** | 通过 Windows 计划任务实现开机静默启动，无需反复手动打开。 |
| **浮窗/通知栏反馈** | 每次清理完毕后，右下角弹窗显示本次优化释放的具体内存容量与触发原因。 |
| **虚拟内存 (分页文件) 监控** | 实时显示系统物理内存与虚拟内存 (Pagefile) 的实际使用状态。 |
| **托盘图标高级定制** | 实时将系统当前物理内存占用百分比数字直接绘制在托盘图标上，颜色随占用率自动渐变（警告/危险/正常），支持中键一键点击清理。 |

### ↕️ 极简紧凑模式 (Compact Mode)
点击窗口右上角最小化按钮左侧的折叠箭头，主窗口即可缩减至极小面积，仅保留内存曲线与清理触发器，适合边玩游戏边盯紧内存。

[![](./docs/assets/images/main-window-compact.png)](#↕️-极简紧凑模式-compact-mode)

### 🔔 系统托盘深度定制
* **右键菜单**：随时右键托盘图标执行“立即优化”或“退出程序”。
* **清理反馈**：清理后弹出 Toast 通知，告知本次已释放几百 MB 或几 GB 内存。
* **实时占用图标**：托盘图标可显示当前内存百分比数值，告急时呈现预警色彩。

[![](./docs/assets/images/notification.png)](#🔔-系统托盘深度定制)

---

## 🧬 技术原理：各内存区域深度解析

WinMemoryCleaner 绝非市面上那种伪造数字的“流氓内存加速器”，而是基于微软官方公开的 **Windows Memory Management API**（例如 `NtSetSystemInformation` 等原生系统调用）实现的真实底层优化。

| 内存区域 | 原理与运作机制 | 最低系统要求 |
| :--- | :--- | :---: |
| **合并页面列表 (Combined Page List)** | 刷新页面合并列表。Windows 8+ 引入的内存去重特性会将内容相同的内存物理页合并，清理此列表可立即收回未解绑的冗余块。 | Windows 8+ / Server 2012+ |
| **已修改文件缓存 (Modified File Cache)** | 将所有固定磁盘的文件缓存强制刷入硬盘，提交所有悬挂的写入事务并释放对应的缓存物理页。 | Windows 7+ / Server 2008 R2+ |
| **已修改页面列表 (Modified Page List)** | 将内存中已修改但尚未写入磁盘的页面刷新落盘，并将保存后的页面转入备用列表。 | Windows 7+ / Server 2008 R2+ |
| **注册表缓存 (Registry Cache)** | 刷新缓存在内存中的注册表配置单元 (Registry Hives)，回收长期不访问的注册表句柄内存。 | Windows 8.1+ / Server 2012+ |
| **备用列表 (Standby List)** | **核心项**。清空由已关闭应用遗留的文件缓存组成的待机列表。这是游戏玩家释放大量备用内存、解决微卡顿的最有效方式。 | Windows Vista+ / Server 2008+ |
| **低优先级备用列表** | 仅清理备用列表中优先级最低的缓存页，在保留高频重要缓存的同时温和释放部分内存。 | Windows 7+ / Server 2008 R2+ |
| **系统文件缓存 (System File Cache)** | 刷新 Windows 系统核心文件占用的工作缓存并压缩其尺寸，适合在运行大型生产力软件前一键回血。 | Windows 7+ / Server 2008 R2+ |
| **进程工作集 (Working Set)** | 修剪用户态进程及系统的 Working Set，强制程序（如浏览器或吃内存应用）归还暂时闲置的 RAM。 | Windows 7+ / Server 2008 R2+ |

---

## 🔎 事实见证：如何用自带工具验证效果

你可以随时使用 Windows 自带的 **资源监视器 (Resource Monitor)** 见证内存真实释放：

1. 按下 `Win + R` 输入 `resmon.exe` 并回车打开资源监视器。
2. 切换到 **“内存”** 选项卡，观察下方彩色横条中的深蓝色 **“备用” (Standby)** 区域。
3. 打开大型游戏、PS 或大量 Chrome 标签页然后全部关闭，此时深蓝色“备用”区会急剧扩大，可用内存变少。
4. 打开 WinMemoryCleaner，勾选 **`Standby List (备用列表)`**，点击 **`Optimize (优化)`**。
5. 观察资源监视器：深蓝色备用区瞬间骤降至极低，浅绿色的 **“可用/可用内存”** 同步暴增！

---

## 💻 命令行与静默自动化调用

本工具原生支持无头（Headless）静默运行，非常适合搭配开机脚本、批处理或特定游戏启动器联动使用：

### 🔳 命令行参数模式
可在命令行中自由组合需要清理的内存区域参数：
```cmd
WinMemoryCleaner.exe /CombinedPageList /ModifiedFileCache /ModifiedPageList /RegistryCache /StandbyList /SystemFileCache /WorkingSet
```

**常用示例（经典组合清理）：**
```cmd
WinMemoryCleaner.exe /ModifiedFileCache /StandbyList /WorkingSet
```

### ⚙️ Windows 后台服务模式
将程序安装为 Windows 底层系统服务，无需任何界面交互即可在后台自动根据设定静默执行维护：

* **安装为后台服务：**
  ```cmd
  WinMemoryCleaner.exe /Install
  ```
* **卸载系统服务：**
  ```cmd
  WinMemoryCleaner.exe /Uninstall
  ```

### 🔄 故障重置命令
如果误设配置导致程序打不开，可使用重置参数一键恢复出厂：
```cmd
WinMemoryCleaner.exe /Reset
```

---

## ❓ 常见问题 (FAQ)

### Q: 为什么杀毒软件或 Windows Defender SmartScreen 会提示警告？
**A: 纯属误报**。
1. 本程序需要使用管理员权限去调用内核级内存清理 API（如修剪系统缓存、清空待机池），普通权限无法触碰系统物理内存页。
2. 程序支持通过计划任务设置开机启动，杀毒软件对“带管理员权限自启的便携小工具”较为敏感。
3. 源码完全公开，CI 流程全部自动化且透明，绝不包含任何恶意行为。若遇到拦截，请在 Windows 安全中心或杀毒软件中添加信任即可。

### Q: 配置保存在哪里？
**A:** 保存在 Windows 注册表：`HKEY_LOCAL_MACHINE\SOFTWARE\WinMemoryCleaner`。

### Q: 如何查看程序运行与清理日志？
**A:** 按 `Win + R` 输入 `eventvwr` 打开“事件查看器”，点击 `Windows 日志 -> 应用程序`，来源筛选 `Windows Memory Cleaner` 即可查看所有详细记录。

---

## 📄 开源许可证

本项目基于 [GPL-3.0 License](/LICENSE) 开源发布，欢迎自由分发与研究。
项目仓库：[https://github.com/zhengwuji/WinMemoryCleaner](https://github.com/zhengwuji/WinMemoryCleaner)
