return {
	Title = "百晓册导出 · AI攻略助手",
	Version = "1.0.0.1",
	Author = "EncyclopediaExporter",
	Description = "把《太吾绘卷》内置百科(百晓册)一键导出成 Markdown 文档。覆盖 188 页百科正文 + 上万条物品/功法词条详情，按游戏内悬浮卡片版式逐字还原——数值可信，不是简单抓文本。\n\n交给 AI，它就能读懂太吾，帮你做攻略；用 Obsidian 打开，就是一本可搜索、可跳转的离线百科。\n\n【核心能力】\n· 完整覆盖：12 章节百科正文 + 13 种物品(武器/盔甲/衣装/宝物/工具/药毒/引子/书籍/杂物/食物/茶酒/代步/促织) + 功法/特性/出身特质/内力属性/招式\n· 按游戏卡片还原：物品属性(破甲、坚韧、命中、变招、破体破气等)按游戏实际显示还原，不是粗略抓取\n· 自动维护：游戏更新百科后再次启用会自动检测并重建，未变更时秒级跳过\n· 安全无侵入：只读取游戏明文数据，不修改任何游戏文件\n\n【怎么用(三步)】\n1. 启用本 Mod：自动生成到游戏目录下的 EncyclopediaExporter_Output\\ 文件夹\n2. 用 Obsidian 打开该文件夹：目录树/链接跳转/悬浮预览/全文搜索，体验接近游戏内百晓册\n3. 或交给 AI 提问：「这两件武器哪个更适合我」「这套功法怎么搭」「开局选什么出身」「内息紊乱怎么解」\n\n【关于数据】\n· 物品数值按百科环境(威力满发挥)显示，是物品的「模板」属性；游戏内你拿到的实际物品会受玩家威力影响，略有浮动\n· 重建只清理本工具自己生成的文件，你在输出目录里写的笔记不会被删，可以放心当工作区用\n· 可在 Mod 设置里关闭「添加颜色标签」，生成纯文字版，兼容 GitHub/论坛等所有渲染器",
	FrontendPlugins = {
		[1] = "EncyclopediaExporter.dll",
	},
	Cover = "Cover.png",
	Source = 0,
	GameVersion = "1.0.1",
	Visibility = 0,
	DefaultSettings = {
		[1] = {
			SettingType = "Toggle",
			Key = "ForceReExport",
			DisplayName = "强制重新导出",
			Description = "打开后在游戏内点「应用」，会立即重新生成全部百科文档。生成完成后自动关闭。",
			GroupName = "导出",
			DefaultValue = false,
		},
		[2] = {
			SettingType = "Toggle",
			Key = "CardProbe",
			DisplayName = "卡片布局探针（开发用）",
			Description = "打开后悬停任意武器，会把卡片实际显示的字段/文本/尺寸 dump 到 Player.log（搜 [CardProbe]）。用于核对导出内容与游戏卡片是否一致。普通玩家无需开启。",
			GroupName = "调试",
			DefaultValue = false,
		},
		[3] = {
			SettingType = "Toggle",
			Key = "IncludeColorTags",
			DisplayName = "添加颜色标签",
			Description = "开启时，导出的富文本会带 <span style=\"color:#xxx\"> 颜色标签（Obsidian 可显示颜色，但 GitHub 等纯 Markdown 渲染器会显示原始标签）。关闭则去除颜色标签，仅保留文字内容，兼容所有渲染器。改后需重新导出生效。",
			GroupName = "导出",
			DefaultValue = true,
		},
	},
	SettingGroups = {
		[1] = "导出",
		[2] = "调试",
	},
	UpdateLogList = {
		[1] = {
			Timestamp = 1781753654,
			LogList = {
				[1] = "新增 13 种物品词条详情，按游戏卡片版式还原属性。\n· 武器(18 种子类：剑/刀/长兵/瑶琴/暗器/箫笛/对刺/掌套/短杵/拂尘/长鞭/令符/机关/针匣/药霜/毒砂/神兵/动物)：招式/攻击距离/攻击耗时/破甲/坚韧/变招积蓄/追击概率/破体破气百分比/4 种命中属性百分比/自带毒(仅毒器)/发挥威力\n· 盔甲(破刃/外伤降低/坚韧/内伤降低)、衣装(魅力)、宝物(行囊)、工具(制作产物/技艺加成)\n· 药毒(服食效果/持续时间/毒素)、引子、书籍(内含功法/耐久)、杂物、食物、茶酒、代步、促织\n· 物品数值按百科环境(威力满发挥100)显示\n新增「添加颜色标签」开关：关闭可生成纯文字版，兼容 GitHub/论坛等不支持内联 HTML 的渲染器\n修复 <color=#pinkyellow> 等语义颜色标签无效的问题：改用运行时反射读取游戏 Colors.Instance 颜色表(188 条)",
			},
		},
		[2] = {
			Timestamp = 1781725353,
			LogList = {
				[1] = "新增「强制重新导出」开关。\n· 在 Mod 设置里打开开关点应用，即可立即重新生成全部文档，无需重启游戏\n· 修复产物目录被删除后不会自动重建的问题\n· 输出目录：游戏目录下的 EncyclopediaExporter_Output/",
			},
		},
		[3] = {
			Timestamp = 1781720000,
			LogList = {
				[1] = "新增功法/特性等悬浮词条详情（共 1800 条），按游戏卡片还原属性。\n· 功法词条含正练/逆练效果、破体破气、命中分段、攻击范围、施展需要等\n· 修正破体破气公式、命中三段拆分、施展部位/蓄式消耗\n· 词条排版优化：聚合行 + 紧凑表格，Obsidian 悬浮查看更清晰\n· 输出目录独立于 Mod（避免上传工坊误传）\n· 重建改白名单清理：你写的笔记和配置不会被删",
			},
		},
		[4] = {
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
