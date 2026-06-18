# TaiwuEncyclopediaExporter

将《太吾绘卷》内置百科（百晓册）重建为结构化 **Markdown 文档**的游戏内 Mod。

启用后自动读取游戏百科源数据与运行时配置，生成 **188 个百科页面 + 上万条悬浮词条详情**——覆盖功法、特性、出身特质、内力属性、招式，以及武器/盔甲/衣装/宝物/工具/药毒/引子/书籍/杂物/食物/茶酒/代步/促织共 13 种物品类型的完整属性卡片，按游戏实际显示版式还原，方便交给 AI 做攻略，或用 Obsidian 等工具离线浏览。

> 输出独立于 Mod 目录（`<游戏根>/EncyclopediaExporter_Output/`），通过游戏内置编辑器上传创意工坊时不会被误打包。

> 面向玩家的完整说明见 [`Mod/EncyclopediaExporter/README.md`](Mod/EncyclopediaExporter/README.md)。

## 当前实现范围

### 百科正文（`EncyclopediaBuilder`）
- 全部 12 章节、188 个 Heading3 页面
- 节点层级树、列表/缩进排版、富文本（颜色 / 链接 / 斜体 / 下划线）、数据表格
- 跨页相对链接 + Heading4 锚点
- 正文里的 `<link="功法-X">` 等链接会指向已生成的词条文件

### 悬浮词条库（`TipEntryResolver`，共 18 种 `ReferenceInsertType`：13 种物品 + 5 种基础）

| 类别 | 子目录 | 渲染来源 | 说明 |
|---|---|---|---|
| 功法 | `词条/功法/` | `CombatSkill` | 品阶/类型/五行/门派、正练逆练、破体破气、命中分段、范围、施展需要、运功路径 |
| 特性 | `词条/特性/` | `CharacterFeature` | 类型、属性加成、效果 |
| 出身特质 | `词条/出身特质/` | `ProtagonistFeature` | 名称 + 描述 |
| 内力属性 | `词条/内力属性/` | `NeiliType` | 名称 + 描述 |
| 招式 | `词条/招式/` | `TrickType` | 名称 + 描述 |
| 武器（18 子类） | `词条/武器/` | `WeaponItem` | 招式/距离/耗时/破甲/坚韧/变招/追击/破体破气%/4 种命中%/自带毒/威力 |
| 盔甲 | `词条/盔甲/` | `ArmorItem` | 破刃/外伤降低/坚韧/内伤降低/威力 |
| 衣装 | `词条/衣装/` | `ClothingItem` | 魅力（男/女）|
| 宝物 | `词条/宝物/` | `AccessoryItem` | 行囊、属性加成 |
| 工具 | `词条/工具/` | `CraftTool` | 制作产物、技艺加成 |
| 药毒 | `词条/药毒/` | `MedicineItem` | 服食效果、持续时间、毒素 |
| 引子 | `词条/引子/` | `MaterialItem` | 子类、属性 |
| 书籍 | `词条/书籍/` | `SkillBookItem` | 内含功法、耐久 |
| 杂物 | `词条/杂物/` | `MiscItem` | 子类、描述 |
| 食物 | `词条/食物/` | `FoodItem` | 子类、效果 |
| 茶酒 | `词条/茶酒/` | `TeaWineItem` | 子类、效果 |
| 代步 | `词条/代步/` | `CarrierItem` | 子类、属性 |
| 促织 | `词条/促织/` | `CricketItem` | 品级、部位（精细排版待完善）|

物品数值按**百科环境**还原：游戏构造 `new ItemDisplayData(itemType, templateId)` 时 `PowerInfo.Default`（Power=100），所有"威力衍生"字段 = 模板字段本身。详见 [`docs/CARD_LAYOUT_PLAN.md`](docs/CARD_LAYOUT_PLAN.md)。

### 颜色与渲染（`MarkdownRenderer` + `DataParser`）
- `<color=#hex>` / `<color=name>` 富文本 → `<span style="color:#hex">`（Obsidian 友好）
- 颜色表由运行时反射 `Colors.Instance.PresetColorNames/PresetColors` 读取（188 条），回退到内置 `KnownColors`
- Mod 设置「添加颜色标签」开关：开 = 保留 span（默认），关 = 纯文本（兼容 GitHub/纯 Markdown）
- `<link="X">` → 经 `Reference` 解析为百科页跳转或词条文件链接

### 调试工具（`CardProbe`）
- Harmony Postfix 挂钩 16 种 `TooltipXxx.Refresh` + `DumpColorsTable`
- 延迟 dump（0.3s + 2 帧）等异步数据就绪，2 秒去重
- 输出到 `Player.log`（搜 `[CardProbe]`），样本备份在 `_probe/samples/`

## 目录结构

```
TaiwuEncyclopediaExporter/
├── EncyclopediaExporter.Source/     # C# 源码工程（.NET Framework 4.8 类库）
│   ├── PluginEntry.cs               # TaiwuRemakePlugin 入口
│   ├── EncyclopediaExporter.csproj
│   ├── Models/                      # 数据模型 + 枚举映射 + TipTypeConfig
│   ├── Core/                        # 解析/构建/渲染/哈希缓存/探针
│   ├── make_cover.py                # 封面生成脚本（Pillow）
│   └── make_preview.py              # 详情图生成脚本（Pillow）
├── Mod/EncyclopediaExporter/        # Mod 发布物
│   ├── Config.lua                   # Mod 元数据 + 设置项定义
│   ├── Cover.png                    # 封面
│   ├── README.md                    # 玩家说明
│   ├── Settings.Lua
│   ├── Plugins/EncyclopediaExporter.dll   # 预编译 DLL
│   └── preview/                     # 创意工坊详情图
└── docs/CARD_LAYOUT_PLAN.md         # 物品卡片字段布局规划（实测 + 反编译核对）
```

## 编译

需要 Visual Studio 2022（含「.NET 桌面开发」工作负载）。

`EncyclopediaExporter.csproj` 通过 `ManagedDir` 属性引用游戏程序集（`Assembly-CSharp.dll` 等）。首次编译前，在工程里把 `ManagedDir` 指向本机的游戏 `Managed` 目录：

```
<The Scroll Of Taiwu>/The Scroll Of Taiwu_Data/Managed/
```

编译后，csproj 的 `CopyToModAfterBuild` target 会自动把 DLL 复制到 `Mod/EncyclopediaExporter/Plugins/`。

游戏根目录下的 `build.bat`（不在本仓库内）封装了这个流程，会自动查找 MSBuild 并编译。

## 技术说明

所有解析规则均通过反编译游戏程序集（`Assembly-CSharp.dll` 的 `Game.Views.Encyclopedia` / `Game.Views.MouseTips.Item` 命名空间）+ Harmony 探针对游戏实际卡片的 dump 双重确认，与游戏实际渲染逻辑一致：

| 机制 | 来源 |
|---|---|
| 节点层级树 | `EncyclopediaDataManager.Init` + `EncyclopediaContentItem.ProcessIndex` |
| 布局（列表/缩进） | `Element.GetLayoutPadding` + `SingleTextElement` |
| 富文本转义 | `Extentions.ParseText` / `ColorReplace`（运行时查 `Colors.Instance`） |
| 表格渲染 | `TableElement.InitData` |
| 链接解析 | `LinkElement`（HyperLink→跳页 / Tips→词条文件）|
| 物品卡片字段 | `TooltipWeapon/Armor/...Refresh` + `ItemDisplayData`（百科环境威力满发挥） |

## 版本管理

基于源 TSV 文件的 SHA256 哈希缓存：游戏更新百科内容后自动检测并重建，未变更时秒级跳过。

## 许可

本工具仅读取游戏的明文数据文件并转换格式，不修改任何游戏文件。反编译的游戏源码不在本仓库内。
