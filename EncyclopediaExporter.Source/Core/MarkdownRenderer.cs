using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using EncyclopediaExporter.Models;

namespace EncyclopediaExporter.Core
{
    /// <summary>
    /// 富文本→Markdown 转换器。
    /// 规则来自反编译 Extentions/SingleTextElement/LinkElement 确认的渲染逻辑。
    /// </summary>
    public class MarkdownRenderer
    {
        private readonly Func<string, ResolveResult> _resolver;

        public MarkdownRenderer(Func<string, ResolveResult> resolver)
        {
            _resolver = resolver;
        }

        /// <summary>把已 ProcessContent 的富文本转为 Markdown。</summary>
        public string ConvertBody(string body)
        {
            if (string.IsNullOrEmpty(body)) return "";
            var s = body;
            s = Regex.Replace(s, @"<align=""center"">(.*?)</align>", "$1", RegexOptions.Singleline);
            s = Regex.Replace(s, @"<align=""left"">(.*?)</align>", "$1", RegexOptions.Singleline);
            s = Regex.Replace(s, @"<align=""right"">(.*?)</align>", "$1", RegexOptions.Singleline);
            s = Regex.Replace(s, @"<i>(.*?)</i>", "*$1*", RegexOptions.Singleline);
            s = Regex.Replace(s, @"</?u>", "");

            // <color=#hex>...</color> → span; 别名→保留文字
            s = Regex.Replace(s, @"<color=([^>]*)>(.*?)</color>", m =>
            {
                string color = m.Groups[1].Value;
                string inner = m.Groups[2].Value;
                if (Regex.IsMatch(color, @"^#[0-9a-fA-F]{6,8}$"))
                    return "<span style=\"color:" + color + "\">" + inner + "</span>";
                return inner;  // 语义别名: 文字本身已是可见内容
            }, RegexOptions.Singleline);

            // <link="X">txt</link> → 经 Reference 解析
            s = Regex.Replace(s, @"<link=""([^""]*)"">(.*?)</link>", m =>
            {
                string target = m.Groups[1].Value;
                string txt = m.Groups[2].Value;
                var r = _resolver(target);
                if (r == null || r.Type == ResolveType.Unresolved)
                    return txt;
                if (r.Type == ResolveType.Page)
                    return "[" + txt + "](" + r.Link + ")";
                // Tips: 文字已是名称，直接保留
                return txt;
            }, RegexOptions.Singleline);

            // 清残留标签
            s = Regex.Replace(s, @"</?(?:color|link|u|i|align|mark|td)[^>]*>", "");
            return s;
        }

        /// <summary>计算从 fromDir 到 toAbsPath 的相对路径（用正斜杠）。</summary>
        public static string RelPath(string fromDir, string toAbsPath)
        {
            var rel = new Uri(Path.GetFullPath(fromDir) + "\\")
                .MakeRelativeUri(new Uri(Path.GetFullPath(toAbsPath)));
            return Uri.UnescapeDataString(rel.ToString());
        }
    }

    public enum ResolveType { Page, Table, Image, Tip, Unresolved }

    public class ResolveResult
    {
        public ResolveType Type;
        public string Link;     // Page 时的 md 相对路径
        public string Param;    // Table/Image/Tip 的参数
    }
}
