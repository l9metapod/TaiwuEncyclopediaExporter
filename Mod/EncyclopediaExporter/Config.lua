return {
	Title = "百晓册导出 · AI攻略助手",
	Version = "1.0.0.0",
	Author = "EncyclopediaExporter",
	Description = "把游戏内置百科(百晓册)一键导出成 Markdown 文档，让 AI 帮你做攻略。\n\n【有什么用】\n导出的 188 页文档涵盖全部 12 章节内容——门派、武学、战斗、产业、物品等，包含数据表格和属性一览。把这些资料交给 AI，它就能读懂太吾，帮你设计 Build、搭配出生效果、解答机制疑问、规划开局。\n\n【怎么用】\n1. 启用本 Mod，自动生成到 Mod\\EncyclopediaExporter\\output\\ 目录\n2. 用本地 AI 工具打开该目录直接提问；或用网页 AI 上传相关章节的文件夹\n3. 不需要付费会员、不需要 API，免费额度就够用\n4. 直接问：「这套功法怎么搭配」「开局选什么出身」「内息紊乱怎么解」\n\n【说明】\n· 当前版本导出百科正文文字+数据表格；悬浮卡片详情、单个功法完整说明等暂未包含，后续版本完善(详见README)\n· 游戏更新百科后再次启用会自动检测并重建\n· 部分复杂机制(如战斗结算)建议多方验证后再参考\n· 本工具只读取游戏明文数据，不修改任何游戏文件",
	FrontendPlugins = {
		[1] = "EncyclopediaExporter.dll",
	},
	Cover = "Cover.png",
	Source = 0,
	GameVersion = "1.0.1",
	Visibility = 1,
	DefaultSettings = { },
	UpdateLogList = {
		[1] = {
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
