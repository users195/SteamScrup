# SteamScrup

Windows 上的 Steam 游戏卸载残留清理工具。

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

## UI

### 深色模式

![深色模式](https://github.com/users195/SteamScrup/blob/main/screenshots/Dark%20Mode.jpg)

### 浅色模式

![浅色模式](https://github.com/users195/SteamScrup/blob/main/screenshots/Light%20Mode.jpg)

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

### 6. 崩溃转储

游戏目录内的 `CrashReport`、`CrashDumps`、`Crash Logs`、`.crashdata`、`minidumps` 等
（含 `*.dmp` 崩溃转储），均为「安全」。

### 7. userdata 存档清理

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
