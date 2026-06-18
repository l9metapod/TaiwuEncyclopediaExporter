using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using EncyclopediaExporter.Models;

namespace EncyclopediaExporter.Core
{
    /// <summary>
    /// 百科树构建与 Markdown 生成。
    /// 建树/聚合/渲染逻辑移植自已验证的 Python 实现，
    /// 节点层级关系来自反编译 EncyclopediaDataManager.Init + EncyclopediaContentItem.ProcessIndex。
    /// </summary>
    public class EncyclopediaBuilder
    {
        private readonly string _assetsDir;
        private readonly string _outputDir;

        private List<ContentItem> _items;
        private Dictionary<string, ContentItem> _itemsByKey;
        private Dictionary<string, ReferenceItem> _references;
        private HashSet<string> _fourKeys;
        private Dictionary<string, string> _keyToPage;     // Heading3 key -> 输出相对路径(无扩展名)
        private Dictionary<string, string> _keyToTwo;      // Heading2 key -> 首个 Three 页
        private MarkdownRenderer _md;
        private TableRenderer _tables;

        // Heading4 key -> (所在页相对路径, 锚点)
        private Dictionary<string, Tuple<string, string>> _keyToAnchor;

        // Reference target(百科链接目标) -> Tips 词条文件的相对路径(如 词条/功法/沛然诀.md)
        // 在 BuildTipLibrary 时填充，供页面渲染时把 <link="功法-沛然诀"> 指向词条文件
        private Dictionary<string, string> _tipFileMap;

        // 本次将要（重新）生成的 .md 文件相对路径集合（正斜杠，相对 _outputDir）。
        // CleanOutput 只删这个集合里的文件，其余（玩家的 .md 笔记、.obsidian 等）一律保留。
        private HashSet<string> _generatedPaths;

        public EncyclopediaBuilder(string assetsDir, string outputDir)
        {
            _assetsDir = assetsDir;
            _outputDir = outputDir;
        }

        /// <summary>执行重建，返回生成的页面数。</summary>
        public int Build()
        {
            // 1. 解析数据
            _items = DataParser.ParseContent(Path.Combine(_assetsDir, "EncyclopediaContent.tsv"));
            _references = DataParser.ParseReference(Path.Combine(_assetsDir, "EncyclopediaReference.tsv"));
            _itemsByKey = new Dictionary<string, ContentItem>();
            foreach (var it in _items) _itemsByKey[it.Key] = it;
            _fourKeys = new HashSet<string>();
            foreach (var it in _items) if (it.Layer == ContentLayer.Four) _fourKeys.Add(it.Key);

            // 2. 确定页面(每个 Heading3 一页)
            var pages = new List<ContentItem>();
            foreach (var it in _items)
                if (it.Layer == ContentLayer.Three) pages.Add(it);

            // 3. 建链接映射
            BuildLinkMaps(pages);

            // 4. 渲染器
            _tables = new TableRenderer(_assetsDir, null);
            _md = new MarkdownRenderer(MakeResolver(null));

            // 5. 计算本次将要生成的所有 .md 路径（白名单），用于精确清理
            //    只删本工具会重新生成的文件，玩家自己写的 .md 笔记一律保留
            _generatedPaths = new HashSet<string>();
            foreach (var page in pages)
                _generatedPaths.Add(GetPageRelPath(page, NonEmptyTitles(page, 3)) + ".md");
            CollectTipPaths();

            // 6. 清理输出目录（仅删除白名单内的 .md + 清空空目录）
            CleanOutput();

            // 7. 生成悬浮信息词条库（功法/特性/武器等详情，从运行时配置读取）
            //    先于页面生成，以便页面里的 <link="功法-X"> 能指向已生成的词条文件
            int tips = BuildTipLibrary();

            // 8. 逐页生成
            int written = 0;
            foreach (var page in pages)
            {
                WritePage(page);
                written++;
            }

            return written + tips;
        }

        /// <summary>
        /// 遍历 Reference 表的核心 Tips 类型，为每个有效条目生成词条文件到 output/词条/&lt;类型&gt;/。
        /// 数据从运行时 Config 单例读取（Initialize 时已加载），文字+数值一并导出。
        /// 返回生成的词条数。
        /// </summary>
        private int BuildTipLibrary()
        {
            int written = 0;
            _tipFileMap = new Dictionary<string, string>();
            // 按类型分组统计，用于日志
            var counts = new Dictionary<ReferenceInsertType, int>();

            foreach (var pair in _references)
            {
                string target = pair.Key;        // 百科链接目标（col0 id）
                var refItem = pair.Value;
                if (!TipTypeConfig.EnumMap.TryGetValue(refItem.InsertType, out var meta))
                    continue; // 非核心类型，跳过

                // 解析 ID：param 通常是数字；促织Tips 在 Params[0]（本期未支持促织）
                int id;
                if (!TryParseTipId(refItem, out id))
                    continue;

                try
                {
                    var result = TipEntryResolver.Render(refItem.InsertType, id);
                    if (result == null) continue;

                    string fileName = result.Item1;
                    string body = result.Item2;
                    if (string.IsNullOrEmpty(fileName)) continue;

                    string relDir = "词条/" + meta.SubDir;
                    string dir = Path.Combine(_outputDir, relDir.Replace('/', Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(dir);
                    string relPath = relDir + "/" + fileName + ".md";
                    string path = Path.Combine(_outputDir, relPath.Replace('/', Path.DirectorySeparatorChar));

                    // 记录链接映射（用正斜杠相对路径，供 MakeResolver 计算页面相对链接）
                    // 同名词条去重：已存在则跳过文件写入，但仍记录链接（指向首个）
                    if (!_tipFileMap.ContainsKey(target))
                        _tipFileMap[target] = relPath;
                    if (File.Exists(path))
                        continue;

                    File.WriteAllText(path, body, new UTF8Encoding(false));
                    written++;
                    if (!counts.ContainsKey(refItem.InsertType)) counts[refItem.InsertType] = 0;
                    counts[refItem.InsertType]++;
                }
                catch (Exception ex)
                {
                    // 单个词条失败不影响整体
                    UnityEngine.Debug.LogWarning("[EncyclopediaExporter] 词条生成失败: "
                        + refItem.InsertType + " id=" + id + " " + ex.Message);
                }
            }

            foreach (var kv in counts)
                UnityEngine.Debug.Log("[EncyclopediaExporter] 词条 " + kv.Key + ": " + kv.Value + " 条");

            return written;
        }

        /// <summary>
        /// 解析 Reference 的 Tips ID。
        /// 默认从 Param(col2) 取数字；促织Tips 从 Params[0] 的 {n,} 取。
        /// </summary>
        private static bool TryParseTipId(ReferenceItem refItem, out int id)
        {
            id = -1;
            if (refItem.InsertType == ReferenceInsertType.CricketTips)
            {
                // 促织：id 在 Params[0]，格式 "{n,}"。提取花括号内逗号前的数字。
                var ps = refItem.Params;
                if (ps == null || ps.Count == 0) return false;
                string raw = ps[0];
                int start = raw.IndexOf('{');
                int comma = raw.IndexOf(',');
                if (start >= 0 && comma > start)
                {
                    string num = raw.Substring(start + 1, comma - start - 1);
                    return int.TryParse(num, out id);
                }
                return false;
            }
            string raw2 = refItem.Param?.Trim();
            if (string.IsNullOrEmpty(raw2)) return false;
            // param 可能是数字 ID，也可能是字符串名（部分特性）——本期仅支持数字
            return int.TryParse(raw2, out id);
        }

        /// <summary>
        /// 清理输出目录：仅删除白名单（本次将生成的 .md）内的文件，并移除因此变空的目录。
        /// 保留用户放入的任何其它内容——包括玩家自己写的 .md 笔记、.obsidian / .claude / .git 等，
        /// 避免游戏更新触发重建后丢失用户数据。
        /// </summary>
        private void CleanOutput()
        {
            if (!Directory.Exists(_outputDir))
            {
                Directory.CreateDirectory(_outputDir);
                return;
            }

            if (_generatedPaths == null || _generatedPaths.Count == 0)
            {
                // 无白名单（异常情况）则不删任何东西，避免误伤
                return;
            }

            // 构建白名单的本地路径集合（用 OS 分隔符比较）
            var whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var rel in _generatedPaths)
                whitelist.Add(rel.Replace('/', Path.DirectorySeparatorChar));

            // 只删除白名单内的 .md 文件
            foreach (var md in Directory.EnumerateFiles(_outputDir, "*.md", SearchOption.AllDirectories))
            {
                string rel = md.Substring(_outputDir.Length).TrimStart(Path.DirectorySeparatorChar, '/', '\\');
                if (whitelist.Contains(rel))
                {
                    try { File.Delete(md); } catch { /* 忽略单个文件删除失败 */ }
                }
            }

            // 自底向上移除变空的目录，但保留任何含内容的目录（保护用户文件）
            PruneEmptyDirs(_outputDir);
        }

        /// <summary>
        /// 预先收集本次将生成的所有 Tips 词条相对路径（不写文件）。
        /// 供 CleanOutput 的白名单使用，确保只删本工具自己产出的词条，不碰玩家笔记。
        /// </summary>
        private void CollectTipPaths()
        {
            foreach (var pair in _references)
            {
                var refItem = pair.Value;
                if (!TipTypeConfig.EnumMap.TryGetValue(refItem.InsertType, out var meta))
                    continue;
                int id;
                if (!TryParseTipId(refItem, out id))
                    continue;
                try
                {
                    var result = TipEntryResolver.Render(refItem.InsertType, id);
                    if (result == null) continue;
                    string fileName = result.Item1;
                    if (string.IsNullOrEmpty(fileName)) continue;
                    string relPath = "词条/" + meta.SubDir + "/" + fileName + ".md";
                    _generatedPaths.Add(relPath);
                }
                catch { /* 收集阶段忽略单个失败 */ }
            }
        }

        /// <summary>
        /// 递归移除空目录。先处理子目录，再判断当前目录：若已无任何子目录且无任何文件，则删除。
        /// 只要目录内还剩任何一个文件（含用户的 .obsidian 等），整棵子树都会被保留。
        /// 注意：_outputDir 自身不会被删除（它的父目录不属于本工具管辖）。
        /// </summary>
        private void PruneEmptyDirs(string dir)
        {
            if (!Directory.Exists(dir)) return;
            bool isRoot = string.Equals(dir, _outputDir, StringComparison.OrdinalIgnoreCase);

            // 无论是否为根，都要递归清理下面的空目录
            foreach (var sub in SafeEnumerateDirs(dir))
                PruneEmptyDirs(sub);

            // _outputDir 自身永不删除（父目录不属于本工具管辖）；其它目录若变空则移除
            if (isRoot) return;

            if (Directory.GetFileSystemEntries(dir).Length == 0)
            {
                try { Directory.Delete(dir, false); } catch { /* 非空或占用则保留 */ }
            }
        }

        private static List<string> SafeEnumerateDirs(string dir)
        {
            var list = new List<string>();
            try { list.AddRange(Directory.GetDirectories(dir)); } catch { }
            return list;
        }

        private void BuildLinkMaps(List<ContentItem> pages)
        {
            _keyToPage = new Dictionary<string, string>();
            foreach (var p in pages)
            {
                var titles = NonEmptyTitles(p, 3);
                _keyToPage[p.Key] = GetPageRelPath(p, titles);
            }

            // Heading4 -> 父 Three 页的锚点
            _keyToAnchor = new Dictionary<string, Tuple<string, string>>();
            foreach (var it in _items)
            {
                if (it.Layer == ContentLayer.Four && _keyToPage.ContainsKey(it.ParentKey3))
                {
                    var title4 = it.Titles.Length > 3 ? it.Titles[3] : "";
                    if (!string.IsNullOrEmpty(title4))
                    {
                        var anchor = Regex.Replace(title4, @"[^\w\u4e00-\u9fff\s-]", "")
                                          .Trim().Replace(" ", "-").ToLowerInvariant();
                        _keyToAnchor[it.Key] = Tuple.Create(_keyToPage[it.ParentKey3], anchor);
                    }
                }
            }

            // Heading2 -> 首个子 Three 页
            _keyToTwo = new Dictionary<string, string>();
            foreach (var it in _items)
            {
                if (it.Layer == ContentLayer.Three && !string.IsNullOrEmpty(it.ParentKey2)
                    && !_keyToTwo.ContainsKey(it.ParentKey2)
                    && _keyToPage.ContainsKey(it.Key))
                {
                    _keyToTwo[it.ParentKey2] = _keyToPage[it.Key];
                }
            }
        }

        private List<string> NonEmptyTitles(ContentItem it, int count)
        {
            var list = new List<string>();
            for (int i = 0; i < count && i < it.Titles.Length; i++)
                if (!string.IsNullOrEmpty(it.Titles[i])) list.Add(it.Titles[i]);
            return list;
        }

        private string GetPageRelPath(ContentItem page, List<string> titles)
        {
            var parts = new List<string>();
            foreach (var t in titles)
            {
                var sn = DataParser.SafeName(t);
                if (!string.IsNullOrEmpty(sn)) parts.Add(sn);
            }
            if (parts.Count == 0) parts.Add("misc");
            return string.Join("/", parts) + "/" + DataParser.SafeName(page.Key);
        }

        /// <summary>为指定输出目录创建 link resolver。</summary>
        private Func<string, ResolveResult> MakeResolver(string currentPageRelNoExt)
        {
            return target =>
            {
                if (!_references.TryGetValue(target, out var refItem))
                    return null;
                if (refItem.InsertType == ReferenceInsertType.HyperLink)
                {
                    var tk = refItem.Param;
                    string pageRel;
                    if (_keyToPage.TryGetValue(tk, out pageRel))
                        return Result(currentPageRelNoExt, pageRel);
                    if (_keyToAnchor.TryGetValue(tk, out var anchor))
                        return Result(currentPageRelNoExt, anchor.Item1, anchor.Item2);
                    if (_keyToTwo.TryGetValue(tk, out pageRel))
                        return Result(currentPageRelNoExt, pageRel);
                    return new ResolveResult { Type = ResolveType.Unresolved };
                }
                if (refItem.InsertType == ReferenceInsertType.ConfigTable)
                    return new ResolveResult { Type = ResolveType.Table, Param = refItem.Param };
                if (refItem.InsertType == ReferenceInsertType.Figure)
                    return new ResolveResult { Type = ResolveType.Image, Param = refItem.Param };
                // Tips：若该链接目标有对应的词条文件，返回指向它的页面链接
                if (_tipFileMap != null && _tipFileMap.TryGetValue(target, out var tipRel))
                    return Result(currentPageRelNoExt, tipRel.Substring(0, tipRel.Length - 3)); // 去掉 .md
                return new ResolveResult { Type = ResolveType.Tip, Param = refItem.Param };
            };
        }

        // 计算 currentPage 到 targetPage 的相对链接
        private ResolveResult Result(string currentPageRelNoExt, string targetPageRelNoExt, string anchor = null)
        {
            if (currentPageRelNoExt == null)
                return new ResolveResult { Type = ResolveType.Page, Link = targetPageRelNoExt + ".md" };
            // 用字符串方式算相对路径(避免 Uri 对中文的问题)
            var fromParts = currentPageRelNoExt.Split('/');
            var toParts = targetPageRelNoExt.Split('/');
            int common = 0;
            while (common < fromParts.Length - 1 && common < toParts.Length
                   && fromParts[common] == toParts[common]) common++;
            var ups = fromParts.Length - 1 - common;
            var sb = new StringBuilder();
            for (int i = 0; i < ups; i++) sb.Append("../");
            for (int i = common; i < toParts.Length; i++)
            {
                if (i > common) sb.Append("/");
                sb.Append(toParts[i]);
            }
            sb.Append(".md");
            if (!string.IsNullOrEmpty(anchor)) sb.Append("#").Append(anchor);
            return new ResolveResult { Type = ResolveType.Page, Link = sb.ToString() };
        }

        private void WritePage(ContentItem page)
        {
            var titles = NonEmptyTitles(page, 3);
            var pageRel = GetPageRelPath(page, titles);
            // 每页用独立的 resolver(因为相对路径依赖当前页)
            var pageMd = new MarkdownRenderer(MakeResolver(pageRel));
            _tables = new TableRenderer(_assetsDir, pageMd);
            _md = pageMd;

            var absPath = Path.Combine(_outputDir, pageRel.Replace('/', Path.DirectorySeparatorChar) + ".md");
            Directory.CreateDirectory(Path.GetDirectoryName(absPath));

            var sb = new StringBuilder();
            sb.AppendLine("# " + (titles.Count > 0 ? titles[titles.Count - 1] : page.Key));
            sb.AppendLine();
            sb.AppendLine("> 百科 Key: `" + page.Key + "`");
            if (titles.Count > 1)
                sb.AppendLine("> 层级: " + string.Join(" / ", titles));
            sb.AppendLine();

            RenderSubtree(page.Key, pageMd, sb);

            File.WriteAllText(absPath, sb.ToString(), new UTF8Encoding(false));
        }

        /// <summary>递归渲染节点子树。Three 渲染内容子+Four子标题; Four 渲染内容子。</summary>
        private void RenderSubtree(string nodeKey, MarkdownRenderer md, StringBuilder sb)
        {
            if (!_itemsByKey.TryGetValue(nodeKey, out var node)) return;
            if (node.Layer == ContentLayer.Three)
            {
                foreach (var ck in ChildrenOf(nodeKey, ContentLayer.Three))
                {
                    if (!_itemsByKey.TryGetValue(ck, out var child)) continue;
                    if (child.Layer == ContentLayer.Content)
                    {
                        foreach (var line in RenderContentItem(child, md))
                            sb.AppendLine(line);
                        sb.AppendLine();
                    }
                    else if (child.Layer == ContentLayer.Four)
                    {
                        var title4 = child.Titles.Length > 3 ? child.Titles[3] : child.Key;
                        if (!string.IsNullOrEmpty(title4))
                        {
                            sb.AppendLine();
                            sb.AppendLine("### " + title4);
                            sb.AppendLine();
                        }
                        RenderSubtree(ck, md, sb);
                    }
                }
            }
            else if (node.Layer == ContentLayer.Four)
            {
                foreach (var ck in ChildrenOf(nodeKey, ContentLayer.Four))
                {
                    if (!_itemsByKey.TryGetValue(ck, out var child)) continue;
                    foreach (var line in RenderContentItem(child, md))
                        sb.AppendLine(line);
                    sb.AppendLine();
                }
            }
        }

        /// <summary>节点的直接子 key 列表（保持顺序）。</summary>
        private List<string> ChildrenOf(string parentKey, ContentLayer parentLayer)
        {
            var result = new List<string>();
            foreach (var it in _items)
            {
                if (parentLayer == ContentLayer.Three)
                {
                    if (it.Layer == ContentLayer.Four && it.ParentKey3 == parentKey)
                        result.Add(it.Key);
                    else if (it.Layer == ContentLayer.Content
                             && !_fourKeys.Contains(it.ParentKey4)
                             && it.ParentKey3 == parentKey)
                        result.Add(it.Key);
                }
                else if (parentLayer == ContentLayer.Four)
                {
                    if (it.Layer == ContentLayer.Content && it.ParentKey4 == parentKey)
                        result.Add(it.Key);
                }
            }
            return result;
        }

        /// <summary>渲染单个内容节点。</summary>
        private List<string> RenderContentItem(ContentItem it, MarkdownRenderer md)
        {
            var lines = new List<string>();
            var content = it.Content;
            var inserts = it.Inserts;

            // {N} 占位符 → 插入对应引用内容
            if (Regex.IsMatch(content.Trim(), @"^\{[0-9]+\}$") && inserts.Count > 0)
            {
                int idx;
                if (int.TryParse(content.Trim().Substring(1, content.Trim().Length - 2), out idx)
                    && idx < inserts.Count)
                {
                    var refId = inserts[idx];
                    if (_references.TryGetValue(refId, out var refItem))
                    {
                        if (refItem.InsertType == ReferenceInsertType.ConfigTable)
                            lines.Add(_tables.Render(refItem));
                        else if (refItem.InsertType == ReferenceInsertType.Figure)
                            lines.Add("*（图片：" + (refItem.Param ?? "") + "）*");
                        else if (refItem.InsertType == ReferenceInsertType.TableCollection)
                        {
                            // 表格集合（游戏里可切 tab 的多 sheet 表）：
                            // Params = 子表 ConfigTable id 列表，Desc = 每个 sheet 的 tab 标签名（一一对应）
                            // 旧实现只渲染 Params[0]，导致其余 sheet 全丢（影响兵器/护具/功法/促织部位等 20+ 个一览表）
                            var sheetIds = refItem.Params;
                            var sheetNames = refItem.Desc;
                            if (sheetIds == null || sheetIds.Count == 0)
                            {
                                lines.Add("*（表格集合：" + refId + "）*");
                            }
                            else
                            {
                                for (int si = 0; si < sheetIds.Count; si++)
                                {
                                    if (!_references.TryGetValue(sheetIds[si], out var sub)) continue;
                                    if (sub.InsertType != ReferenceInsertType.ConfigTable) continue;
                                    // 用 tab 标签名作为小标题，让每个 sheet 独立成段（Obsidian 里清晰分块）
                                    string sheetTitle = (si < sheetNames.Count && !string.IsNullOrEmpty(sheetNames[si]))
                                                        ? sheetNames[si] : null;
                                    if (sheetTitle != null)
                                    {
                                        lines.Add("**" + sheetTitle + "**");
                                        lines.Add("");
                                    }
                                    lines.Add(_tables.Render(sub, sheetTitle));
                                    if (si < sheetIds.Count - 1) lines.Add("");
                                }
                            }
                        }
                        else
                            lines.Add("*（引用：" + refId + "）*");
                        return lines;
                    }
                }
            }

            // 普通正文
            var converted = md.ConvertBody(content);
            if (string.IsNullOrWhiteSpace(converted)) return lines;

            bool isList = it.Layout.Contains(ContentLayout.Enum0);
            int indentLevel = 0;
            foreach (var l in it.Layout)
                if (EnumMaps.LayoutIndent.TryGetValue(l, out var il) && il > 0) indentLevel = il;

            if (isList)
            {
                lines.Add(new string(' ', indentLevel * 2) + "- " + converted.Trim());
            }
            else if (indentLevel > 0)
            {
                lines.Add(new string(' ', indentLevel * 2) + converted.Trim());
            }
            else
            {
                lines.Add(converted.Trim());
            }
            return lines;
        }
    }
}
