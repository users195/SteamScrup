# SteamScrup v1.1 PRD — 界面缺陷修复与总览可视化

| 项目 | 内容 |
|---|---|
| 版本 | v1.1（在 v1.0 便携版基础上修复） |
| 前置基线 | `backup\SteamScrup-20260930-161420`（v1.0，自检 27/27 通过） |
| 交付形态 | 便携版 ZIP，保持 v1.0 全部红线不变 |
| 编写日期 | 2026-09-30 |

---

## 1. 本次目标

修复用户实测反馈的 8 项界面缺陷，并把总览页从"文字清单"改造为"图表化概况"。

## 2. 不可回退的约束（沿用 v1.0）

- 永不自动删除、无后台静默清理、无定时任务、无开机自启
- 永不删除 `appmanifest_*.acf`
- 默认不触碰 userdata 存档
- 零联网、无账号、无遥测
- 便携、不提权、自带 .NET 10
- 修复过程中**不得引入新的 `TwoWay` 绑定到只读属性**（v1.0 启动崩溃的根因，详见 §9）

---

## 3. 问题清单与修复方案

### Issue 1 — 切换到深色主题无效

**定位**：设置 → 外观 → 深色

**已完成的排查（实测证据）**

| 实验 | 结果 |
|---|---|
| 启动前写入 `settings.json` 的 `"Theme":"Dark"`，再启动 | 日志 `theme applied: requested=Dark effective=dark` → **启动路径正常** |
| 读取 Windows 主题注册表 | `AppsUseLightTheme = 1` → "跟随系统"显示浅色是**正确行为**，不是缺陷 |

**结论**：缺陷位于**运行时切换**路径，不是"跟随系统"。

**根因（待仪表化确认）**

`App.ApplyTheme()` 依赖两个环节，任一断裂都会表现为"点了没反应"：

1. `MainWindow.OnThemeChecked` 是否真的触发了 `ThemeService.Theme` 变更
   —— 该处理器带 `if (!_initialised) return;` 守卫，且依赖 `sender.Tag` 为 `"Dark"` 字符串
2. `ApplyTheme()` 替换 `Resources.MergedDictionaries[0]` 后，已实例化的控件是否重新解析 `DynamicResource`

v1.1 内部曾出现过同类问题（见 §9），因此**不接受"看起来对"**，必须用日志证明。

**修复方案**

- `ApplyTheme()` 增加可验证日志：替换前后的字典 `Source`、`MergedDictionaries.Count`、目标主题键
- `OnThemeChecked` 增加日志：收到哪个 `Tag`、`_initialised` 状态、是否发生实际变更
- 若确认是字典替换未生效，则改为**重建整份主题字典并强制刷新**：替换后在窗口上重新应用一次根级 `Background/Foreground`，并对 `MainWindow` 调用 `InvalidateVisual()`
- 兜底方案：把主题色改为"通过 `ThemeService` 暴露的可绑定属性 + 动态资源双写"，避免完全依赖字典替换

**验收**：浅色↔深色↔跟随系统 三态互切，界面颜色立即变化；切换后重启程序，选择被记住。

---

### Issue 2 — 语言切换不完整

**定位**：设置 → 语言 → English

**根因（已确认，代码级）**

| 残留中文 | 原因 |
|---|---|
| 侧边栏分类名 | `CategoryFilter.Label => L.T(LabelKey)` —— 在**构造函数里求值一次**并缓存进 `Categories` 集合，集合从不重建，也不发 `PropertyChanged` |
| "扫描完成，发现 x 个候选项目" | `StatusMessage` 是**一次性字符串快照**：`StatusMessage = L.F("msg.scanComplete", n)`，切换语言时不会重新生成 |

**修复方案**

- `CategoryFilter` 改为 `LocalizedViewModel` 子类，`Label` 改为读时求值并支持变更通知；`MainViewModel.OnLocalizationChanged()` 中刷新所有分类项
- `StatusMessage` 改为**结构化状态**：保存"状态键 + 参数"（例如 `("msg.scanComplete", [87])`），`StatusMessage` 属性在读取时用当前语言渲染。语言一变，所有状态文案自动跟随
- 同步排查其余一次性文案：`SelectedSizeText`、`VisibleCountText`、`CleanButtonText`、错误信息

**验收**：中↔英互切后，侧边栏、列表、状态栏、总览、设置、对话框**无任何残留中文**（反之亦然）。

---

### Issue 3 — "全选 / 全不选 / 仅选安全项"点击无效

**定位**：候选列表左上三个按钮

**根因（已确认，代码级）**

三个命令的 `CanExecute` 均为 `() => IsListVisible`：

```csharp
SelectAllCommand  = new RelayCommand(() => SelectAll(true),  () => IsListVisible);
SelectNoneCommand = new RelayCommand(() => SelectAll(false), () => IsListVisible);
SelectSafeCommand = new RelayCommand(SelectSafeOnly,         () => IsListVisible);
```

**初始状态 `IsSettingsSelected == false` 但 `IsListVisible` 从未被求值/通知**，WPF 在首次绑定时拿到的 `CanExecute` 为 `false` 并**不再重新查询**，因此按钮永久灰化或点了无响应。

**修复方案**

- `IsListVisible` 的 setter 触发 `RefreshCommands()`
- 三个命令的 `CanExecute` 改为同时校验"有可见行"`Rows.Count > 0`
- 点击后立即调用 `RaiseSelectionSummary()` 更新底部计数（防"选了但计数不动"）

**验收**：扫描完成后三个按钮可点，点击后列表勾选状态与底部已选数量、已选大小**同步变化**。

---

### Issue 4 — 候选界面重复的状态文字

**定位**：候选列表区域中间

**根因**：`MainWindow.xaml` 第 333 行有一个居中的 `TextBlock` 绑 `StatusMessage`，与底部状态栏（第 468 行）**同一数据源**，视觉重复。

**修复方案**：删除候选区域内那个居中 `TextBlock`。空列表提示改为专用文案（见 §5.3），不再复用 `StatusMessage`。

**验收**：候选界面内只出现一次状态文字。

---

### Issue 5 — 搜索框缺少输入引导

**定位**：候选列表右上搜索框

**已确认做法**：提示文字在框内叠加，输入时自动隐藏（真 placeholder）。

**修复方案**

- 用一个 `Grid` 把提示 `TextBlock` 叠在 `TextBox` 之上
- 提示可见条件：`SearchText` 为空 **且** 输入框未获得焦点
- 提示文案取 `msg.searchHint`（已有中英文），颜色用 `FgMuted`

**验收**：空白时显示灰色提示；一旦输入或聚焦，提示立即消失；清空后重新出现。

---

### Issue 6 — Steam 运行状态不实时更新

**定位**：顶部状态横幅、"清理所选"按钮、"重新扫描"按钮

**根因（已确认）**：`RefreshSteamState()` 只在**构造时**和**点击清理时**调用，没有任何进程监控。退出 Steam 后横幅与按钮状态会一直停留在旧值。

**已确认做法**：每 **3 秒**轮询，状态变化时自动重扫。

**修复方案**

- `DispatcherTimer` 每 3 秒调用 `SteamLocator.IsSteamRunning()`（`GetProcessesByName` 开销很低）
- 状态**发生变化**时：
  - 更新横幅文案与颜色（绿=未运行 / 黄=运行中）
  - 立即刷新两个按钮的可用性
  - **自动触发一次重扫**（记录日志说明是状态变化触发的）
- 清理期间（`IsDeleting`）暂停自动重扫，避免与删除竞争
- 轮询在窗口关闭时停止

**验收**：修改 Steam 进程状态后 **≤3 秒**界面自动跟随；退出 Steam 后无需手动操作，"清理所选"即变为可用。

---

### Issue 7 — 总览页移除安全保护说明

**定位**：总览页"安全保证（不可关闭）"卡片

**已确认做法**：从界面删除，内容并入 `README.md`。

**现状**：`README.md` **已包含**同内容的「安全保证（不可关闭）」章节，无需重复添加，只需确认措辞完整（含"不提权运行"一条）。

**修复方案**：删除 `MainWindow.xaml` 第 205–211 行的保护卡片；核对 README 章节覆盖全部 7 条保证。

**验收**：总览页无该卡片；README 中可查到全部保证条目。

---

### Issue 8 — 总览改用图表展示概况

**已确认做法**：统计**全部候选项目**；**饼图 + 条形图**；并标注其中"已勾选"的数量与大小。

**修复方案**

数据层新增（`MainViewModel`，全部为**只读派生属性**）：

| 属性 | 说明 |
|---|---|
| `CategoryStats` | 按分类聚合：`ScanCategory`、名称、条目数、字节数、占比 |
| `TotalCandidateBytes` | 全部候选总占用 |
| `SelectedCandidateBytes` | 其中已勾选的部分 |
| `LargestCategory` | 占用最大的分类（用于"主要来源"一句话结论） |

视图层（不引入第三方图表库，纯 WPF 几何绘制）：

- **饼图**：`ViewModel` 预计算 `PathGeometry`（`PathFigure` + `ArcSegment` 列表），XAML 用 `ItemsControl` 叠加 `Path` 绘制。占比 <1% 的扇区合并为"其他"，避免碎片
- **条形图**：每个分类一行，显示 `名称` + 横向比例条 + `大小` + `占比%`，按大小降序
- **顶部保留**关键指标：Steam 目录、库数量、已安装游戏数、候选总数
- **补一行结论**：例如"主要占用来自 Workshop 缓存 24.6 GB（62%）"

**验收**：总览能一眼看出"哪类垃圾最占空间"；扇区/条形与列表实际数据一致；切换中英文与主题后图表正常。

---

## 4. 交互与视觉规格

### 4.1 总览页布局（自上而下）

1. 关键指标卡：Steam 路径 / 库数量 / 已安装游戏 / 候选条目数
2. 磁盘占用概况卡：饼图（左）+ 图例（右，含每类颜色、名称、大小、占比）
3. 分类明细卡：条形图列表，每行 `色块 · 分类名 · 比例条 · 大小 · 占比%`
4. 结论行：最大占用来源 + 已勾选大小

> 原"安全保证"卡片删除（Issue 7）

### 4.2 分类配色

沿用现有主题色板，为 7 个分类各分配固定色，**深浅主题下都保持可读**：

| 分类 | 建议色相 |
|---|---|
| 游戏目录残留 | 蓝 `#66C0F4` |
| 创意工坊缓存 | 紫 `#9B8CF4` |
| userdata（高级） | 橙 `#E8A33D` |
| DirectX / 显卡缓存 | 青 `#4FC3A1` |
| Steam 临时文件 | 灰蓝 `#7A8A99` |
| 崩溃转储 | 红 `#E0685F` |
| Steam 着色器缓存 | 黄绿 `#B8C95A` |

### 4.3 空状态

- 未扫描：提示"点击右上角「重新扫描」开始"
- 扫描完成但该分类无项目：提示"此分类没有发现残留"
- 搜索无结果：提示"没有匹配的项目"

---

## 5. 技术要点

### 5.1 涉及文件

| 文件 | 改动 |
|---|---|
| `UI/MainViewModel.cs` | 状态结构化、分类本地化、命令可用性、Steam 轮询、图表数据 |
| `UI/MainWindow.xaml` | 删重复状态行、删保护卡、搜索框 placeholder、总览图表、命令绑定 |
| `UI/MainWindow.xaml.cs` | 主题切换日志、搜索框焦点处理、轮询生命周期 |
| `UI/CategoryFilter.cs`（新） | 可通知的分类项 |
| `UI/Charts.cs`（新） | 饼图几何计算、分类配色 |
| `App.xaml.cs` | `ApplyTheme()` 仪表化与修复 |
| `Core/LocalizationStrings.cs` | 补新增文案的中英文 |

### 5.2 状态结构化（Issue 2 的关键）

```csharp
// 不再直接保存翻译后的字符串
private string? _statusKey;
private object?[] _statusArgs = Array.Empty<object>();

public string StatusMessage => _statusKey is null
    ? string.Empty
    : L.F(_statusKey, _statusArgs);

private void SetStatus(string key, params object?[] args)
{
    _statusKey = key;
    _statusArgs = args;
    OnPropertyChanged(nameof(StatusMessage));
}
```

`OnLocalizationChanged()` 中只需 `OnPropertyChanged(nameof(StatusMessage))` 即可全文案跟随。

### 5.3 新增文案键（中英双份）

| 键 | 中文 | English |
|---|---|---|
| `msg.emptyNotScanned` | 点击右上角「重新扫描」开始 | Click "Rescan" in the top right to start |
| `msg.emptyCategory` | 此分类没有发现残留 | No leftovers found in this category |
| `msg.emptySearch` | 没有匹配的项目 | No matching items |
| `sum.breakdown` | 磁盘占用概况 | Space breakdown |
| `sum.selectedShare` | 其中已勾选 | Selected |
| `sum.insight` | 主要占用来自 {0}，共 {1}（{2}） | Mostly {0}: {1} ({2}) |
| `sum.other` | 其他 | Other |
| `steam.stateChanged` | 检测到 Steam 状态变化，已自动重新扫描 | Steam state changed, rescanned automatically |

---

## 6. 验收清单

| # | 验收项 | 方法 |
|---|---|---|
| 1 | 三态主题互切立即生效并持久化 | 手动 + 主题切换日志 |
| 2 | 中英互切无残留文字 | 手动逐页检查 |
| 3 | 全选/全不选/仅选安全项均生效 | 手动 + 底部计数联动 |
| 4 | 候选界面无重复状态文字 | 目视 |
| 5 | 搜索框提示正确显示与隐藏 | 手动 |
| 6 | Steam 状态 ≤3 秒内自动跟随并重扫 | 手动启停 Steam |
| 7 | 总览无安全保护卡片 | 目视 + README 校对 |
| 8 | 饼图/条形图数据与列表一致 | 交叉核对分类大小 |
| 9 | **无回归**：`--selftest` 27 项全通过 | `SteamScrup.exe --selftest` |
| 10 | **无绑定回归**：窗口正常启动、0 告警、TwoWay 源全可写 | `--exit-after=10000 --binding-report=` |

---

## 7. 风险

| 风险 | 应对 |
|---|---|
| 主题切换若根因是 WPF 字典替换不刷新，可能需要改用属性绑定方案 | 已备兜底方案（§3 Issue 1） |
| 每 3 秒自动重扫在某些机器上（784 GB 库）可能造成卡顿 | 重扫在工作线程执行；设为"状态变化时才重扫"而非定时重扫 |
| 饼图几何计算在极端数据（占比极小扇区）下出现视觉瑕疵 | <1% 合并为"其他"；对极小扇区设最小可视角度 |
| 改动 XAML 可能重新引入 TwoWay 绑定崩溃 | **每次改动后必须跑冒烟测试**（验收项 10） |

---

## 8. 不在本次范围

- 不做磁盘空间趋势历史记录
- 不做清理计划/自动化
- 不做自定义清理规则
- 不改动扫描与删除逻辑本身（仅界面与状态呈现）
- 不改动分类划分

---

## 9. 附：v1.0 已修复的绑定崩溃（防止回退）

WPF 中 `ProgressBar.Value`、`TextBox.Text`、`Run.Text`、`ToggleButton.IsChecked` 的元数据
`BindsTwoWayByDefault = true`。绑到只读属性上会在 `Show()` 期间抛
`InvalidOperationException`，**窗口完全起不来**，且 WPF 会一个接一个报错，掩盖真实数量。

| 处理 | 位置 |
|---|---|
| 不用 `ProgressBar`，改两列 `Grid`（`GridLength` 不会被写回） | 进度条 |
| 自定义标记扩展 `{ui:OneWayText ...}` | `<Run Text>` |
| 索引器 `Item[]` 提供**忽略写入的 setter** | `LocalizedViewModel` |
| 文本框/复选框只绑**可写**属性 | 设置页 |

改动 XAML 后务必执行：

```
SteamScrup.exe --exit-after=10000 --binding-report=report.txt
```

退出码 `0` = 启动正常，`2` = 启动抛异常。
