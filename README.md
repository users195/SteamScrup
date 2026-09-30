# SteamScrup

Windows 上的 Steam 游戏卸载残留清理工具。**默认永远只预览，必须手动勾选并二次确认才会删除。**
Portable Steam leftover cleaner for Windows: preview first, clean manually, never deletes on its own.

**便携版：解压即用，不安装、不写注册表、不需要管理员权限、零运行时依赖（自带 .NET 10）。**

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)

---

## 免责声明

- 本项目是**个人开发的第三方工具**，与 Valve Corporation **没有任何关联**，未经其授权、认可或赞助。
- "Steam" 是 Valve Corporation 的商标，本项目仅在**描述用途**的意义上使用该名称。
- 本工具**只操作本机文件**，不会登录账号、不会访问网络账户、不上传任何数据。
  （唯一联网行为是查询游戏名称，详见「联网说明」章节。）
- 清理操作**由使用者自行判断并确认**。请务必在清理前核对列表，尤其是标记为「需确认」的项目。

## 许可

本项目以 [MIT 许可证](LICENSE) 开源。

---

## 安全保证（不可关闭）

这些不是设置项，是写死在代码里的约束，界面上也只做展示：

| 保证 | 实现位置 |
|---|---|
| 永不自动删除，永不后台静默清理，无定时任务，无开机自启 | 全部清理都必须经过 `ConfirmCleanupWindow` |
| 永不删除任何 `appmanifest_<appid>.acf` 清单文件 | `Cleaner.Delete` 的硬护栏（正则匹配） |
| 永不触碰 userdata 存档（除高级选项手动开启） | 高级开关默认关闭，关闭时整个分类不显示 |
| 零联网、无登录、无遥测、无更新检查 | 全项目无网络调用 |
| 不提权运行 | 权限不足的项跳过并记入日志 |
| 不触碰 Steam 自身功能目录 | `SafetyRules.ProtectedCommonFolderNames` |
| 不删除含存档 / MOD 的内容 | `SafetyRules.SaveDataMarkers` / `UserModMarkers` 强制降级为「需确认」且默认不勾选 |

程序自带**自检模式**，你可以自己验证上述保证：

```powershell
SteamScrup.exe --selftest report.txt
```

它会检查 VDF 解析、库发现、大小写去重、临时 acf 排除、中文路径解码、扫描器安全性、
appmanifest 删除护栏、便携数据目录等，并把结论写入报告文件（退出码 0 = 全部通过）。

---

## 系统要求

| 项目 | 要求 |
|---|---|
| 操作系统 | Windows 10 / 11（x64） |
| 运行时 | **无要求** —— 自带 .NET 10，解压即用 |
| 权限 | **普通用户即可**，不申请任何提权 |

---

## 使用

1. 把 `SteamScrup-1.4.0-portable.zip` 解压到任意目录（例如 `D:\Tools\`）
2. 双击 `SteamScrup.exe`
3. 程序自动读取 Steam 全部库路径（注册表 + `libraryfolders.vdf`），无需手动输入
4. 在列表里核对、勾选要清理的项目
5. 点右下角「清理所选」→ 二次确认 → 执行

### 重要行为

- **是否需要退出 Steam 取决于所选类别**（v1.4 起为精细控制，不再是"一刀切"）：

  | 需要先完全退出 Steam | 可以直接清理（Steam 可保持运行） |
  |---|---|
  | Steam 临时文件、Steam 着色器缓存、创意工坊缓存、userdata 存档 | **DirectX / 显卡缓存、崩溃转储、游戏目录残留** |

  勾选项全部属于右侧三类时，"清理所选"保持可用并显示绿色提示「所选项目无需退出 Steam」；
  一旦勾选任何左侧类别，按钮禁用并弹出黄色横幅说明有几项需要退出 Steam。
- **默认送回收站**（可还原），可在设置里改为永久删除。
- **送回收站不会立即释放磁盘空间**，需要清空回收站后才真正腾出空间。
- 删除操作**实时写入日志**，每行都 flush，程序崩溃也不会丢失记录。

### 设置与日志存放位置

**优先存放在程序所在目录**（`settings.json` 和 `logs\`），整个文件夹可以随意复制到别的机器，配置跟着走。

如果程序目录不可写（例如被放进 `Program Files` 或只读共享），会自动改用
`%LOCALAPPDATA%\SteamScrup`，**不会报错**。当前实际使用的位置显示在自检报告里。

日志可在设置里改到任意目录，并可从界面直接打开日志文件夹。

---

## 清理类别与风险分级

每一行都带风险等级，**只有「安全」项会默认勾选**：

| 等级 | 含义 | 默认勾选 |
|---|---|---|
| 安全 | 空目录、仅剩少量残留文件、或可自动重建的缓存 | ✅ |
| 较低风险 | 有实体内容但无游戏清单；或游戏仍在但缓存可重建 | ❌ |
| 需确认 | 含可执行文件、体积较大、含存档或 MOD | ❌（并在确认框里逐条列出） |
| 受保护 | Steam 自身功能目录，不可删除 | 灰掉不可选 |

### 1. 游戏目录残留（`steamapps\common`）

对比所有库的 `installdir` 集合（**跨库匹配**，避免游戏装在其他库时被误判），
结合目录内容物判定等级。可识别的典型情况：

- **空目录**，或只剩一个 `steam_appid.txt` 的空壳目录 → 判为「安全」
- 仅剩配置 / 崩溃数据的目录 → 判为「安全」或「较低风险」
- **受保护不误删**：`Steamworks Shared`、`Steam Controller Configs`（含账号手柄配置）、`screenshots`、`sourcemods`
- **降级为「需确认」不误删**：含 `win64_save` / `Saves` 等存档目录的、含 ReShade 等画质插件（`dxgi.dll` 等）的、
  含 MOD 安装包（`.exe`/压缩包）的目录

### 2. Steam 着色器缓存

`steamapps\shadercache\<appid>`。游戏已卸载的（孤儿）为「安全」，游戏仍在的为「较低风险」，
删除后由游戏自动重建。

### 3. 创意工坊缓存

`workshop\content\<appid>`、`workshop\temp`、`workshop\downloads`、失效的 `appworkshop_<appid>.acf`。
游戏已卸载的工坊内容判为「安全」。

### 4. Steam 临时文件

`steamapps\corrupt`（损坏的下载分块）、`steamapps\temp`、`steamapps\downloading`、
以及 `appmanifest_<id>.acf.<n>.tmp` 这类更新残留文件。

### 5. DirectX / 显卡着色器缓存

**白名单式扫描**，只扫这些明确路径（存在才扫）：

| 路径 | 性质 |
|---|---|
| `%LOCALAPPDATA%\D3DSCache` | Windows 系统级 DirectX 着色器缓存 |
| `%LOCALAPPDATA%\NVIDIA\DXCache`、`GLCache`、`ComputeCache`、`VkCache` | NVIDIA 着色器缓存 |
| `%LOCALAPPDATA%\AMD\DxCache`、`DxcCache`、`VkCache` | AMD 着色器缓存 |
| `%LOCALAPPDATA%\Intel\ShaderCache` | Intel 着色器缓存 |
| `%LOCALAPPDATA%\Microsoft\DirectX Shader Cache` | 系统 DirectX 缓存 |
| `%APPDATA%\NVIDIA\ComputeCache` | NVIDIA 计算缓存 |

> ⚠️ 这些都是**下载/编译得来、删除后自动重建**的缓存，不影响存档与游戏进度。
>
> 关键设计：**绝不按目录名盲扫游戏内容目录**。例如 `Counter-Strike Source\cstrike\materials\temp`
> 名字看着像缓存，实际是游戏内容文件，删了会损坏游戏。因此只对**已判定为残留的目录**
> 做有界遍历，并仅匹配已知的崩溃转储类目录名。

### 6. 崩溃转储

游戏目录内的 `CrashReport`、`CrashDumps`、`Crash Logs`、`.crashdata`、`minidumps` 等
（含 `*.dmp` 崩溃转储），均为「安全」。

### 7. userdata 存档清理（高级，默认关闭）

需在设置里手动开启「显示 userdata 存档清理」后才会出现。

- 已卸载游戏的存档目录会被列出，**含实际存档的一律默认不勾选**，红标提示可能永久丢失
- 仍处于安装状态的游戏存档标记为「受保护」，不可删除
- `config`、`ugc`、`ugcmsgcache`、`inventorymsgcache`、`gamerecordings`
  是账号活动数据**不是垃圾**，已排除
- 确认框要求勾选「我已知晓存档可能永久丢失，风险自负」；即使确认，**仍走回收站**作为第二道保险

> 提示：`remotecache.vdf` 只是 Steam Cloud 同步元数据，删除它不会删除云端存档；
> 支持云存档的游戏重装后存档会重新同步回来。但**不支持云存档的游戏就是永久丢失**。

---

## 从源码构建

### 前置条件

| 需要 | 说明 |
|---|---|
| **.NET 10 SDK** | 项目目标框架为 `net10.0-windows`，需要 SDK（不是只装运行时） |
| Windows 10 / 11 | 需要 WindowsDesktop 目标包来编译 WPF |
| PowerShell | 构建脚本为 `.ps1` |

### 第一步：准备依赖（clone 后必须做）

仓库**不包含** SDK 与 NuGet 包（两者合计约 1.1 GB），需要自行准备：

```powershell
# 方式一（推荐）：直接使用系统安装的 dotnet，并允许访问 nuget.org
#   自包含发布需要以下四个包，版本必须与 SDK 的 WindowsDesktop 引用包一致：
#     Microsoft.NETCore.App.Runtime.win-x64
#     Microsoft.WindowsDesktop.App.Runtime.win-x64
#     Microsoft.AspNetCore.App.Runtime.win-x64
#     Microsoft.NET.ILLink.Tasks
#   目标框架为 net10.0 时，请使用 10.0.x 版本（本项目开发时使用 10.0.12）。

# 方式二：便携 SDK（不写注册表、不污染系统，与本项目开发环境一致）
$ver = '10.0.401'
Invoke-WebRequest "https://builds.dotnet.microsoft.com/dotnet/Sdk/$ver/dotnet-sdk-$ver-win-x64.zip" -OutFile sdk.zip
Expand-Archive sdk.zip -DestinationPath .\tools\dotnet -Force
```

> `tools\NuGet.config` 目前**只声明本地源** `tools\runtimepacks`，以便完全离线构建。
> 若你的网络可以访问 nuget.org，把该文件里的 `nuget.org` 源加回去即可让 `dotnet restore` 自动拉取上述包。

把下载好的 `.nupkg` 放进 `tools\runtimepacks\` 后，构建无需联网。

### 第二步：构建

```powershell
# 快速构建（框架依赖，仅用于本地调试）
.\tools\build.ps1

# 生成便携版 ZIP（自包含单文件，最终交付物）→ dist\SteamScrup-<版本>-portable.zip
.\tools\build.ps1 -Portable

# 运行无头自检
.\tools\build.ps1 -SelfTest

# 重新生成界面图标（需要 Python + Pillow）
python .\tools\make_icon.py

# 校验图标字体码位是否真实存在（防止图标显示成方框）
python .\tools\check_glyphs.py
```

若 PowerShell 执行策略禁止运行脚本，用：

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\build.ps1 -Portable
```

### 仓库里有哪些工具脚本

| 路径 | 用途 |
|---|---|
| `tools\build.ps1` | 构建 / 自包含单文件发布 / 自检，统一入口 |
| `tools\NuGet.config` | 本地包源配置，使构建可以完全离线 |
| `tools\make_icon.py` | 用 Pillow 生成 `app.ico` 与预览图 |
| `tools\check_glyphs.py` | 像素级比对，确认图标码位在字体里真实存在 |
| `tools\probe_icons.py` | 渲染图标对照图，便于人工挑选字形 |
| `tools\fetch.js` | 用 Node 的 OpenSSL 下载文件的小工具（绕开 schannel 限制时有用） |

> `tools\dotnet`、`tools\nuget`、`tools\runtimepacks` 已在 `.gitignore` 中排除。

### 交付物为什么只有一个 exe

自包含发布的 .NET 应用默认会在 exe 旁边铺开约 240 个 DLL，根目录非常杂乱。这些文件
**不能简单移进子文件夹**：原生 host 要求 `hostfxr.dll` / `hostpolicy.dll` / `coreclr.dll`
与 apphost 同目录，`SteamScrup.dll` 也必须同名同目录，实测移入子目录会直接启动失败。

所以改用**单文件发布**（`PublishSingleFile` + `IncludeNativeLibrariesForSelfExtract` +
`EnableCompressionInSingleFile`）。结果是文件夹里只有 `SteamScrup.exe` 和说明文件，
压缩后体积也更小（53 MB 对 57 MB）。

> 单文件模式依赖 `Microsoft.NET.ILLink.Tasks`，该包已放入 `tools\runtimepacks`，因此构建
> 仍然完全离线。

### 为什么构建能离线进行

`tools\NuGet.config` **只声明本地源**、不包含 nuget.org，并把 `globalPackagesFolder` 指向
`tools\nuget`。自包含发布需要的包（`Microsoft.NETCore.App.Runtime.win-x64`、
`Microsoft.WindowsDesktop.App.Runtime.win-x64`、`Microsoft.AspNetCore.App.Runtime.win-x64`、
`Microsoft.NET.ILLink.Tasks`，版本均为 `10.0.12`）已放在 `tools\runtimepacks`，
因此 `dotnet publish` 全程不联网。

> 版本对齐很重要：`10.0.12` 必须与 SDK 自带的 `Microsoft.WindowsDesktop.App.Ref` 版本一致。
> 升级 SDK 后，需要同步替换 `tools\runtimepacks` 里的这几个包。

### 一个容易踩的 WPF 坑（已修复，勿回退）

`ProgressBar.Value`、`TextBox.Text`、`Run.Text`、`ToggleButton.IsChecked` 这些目标属性
**默认就是 `TwoWay`**（元数据里 `BindsTwoWayByDefault = true`）。把它们绑到只读属性上，
WPF 会在 `Window.Show()` 期间抛 `InvalidOperationException`
（"A TwoWay or OneWayToSource binding cannot work on the read-only property ..."），
**整个窗口都起不来**。而且 XAML 里没有 `Mode=OneWay` 的简写语法，`{Binding X}` 无法局部改模式。

本项目采用的处理方式：

| 位置 | 做法 |
|---|---|
| 进度条 | 完全不用 `ProgressBar`，改为两列 `Grid`（`GridLength` 永远不会被写回），绑定 `ScanProgressColumn` |
| 底栏计数 | 用自定义标记扩展 `{ui:OneWayText SelectedCount}`（`Run.Text` 需要显式 OneWay） |
| 本地化索引器 | `Item[]` 故意提供**忽略写入的 setter**，因为部分控件模板会用 `Default` 模式写回它 |
| 文本框 | 绑定的都是**可写**属性（`SearchText` / `LogDirectory` / `SteamRootOverride`），TwoWay 是正确行为 |
| 复选框 | 绑定的都是**可写**属性（`IsSelected` / `ShowUserData`） |

改动这部分代码后，**务必跑一次界面冒烟测试**（见下），它会自动把上述问题全部检出来。

---

## 命令行参数

```
SteamScrup.exe                        启动图形界面
SteamScrup.exe --selftest [报告路径]   无头自检（不建立窗口）
SteamScrup.exe --exit-after=10000 --binding-report=报告路径
                                      界面冒烟测试：真开窗口、跑扫描、检查全部数据绑定后自动退出
SteamScrup.exe --dump-bindings=报告路径  不显示窗口，转储一层绑定信息
SteamScrup.exe --uninstall [--silent] 移除程序（便携版直接删文件夹即可）
```

### 界面冒烟测试

因为绑定错误只在 `Window.Show()` 期间爆发，而且不会写进普通日志，所以专门做了这个模式：

```powershell
SteamScrup.exe --exit-after=10000 --binding-report=report.txt
```

它会真正打开窗口、执行一次完整扫描，**并自动模拟用户操作**，然后写出报告并退出。报告里包含：

- `show() result` —— 窗口是否成功显示（`OK` 或异常类型与消息）
- **运行时主题切换**：通过设置里的单选按钮依次切到浅色/深色/跟随系统，记录每次解析到的 `Accent` 颜色
- **选择命令**：调用全选/全不选/仅选安全项，核对可见行数与受保护行数
- **语言切换**：切到 English 后检查侧边栏与状态栏是否还有中文残留
- **搜索框 placeholder**：空白未聚焦 / 聚焦 / 已输入 三种状态下的可见性
- 绑定告警列表（来自 `PresentationTraceSources.DataBindingSource`）
- 完整绑定表，含每个绑定**最终生效的模式**
- 一条结论：**所有 TwoWay 绑定的源属性是否可写**（防止"不崩但输入框打不进字"）

退出码：`0` = 启动正常，`2` = 启动时抛异常。改完 XAML 或设置逻辑后跑一次即可。

> 这个模式在 v1.1 开发中真正发挥了作用：它抓出了"切换深色主题无效"的根因——
> `OnThemeChecked` 只改了 `Settings.Theme`，**从未调用 `ThemeService`**，
> 所以 `ApplyTheme()` 根本没被触发。之前手动测试时直接操作了 service，因此误判为正常。

---

## 项目结构

```
src/SteamScrup/
  Core/                   逻辑层（与 UI 完全解耦，可独立测试）
    VdfParser.cs           自研 VDF 分词器（引号转义、嵌套、注释）
    SteamLocator.cs        注册表 + libraryfolders.vdf 发现，大小写不敏感去重
    SteamModels.cs         库 / 清单模型
    Scanner.cs             六类扫描器 + 可取消的尺寸测量
    SafetyRules.cs         保护名单、存档/MOD 识别、缓存白名单
    Cleaner.cs             回收站/永久删除 + appmanifest 硬护栏
    AppPaths.cs            便携数据目录解析（不可写时回退）
    OperationLog.cs        实时 flush 的按天日志
    AppSettings.cs         设置持久化（JSON）
    Localizer.cs           轻量 i18n
    LocalizationStrings.cs 中英文全部文案
    ThemeService.cs        跟随系统主题
  UI/
    MainWindow.xaml        主窗口（总览 / 列表 / 设置）
    ConfirmCleanupWindow.xaml   二次确认 + 高风险逐条列出 + 风险确认框
    CleanupResultWindow.xaml    清理结果
    MainViewModel.cs       扫描、筛选、勾选、清理流程
    Themes/                Dark.xaml / Light.xaml
    Styles.xaml            控件模板与排版
  SelfTest.cs              无头自检（--selftest）
  Uninstaller.cs           --uninstall 实现
tools/                     便携 SDK、运行时包、构建脚本、图标生成、下载器
```

---

## 已知限制

- 只能识别**本机已登录账号**可见的 Steam 配置；若 Steam 从未在本机登录过，需在设置里手动指定 Steam 目录
- 跨库判定依赖所有库的 `libraryfolders.vdf` 可读；某个库离线 / 未挂载时，该库的游戏会被当作已卸载
- 回收站方式不会立即释放空间，需要清空回收站
- 卸载残留判定基于文件系统特征，无法 100% 确定某目录「一定是」残留，
  因此含存档、MOD、可执行文件的目录一律降级为「需确认」且默认不勾选
- **已下架游戏的名称无法解析**：Steam 官方接口对这类 appid 返回 `success: false`，
  本地也没有留存名称，界面上会显示为 `AppID <数字>`
- 未做代码签名，首次运行时 Windows SmartScreen 可能提示「未知发布者」，选择「仍要运行」即可。
  根除该提示只能购买代码签名证书（详见下节）

## 关于 SmartScreen 提示

未签名的可执行文件在 Windows 上会触发 SmartScreen「未知发布者」提示。这是系统行为，
没有免费且可靠的绕过方式——SmartScreen 不信任自签名证书，且即使使用普通（OV）
代码签名证书也需要累积下载信誉后才会消除提示。若要让发布即刻可信，只能购买
EV 代码签名证书。

在未签名的情况下，建议使用者自行核对文件哈希后再运行。

---

## 贡献

欢迎提交 Issue 与 Pull Request。修改代码时请注意：

1. **改动 XAML 后必须运行界面冒烟测试**（`--exit-after` + `--binding-report`）。
   WPF 中 `ProgressBar.Value`、`TextBox.Text`、`Run.Text` 等属性默认双向绑定，
   绑到只读属性上会在窗口显示阶段直接抛异常导致**程序无法启动**，
   而这类错误不会写进普通日志。
2. **新增本地化文案必须同时补齐中英文两份**，否则会直接显示键名。
3. **图标字形必须先用 `tools\check_glyphs.py` 验证**，选错码位会渲染成方框。

## 许可

本项目以 [MIT 许可证](LICENSE) 发布，可自由使用、修改、分发（需保留版权声明）。

本工具仅操作本机 Steam 文件，不收集、不上传任何用户数据。
