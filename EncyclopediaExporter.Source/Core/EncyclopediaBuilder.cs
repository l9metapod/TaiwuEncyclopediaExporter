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

            // 5. 清理输出目录
            // 只删除本工具产出的 *.md 文件，保留用户放入的任何其它内容
            // （如 .obsidian / .claude / .git / 自定义笔记等），避免更新后丢失用户设置。
            CleanOutput();

            // 6. 逐页生成
            int written = 0;
            foreach (var page in pages)
            {
                WritePage(page);
                written++;
            }
            return written;
        }

        /// <summary>
        /// 清理输出目录：仅删除本工具产出的 *.md 文件，并移除因此变空的目录。
        /// 保留用户放入的任何其它内容（.obsidian / .claude / .git / 自定义笔记等），
        /// 避免游戏更新触发重建后丢失用户的查看器/AI 配置。
        /// </summary>
        private void CleanOutput()
        {
            if (!Directory.Exists(_outputDir))
            {
                Directory.CreateDirectory(_outputDir);
                return;
            }

            // 删除所有 .md 文件（递归）
            foreach (var md in Directory.EnumerateFiles(_outputDir, "*.md", SearchOption.AllDirectories))
            {
                try { File.Delete(md); } catch { /* 忽略单个文件删除失败 */ }
            }

            // 自底向上移除变空的目录，但保留任何含内容的目录（保护用户文件）
            PruneEmptyDirs(_outputDir);
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
                            if (refItem.Params.Count > 0
                                && _references.TryGetValue(refItem.Params[0], out var sub)
                                && sub.InsertType == ReferenceInsertType.ConfigTable)
                                lines.Add(_tables.Render(sub));
                            else
                                lines.Add("*（表格集合：" + refId + "）*");
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
