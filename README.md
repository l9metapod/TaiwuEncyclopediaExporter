# TaiwuEncyclopediaExporter

将《太吾绘卷》内置百科（百晓册）重建为结构化 **Markdown 文档**的游戏内 Mod。

启用后自动读取游戏百科源数据，按游戏实际渲染逻辑生成 188 个 Markdown 页面，方便交给 AI 做攻略，或用 Obsidian 等工具离线浏览。

> 面向玩家的完整说明见 [`Mod/EncyclopediaExporter/README.md`](Mod/EncyclopediaExporter/README.md)。

## 目录结构

```
TaiwuEncyclopediaExporter/
├── EncyclopediaExporter.Source/     # C# 源码工程（.NET Framework 4.8 类库）
│   ├── PluginEntry.cs               # TaiwuRemakePlugin 入口
│   ├── EncyclopediaExporter.csproj
│   ├── Models/                      # 数据模型 + 枚举映射
│   ├── Core/                        # 解析/构建/渲染/哈希缓存
│   ├── make_cover.py                # 封面生成脚本（Pillow）
│   └── make_preview.py              # 详情图生成脚本（Pillow）
└── Mod/EncyclopediaExporter/        # Mod 发布物
    ├── Config.lua                   # Mod 元数据
    ├── Cover.png                    # 封面
    ├── README.md                    # 玩家说明
    ├── Settings.Lua
    ├── Plugins/EncyclopediaExporter.dll   # 预编译 DLL
    └── preview/                     # 创意工坊详情图
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

所有解析规则均通过反编译游戏程序集（`Assembly-CSharp.dll` 的 `Game.Views.Encyclopedia` 命名空间）确认，与游戏实际渲染逻辑一致：

| 机制 | 来源 |
|---|---|
| 节点层级树 | `EncyclopediaDataManager.Init` + `EncyclopediaContentItem.ProcessIndex` |
| 布局（列表/缩进） | `Element.GetLayoutPadding` + `SingleTextElement` |
| 富文本转义 | `Extentions.ParseText` / `ColorReplace` |
| 表格渲染 | `TableElement.InitData` |
| 链接解析 | `LinkElement`（HyperLink→跳页 / Tips→保留文字）|

## 版本管理

基于源 TSV 文件的 SHA256 哈希缓存：游戏更新百科内容后自动检测并重建，未变更时秒级跳过。

## 许可

本工具仅读取游戏的明文数据文件并转换格式，不修改任何游戏文件。反编译的游戏源码不在本仓库内。
