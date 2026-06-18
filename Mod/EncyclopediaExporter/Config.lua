return {
	Title = "百晓册导出 · AI攻略助手",
	Version = "1.0.0.0",
	Author = "EncyclopediaExporter",
	Description = "把游戏内置百科(百晓册)一键导出成 Markdown 文档，让 AI 帮你做攻略。\n\n【有什么用】\n导出 188 页百科正文 + 1800 条悬浮词条详情，涵盖全部 12 章节——门派、武学、战斗、产业、物品等，含数据表格与功法属性。把这些资料交给 AI，它就能读懂太吾，帮你设计 Build、搭配出生效果、解答机制疑问、规划开局。\n\n【怎么用】\n1. 启用本 Mod，自动生成到游戏目录下的 EncyclopediaExporter_Output\\ 目录（独立于 Mod，上传工坊不会误传）\n2. 用本地 AI 工具打开该目录直接提问；或用网页 AI 上传相关章节的文件夹\n3. 不需要付费会员、不需要 API，免费额度就够用\n4. 直接问：「这套功法怎么搭配」「开局选什么出身」「内息紊乱怎么解」\n\n【已包含】\n· 188 页百科正文 + 100+ 张数据表格\n· 功法词条 924 条：正练/逆练效果、破体破气、命中分段、攻击范围、施展需要等（按游戏卡片还原）\n· 特性 766 条、出身特质 48 条、内力属性 36 条、招式 22 条\n【说明】\n· 武器/盔甲/书籍/药毒/建筑等更多词条类型持续完善中(详见README)\n· 游戏更新百科后再次启用会自动检测并重建\n· 重建用白名单清理，你写的笔记和配置(含.md)都不会被删，可放心当工作区用\n· 本工具只读取游戏明文数据，不修改任何游戏文件",
	FrontendPlugins = {
		[1] = "EncyclopediaExporter.dll",
	},
	Cover = "Cover.png",
	Source = 0,
	GameVersion = "1.0.1",
	Visibility = 1,
	DefaultSettings = {
		[1] = {
			SettingType = "Toggle",
			Key = "ForceReExport",
			DisplayName = "强制重新导出",
			Description = "打开后在游戏内点「应用」，会立即重新生成全部百科文档。生成完成后自动关闭。",
			GroupName = "导出",
			DefaultValue = false,
		},
	},
	SettingGroups = { [1] = "导出" },
	UpdateLogList = {
		[1] = {
			Timestamp = 1781753654,
			LogList = {
				[1] = "新增「强制重新导出」开关。\n· 在 Mod 设置里打开开关点应用，即可立即重新生成全部文档，无需重启游戏\n· 修复产物目录被删除后不会自动重建的问题\n· 输出目录：游戏目录下的 EncyclopediaExporter_Output/",
			},
		},
		[2] = {
			Timestamp = 1781725353,
			LogList = {
				[1] = "新增功法/特性等悬浮词条详情（共 1800 条），按游戏卡片还原属性。\n· 功法词条含正练/逆练效果、破体破气、命中分段、攻击范围、施展需要等\n· 修正破体破气公式、命中三段拆分、施展部位/蓄式消耗\n· 词条排版优化：聚合行 + 紧凑表格，Obsidian 悬浮查看更清晰\n· 输出目录独立于 Mod（避免上传工坊误传）\n· 重建改白名单清理：你写的笔记和配置不会被删",
			},
		},
		[3] = {
			Timestamp = 1781708330,
			LogList = {
				[1] = "首发布版本。一键导出《太吾绘卷》内置百科(百晓册)为 188 页 Markdown 文档，让 AI 帮你做攻略。\n· 覆盖全部 12 章节：门派、武学、战斗、产业、物品等\n· 含 100+ 张数据表格(品阶、属性、概率)\n· 按游戏原版式还原列表与缩进排版\n· 基于源文件哈希自动检测游戏更新并重建\n· 详情图分辨率提升至 1024×768\n· AI 使用说明采用通用说法，不绑定具体工具",
			},
		},
	},
	ChangeConfig = false,
	HasArchive = false,
	NeedRestartWhenSettingChanged = false,
	FileId = 3746603173,
	WorkshopCover = "Cover.png",
	TagList = {
		[1] = "Optimizations",
	},
}
