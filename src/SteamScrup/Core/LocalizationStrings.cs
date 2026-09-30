namespace SteamScrup.Core;

/// <summary>
/// All user-facing strings. Kept as plain dictionaries so no build-time code
/// generation is needed and both languages stay visible side by side in source.
/// </summary>
internal static class LocalizationStrings
{
    public static void LoadChinese(Dictionary<string, string> d)
    {
        // ---- app shell
        d["app.title"] = "SteamScrup — Steam 残留清理工具";
        d["app.shortTitle"] = "SteamScrup";
        d["app.subtitle"] = "预览后手动清理，绝不自动删除";

        // ---- navigation / categories
        d["cat.all"] = "全部";
        d["cat.summary"] = "总览";
        d["cat.common"] = "游戏目录残留";
        d["cat.shader"] = "Steam 着色器缓存";
        d["cat.workshop"] = "创意工坊缓存";
        d["cat.steamCache"] = "Steam 临时文件";
        d["cat.directX"] = "DirectX / 显卡缓存";
        d["cat.crash"] = "崩溃转储";
        d["cat.userdata"] = "userdata 存档（高级）";
        d["cat.settings"] = "设置";

        // ---- columns
        d["col.select"] = "选择";
        d["col.name"] = "名称";
        d["col.path"] = "路径";
        d["col.size"] = "占用大小";
        d["col.files"] = "文件数";
        d["col.modified"] = "修改时间";
        d["col.confidence"] = "风险等级";
        d["col.reason"] = "说明";

        // ---- confidence
        d["conf.safe"] = "安全";
        d["conf.likely"] = "较低风险";
        d["conf.review"] = "需确认";
        d["conf.protected"] = "受保护";

        // ---- reasons
        d["reason.emptyFolder"] = "空文件夹，没有内容";
        d["reason.stubOnly"] = "仅剩少量残留文件（如 steam_appid.txt）";
        d["reason.noManifest"] = "没有对应的游戏清单文件";
        d["reason.largeLeftover"] = "体积较大但没有游戏清单";
        d["reason.containsExecutable"] = "含可执行文件，可能是手动安装的软件";
        d["reason.containsSaveData"] = "含存档数据，删除会丢失进度";
        d["reason.containsMod"] = "含 MOD / 画质插件，可能是你手动安装的";
        d["reason.protectedSteamFolder"] = "Steam 自身功能目录，不可删除";
        d["reason.orphanShaderCache"] = "游戏已卸载，着色器缓存已失效";
        d["reason.installedShaderCache"] = "游戏仍在，缓存删除后会自动重建";
        d["reason.orphanWorkshop"] = "游戏已卸载，创意工坊内容已失效";
        d["reason.installedWorkshop"] = "游戏仍在，创意工坊内容会自动重建";
        d["reason.workshopScratch"] = "创意工坊下载临时文件";
        d["reason.orphanWorkshopMeta"] = "已失效的创意工坊元数据";
        d["reason.corruptChunks"] = "损坏的下载分块";
        d["reason.steamTemp"] = "Steam 更新临时文件";
        d["reason.steamDownloading"] = "未完成的下载缓存";
        d["reason.tempManifest"] = "残留的清单临时文件";
        d["reason.directXShaderCache"] = "DirectX 着色器缓存，会自动重建";
        d["reason.gpuShaderCache"] = "显卡着色器缓存，会自动重建";
        d["reason.crashData"] = "崩溃转储 / 日志";
        d["reason.activeUserData"] = "游戏仍在安装，属于活动存档，不可删除";
        d["reason.orphanUserDataWithSaves"] = "游戏已卸载，但里面是存档数据";
        d["reason.orphanUserDataEmpty"] = "游戏已卸载，且没有实际存档";

        // ---- toolbar
        d["tb.scan"] = "重新扫描";
        d["tb.scanning"] = "扫描中…";
        d["tb.selectAll"] = "全选";
        d["tb.selectNone"] = "全不选";
        d["tb.selectSafe"] = "仅选安全项";
        d["tb.selectSafeAll"] = "选择所有安全项";
        d["tb.selectSafeHint"] = "勾选全部分类中的所有安全项（不含需确认与受保护项）";
        d["tb.clearSelection"] = "取消选择";
        d["tb.clearSelectionHint"] = "取消当前列表中的全部选中项；底部的「取消所有选择项」作用范围相同";
        d["tb.clearAll"] = "取消所有选择项";
        d["tb.clearAllHint"] = "取消勾选全部候选项，包含被分类或搜索隐藏的项目";
        d["set.sidebarWidth"] = "侧边栏宽度";
        d["set.sidebarWidthHint"] = "拖动可调整宽度，双击恢复默认；设置会被记住";
        d["tb.clean"] = "清理所选";
        d["tb.refresh"] = "刷新";
        d["tb.openFolder"] = "打开所在位置";
        d["tb.stop"] = "停止";

        // ---- summary
        d["sum.title"] = "总览";
        d["sum.steamPath"] = "Steam 安装位置";
        d["sum.libraries"] = "库文件夹";
        d["sum.games"] = "已安装游戏";
        d["sum.candidates"] = "发现候选残留";
        d["sum.selected"] = "已勾选";
        d["sum.selectedSize"] = "已勾选大小";
        d["sum.notFound"] = "未找到";
        d["sum.noSteam"] = "未检测到 Steam 安装";
        d["sum.noSteamHint"] = "请确认 Steam 已安装，或在设置中手动指定 Steam 目录。";
        d["sum.libraryList"] = "检测到的库";
        d["sum.breakdown"] = "磁盘占用概况";
        d["sum.breakdownHint"] = "按分类统计全部候选残留的占用";
        d["sum.selectedShare"] = "其中已勾选";
        d["sum.insight"] = "主要占用来自「{0}」，共 {1}（{2}）";
        d["sum.other"] = "其他";
        d["sum.selection"] = "已选 {0} 项 · {1}";
        d["sum.legend"] = "分类";
        d["sum.bySize"] = "按占用排序";
        d["sum.noData"] = "还没有扫描数据，点击右上角「重新扫描」开始。";
        d["sum.percent"] = "占比";
        d["sum.size"] = "大小";
        d["sum.count"] = "条目";

        // ---- log
        d["log.title"] = "操作日志";
        d["log.directory"] = "日志输出目录";
        d["log.browse"] = "浏览…";
        d["log.open"] = "打开日志目录";
        d["log.current"] = "当前日志文件";
        d["log.view"] = "查看日志";

        // ---- settings
        d["set.title"] = "设置";
        d["set.appearance"] = "外观";
        d["set.theme"] = "主题";
        d["set.theme.system"] = "跟随系统";
        d["set.theme.light"] = "浅色";
        d["set.theme.dark"] = "深色";
        d["set.language"] = "语言";
        d["set.language.system"] = "跟随系统";
        d["set.language.zh"] = "简体中文";
        d["set.language.en"] = "English";
        d["set.behavior"] = "清理行为";
        d["set.deleteMode"] = "删除方式";
        d["set.deleteModeNote"] = "删除方式在每次清理前的确认框中选择：移至回收站（可还原）或永久删除（不可恢复）。此处不再保存默认值。";
        d["set.deleteMode.recycle"] = "移到回收站（推荐，可还原）";
        d["set.deleteMode.permanent"] = "永久删除（不可恢复）";
        d["set.recycleWarning"] = "注意：移到回收站不会立即释放磁盘空间，需要清空回收站后才真正腾出空间。";
        d["set.advanced"] = "高级选项";
        d["set.advanced.userdata"] = "显示 userdata 存档清理";
        d["set.advanced.userdataHint"] = "userdata 存放游戏存档。开启后可以看到已卸载游戏的存档并可删除，但删除后可能永久丢失进度。";
        d["set.userdataAlwaysOn"] = "userdata 存档清理始终可用。勾选其中项目并点击「清理所选」时，会单独提示存档可能丢失并由你确认风险。";
        d["dlg.userdataWarningTitle"] = "警告：所选内容包含游戏存档";
        d["dlg.userdataWarningBody"] = "所选项目中有 {0} 项属于 userdata 存档。删除后游戏进度可能永久丢失——即使走回收站也未必能恢复，部分存档会被 Steam Cloud 回写覆盖。";
        d["dlg.userdataAck"] = "我已知晓存档可能永久丢失，风险由我自行承担";
        d["dlg.userdataNeedsAck"] = "所选内容包含游戏存档，请先勾选上方确认项";
        d["set.steamPath"] = "Steam 目录";
        d["set.steamPath.auto"] = "自动检测";
        d["set.steamPath.browse"] = "手动指定…";
        d["set.paths"] = "路径与日志";
        d["set.dataLocation"] = "设置与日志存放位置";
        d["set.nameLookup"] = "游戏名称解析";
        d["set.nameLookupCached"] = "个名称已缓存（来源：Steam 官方接口）";
        d["set.nameLookupProgress"] = "正在联网解析游戏名：{0}/{1}";
        d["set.saved"] = "设置已保存";
        d["set.protection"] = "安全保护（不可关闭）";
        d["set.protection.appmanifest"] = "永不删除 .acf 清单文件";
        d["set.protection.autodelete"] = "永不自动删除，始终预览后手动确认";
        d["set.protection.network"] = "无任何联网行为、无遥测";
        d["set.protection.userdata"] = "默认不触碰 userdata 存档";

        // ---- confirmations
        d["dlg.confirmTitle"] = "确认清理";
        d["dlg.confirmBody"] = "即将清理 {0} 个项目，共 {1}。";
        d["dlg.confirmRecycle"] = "这些项目会被移到回收站，可以还原。";
        d["dlg.confirmPermanent"] = "警告：这些项目将被永久删除，无法恢复！";
        d["dlg.confirmRiskTitle"] = "高风险内容";
        d["dlg.confirmRiskBody"] = "所选项目中有 {0} 个被标记为「需确认」，可能包含存档、MOD 或手动安装的软件：";
        d["dlg.confirmRiskAck"] = "我已确认这些内容可以删除，风险由我自行承担";
        d["dlg.pickAction"] = "请选择处理方式";
        d["dlg.moveToRecycle"] = "移至回收站";
        d["dlg.deletePermanent"] = "永久删除";
        d["dlg.permanentNeedsAck"] = "永久删除不可恢复，请先勾选上方确认项";
        d["dlg.ok"] = "确定";
        d["dlg.cancel"] = "取消";
        d["dlg.yes"] = "继续";
        d["dlg.no"] = "取消";

        // ---- steam running
        d["steam.running"] = "检测到 Steam 正在运行，部分清理功能不可用。请先完全退出 Steam。";
        d["steam.notRunning"] = "Steam 未运行，可以安全清理";
        d["steam.stateChanged"] = "检测到 Steam 状态变化，已自动重新扫描…";
        d["steam.notRequiredHint"] = "所选项目无需退出 Steam";
        d["steam.blockedSelection"] = "检测到 Steam 正在运行，部分清理功能不可用：所选项目中有 {0} 项需要先完全退出 Steam。";
        d["steam.blockedNothing"] = "所选项目均无需退出 Steam，可直接清理。";

        // ---- results
        d["res.title"] = "清理结果";
        d["res.success"] = "成功";
        d["res.failed"] = "失败";
        d["res.summary"] = "成功 {0} 项，失败 {1} 项。";
        d["res.recycleHint"] = "如需真正释放空间，请清空回收站。";
        d["res.deniedHint"] = "部分项目因权限不足失败，可尝试以管理员身份运行。";
        d["res.detail"] = "详情";

        // ---- misc
        d["msg.scanComplete"] = "扫描完成，发现 {0} 个候选项目。";
        d["msg.scanCancelled"] = "扫描已取消。";
        d["msg.scanFailed"] = "扫描失败：{0}";
        d["msg.nothingSelected"] = "请先勾选要清理的项目。";
        d["msg.searchHint"] = "搜索名称或路径…";
        d["msg.error"] = "发生错误";
        d["msg.copyPath"] = "复制路径";
        d["msg.itemCount"] = "共 {0} 项";
        d["msg.fileCount"] = "{0} 个文件";
        d["msg.regenerates"] = "删除后自动重建";
        d["msg.revealInExplorer"] = "在资源管理器中显示";
        d["msg.emptyNotScanned"] = "点击右上角「重新扫描」开始";
        d["msg.emptyCategory"] = "此分类没有发现残留";
        d["msg.emptySearch"] = "没有匹配的项目";
        d["msg.rescanHint"] = "设置已更新，点击「重新扫描」以刷新列表。";
        d["msg.logDirFailed"] = "日志目录不可写，已保留原设置。";

        // ---- uninstall
        d["uninstall.title"] = "卸载 SteamScrup";
        d["uninstall.confirm"] = "确定要卸载吗？此操作会删除程序文件、快捷方式和设置。\n\n注意：不会删除任何已清理或未清理的 Steam 文件。";
        d["uninstall.done"] = "SteamScrup 已卸载。";
    }

    public static void LoadEnglish(Dictionary<string, string> d)
    {
        // ---- app shell
        d["app.title"] = "SteamScrup — Steam leftover cleaner";
        d["app.shortTitle"] = "SteamScrup";
        d["app.subtitle"] = "Preview first, clean manually. Never deletes on its own.";

        // ---- navigation / categories
        d["cat.all"] = "All";
        d["cat.summary"] = "Summary";
        d["cat.common"] = "Game folder leftovers";
        d["cat.shader"] = "Steam shader cache";
        d["cat.workshop"] = "Workshop cache";
        d["cat.steamCache"] = "Steam temp files";
        d["cat.directX"] = "DirectX / GPU cache";
        d["cat.crash"] = "Crash dumps";
        d["cat.userdata"] = "userdata saves (advanced)";
        d["cat.settings"] = "Settings";

        // ---- columns
        d["col.select"] = "Select";
        d["col.name"] = "Name";
        d["col.path"] = "Path";
        d["col.size"] = "Size";
        d["col.files"] = "Files";
        d["col.modified"] = "Modified";
        d["col.confidence"] = "Risk";
        d["col.reason"] = "Reason";

        // ---- confidence
        d["conf.safe"] = "Safe";
        d["conf.likely"] = "Low risk";
        d["conf.review"] = "Review";
        d["conf.protected"] = "Protected";

        // ---- reasons
        d["reason.emptyFolder"] = "Empty folder, nothing inside";
        d["reason.stubOnly"] = "Only a few leftover files (e.g. steam_appid.txt)";
        d["reason.noManifest"] = "No matching app manifest";
        d["reason.largeLeftover"] = "Large folder but no app manifest";
        d["reason.containsExecutable"] = "Contains executables, may be software you installed";
        d["reason.containsSaveData"] = "Contains save data, deleting loses progress";
        d["reason.containsMod"] = "Contains mods or graphics injectors you installed";
        d["reason.protectedSteamFolder"] = "Steam's own feature folder, never deletable";
        d["reason.orphanShaderCache"] = "Game uninstalled, shader cache is stale";
        d["reason.installedShaderCache"] = "Game still installed, cache rebuilds automatically";
        d["reason.orphanWorkshop"] = "Game uninstalled, workshop content is stale";
        d["reason.installedWorkshop"] = "Game still installed, workshop content rebuilds";
        d["reason.workshopScratch"] = "Workshop download scratch files";
        d["reason.orphanWorkshopMeta"] = "Stale workshop metadata";
        d["reason.corruptChunks"] = "Corrupted download chunks";
        d["reason.steamTemp"] = "Steam update scratch files";
        d["reason.steamDownloading"] = "Incomplete download cache";
        d["reason.tempManifest"] = "Leftover manifest temp file";
        d["reason.directXShaderCache"] = "DirectX shader cache, rebuilds automatically";
        d["reason.gpuShaderCache"] = "GPU shader cache, rebuilds automatically";
        d["reason.crashData"] = "Crash dumps / logs";
        d["reason.activeUserData"] = "Game still installed, active save data, never deletable";
        d["reason.orphanUserDataWithSaves"] = "Game uninstalled, but this holds save data";
        d["reason.orphanUserDataEmpty"] = "Game uninstalled and no actual saves";

        // ---- toolbar
        d["tb.scan"] = "Rescan";
        d["tb.scanning"] = "Scanning…";
        d["tb.selectAll"] = "Select all";
        d["tb.selectNone"] = "Select none";
        d["tb.selectSafe"] = "Select safe only";
        d["tb.selectSafeAll"] = "Select all safe items";
        d["tb.selectSafeHint"] = "Ticks every safe item in all categories (excludes Review and Protected)";
        d["tb.clearSelection"] = "Clear selection";
        d["tb.clearSelectionHint"] = "Unticks everything in the list; the footer's \"clear all selections\" does the same";
        d["tb.clearAll"] = "Clear all selections";
        d["tb.clearAllHint"] = "Unticks every candidate, including items hidden by the category filter or search";
        d["set.sidebarWidth"] = "Sidebar width";
        d["set.sidebarWidthHint"] = "Drag to resize, double-click to reset; the choice is remembered";
        d["tb.clean"] = "Clean selected";
        d["tb.refresh"] = "Refresh";
        d["tb.openFolder"] = "Open location";
        d["tb.stop"] = "Stop";

        // ---- summary
        d["sum.title"] = "Summary";
        d["sum.steamPath"] = "Steam location";
        d["sum.libraries"] = "Libraries";
        d["sum.games"] = "Installed games";
        d["sum.candidates"] = "Candidates found";
        d["sum.selected"] = "Selected";
        d["sum.selectedSize"] = "Selected size";
        d["sum.notFound"] = "not found";
        d["sum.noSteam"] = "Steam installation not detected";
        d["sum.noSteamHint"] = "Make sure Steam is installed, or set the Steam folder manually in Settings.";
        d["sum.libraryList"] = "Detected libraries";
        d["sum.breakdown"] = "Space breakdown";
        d["sum.breakdownHint"] = "All candidates grouped by category";
        d["sum.selectedShare"] = "Selected";
        d["sum.insight"] = "Mostly {0}: {1} ({2})";
        d["sum.other"] = "Other";
        d["sum.selection"] = "{0} selected · {1}";
        d["sum.legend"] = "Category";
        d["sum.bySize"] = "Sorted by size";
        d["sum.noData"] = "No scan data yet. Click \"Rescan\" in the top right to start.";
        d["sum.percent"] = "Share";
        d["sum.size"] = "Size";
        d["sum.count"] = "Items";

        // ---- log
        d["log.title"] = "Operation log";
        d["log.directory"] = "Log output folder";
        d["log.browse"] = "Browse…";
        d["log.open"] = "Open log folder";
        d["log.current"] = "Current log file";
        d["log.view"] = "View log";

        // ---- settings
        d["set.title"] = "Settings";
        d["set.appearance"] = "Appearance";
        d["set.theme"] = "Theme";
        d["set.theme.system"] = "Follow system";
        d["set.theme.light"] = "Light";
        d["set.theme.dark"] = "Dark";
        d["set.language"] = "Language";
        d["set.language.system"] = "Follow system";
        d["set.language.zh"] = "简体中文";
        d["set.language.en"] = "English";
        d["set.behavior"] = "Cleanup behaviour";
        d["set.deleteMode"] = "Deletion method";
        d["set.deleteModeNote"] = "The deletion method is chosen in the confirmation dialog before each cleanup: Move to Recycle Bin (restorable) or Delete permanently (unrecoverable). No default is stored here.";
        d["set.deleteMode.recycle"] = "Move to Recycle Bin (recommended, restorable)";
        d["set.deleteMode.permanent"] = "Delete permanently (unrecoverable)";
        d["set.recycleWarning"] = "Note: the Recycle Bin does not free disk space until you empty it.";
        d["set.advanced"] = "Advanced options";
        d["set.advanced.userdata"] = "Show userdata save cleanup";
        d["set.advanced.userdataHint"] = "userdata holds game saves. Enabling this reveals saves of uninstalled games; deleting them may permanently lose progress.";
        d["set.userdataAlwaysOn"] = "userdata save cleanup is always available. Selecting entries and pressing \"Clean selected\" raises a dedicated warning about losing saves.";
        d["dlg.userdataWarningTitle"] = "Warning: saves are included";
        d["dlg.userdataWarningBody"] = "{0} selected item(s) are userdata saves. Deleting them may permanently lose game progress - even through the Recycle Bin, because Steam Cloud can write some of them back.";
        d["dlg.userdataAck"] = "I understand the saves may be lost permanently and accept the risk";
        d["dlg.userdataNeedsAck"] = "The selection contains game saves; tick the box above first";
        d["set.steamPath"] = "Steam folder";
        d["set.steamPath.auto"] = "Auto-detect";
        d["set.steamPath.browse"] = "Choose manually…";
        d["set.paths"] = "Paths and logs";
        d["set.dataLocation"] = "Settings and log location";
        d["set.nameLookup"] = "Game name resolution";
        d["set.nameLookupCached"] = "name(s) cached (source: Steam public endpoint)";
        d["set.nameLookupProgress"] = "Resolving game names online: {0}/{1}";
        d["set.saved"] = "Settings saved";
        d["set.protection"] = "Safety guarantees (cannot be disabled)";
        d["set.protection.appmanifest"] = "Never deletes .acf manifest files";
        d["set.protection.autodelete"] = "Never deletes automatically; always preview and confirm";
        d["set.protection.network"] = "No network access, no telemetry";
        d["set.protection.userdata"] = "Never touches userdata saves by default";

        // ---- confirmations
        d["dlg.confirmTitle"] = "Confirm cleanup";
        d["dlg.confirmBody"] = "About to clean {0} item(s), totalling {1}.";
        d["dlg.confirmRecycle"] = "These items will be moved to the Recycle Bin and can be restored.";
        d["dlg.confirmPermanent"] = "Warning: these items will be permanently deleted!";
        d["dlg.confirmRiskTitle"] = "High-risk content";
        d["dlg.confirmRiskBody"] = "{0} selected item(s) are marked \"Review\" and may contain saves, mods or software you installed:";
        d["dlg.confirmRiskAck"] = "I have verified these can be deleted and accept the risk";
        d["dlg.pickAction"] = "Choose how to proceed";
        d["dlg.moveToRecycle"] = "Move to Recycle Bin";
        d["dlg.deletePermanent"] = "Delete permanently";
        d["dlg.permanentNeedsAck"] = "Permanent deletion cannot be undone; tick the box above first";
        d["dlg.ok"] = "OK";
        d["dlg.cancel"] = "Cancel";
        d["dlg.yes"] = "Continue";
        d["dlg.no"] = "Cancel";

        // ---- steam running
        d["steam.running"] = "Steam is running. Some cleanup features are unavailable; exit Steam completely first.";
        d["steam.notRunning"] = "Steam is not running, safe to clean";
        d["steam.stateChanged"] = "Steam state changed, rescanning automatically…";
        d["steam.notRequiredHint"] = "Selection does not need Steam closed";
        d["steam.blockedSelection"] = "Steam is running, so some cleanup features are unavailable: {0} selected item(s) need Steam fully closed first.";
        d["steam.blockedNothing"] = "Nothing selected needs Steam closed; cleaning can proceed.";

        // ---- results
        d["res.title"] = "Cleanup result";
        d["res.success"] = "Succeeded";
        d["res.failed"] = "Failed";
        d["res.summary"] = "{0} succeeded, {1} failed.";
        d["res.recycleHint"] = "Empty the Recycle Bin to actually free the disk space.";
        d["res.deniedHint"] = "Some items failed due to permissions; try running as administrator.";
        d["res.detail"] = "Details";

        // ---- misc
        d["msg.scanComplete"] = "Scan complete: {0} candidate(s) found.";
        d["msg.scanCancelled"] = "Scan cancelled.";
        d["msg.scanFailed"] = "Scan failed: {0}";
        d["msg.nothingSelected"] = "Select at least one item to clean.";
        d["msg.searchHint"] = "Search name or path…";
        d["msg.error"] = "Error";
        d["msg.copyPath"] = "Copy path";
        d["msg.itemCount"] = "{0} item(s)";
        d["msg.fileCount"] = "{0} files";
        d["msg.regenerates"] = "Rebuilds automatically";
        d["msg.revealInExplorer"] = "Show in Explorer";
        d["msg.emptyNotScanned"] = "Click \"Rescan\" in the top right to start";
        d["msg.emptyCategory"] = "No leftovers found in this category";
        d["msg.emptySearch"] = "No matching items";
        d["msg.rescanHint"] = "Settings updated. Click \"Rescan\" to refresh the list.";
        d["msg.logDirFailed"] = "That log folder is not writable; the previous setting was kept.";

        // ---- uninstall
        d["uninstall.title"] = "Uninstall SteamScrup";
        d["uninstall.confirm"] = "Uninstall now? Program files, shortcuts and settings will be removed.\n\nNo Steam files (cleaned or otherwise) are touched.";
        d["uninstall.done"] = "SteamScrup has been uninstalled.";
    }
}
