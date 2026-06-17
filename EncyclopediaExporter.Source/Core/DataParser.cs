using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using EncyclopediaExporter.Models;

namespace EncyclopediaExporter.Core
{
    /// <summary>
    /// TSV 数据解析器。所有解析规则来自反编译代码确认：
    /// - EncyclopediaDataProcessor.ParseStr / ParseStrArray / GetTable
    /// - Extentions.ColorReplace / ParseText
    /// - EncyclopediaContent.Init / EncyclopediaReference.Init
    /// </summary>
    public static class DataParser
    {
        // 语义颜色别名→hex（从反编译 Extentions.SetValueColor 等提取的已知映射）
        private static readonly Dictionary<string, string> KnownColors =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "brightblue", "#6fb6ff" },
                { "brightyellow", "#fff3b0" },
                { "specialyellow", "#e3c66d" },
            };
        private static readonly Dictionary<string, string> GradeColors =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "GradeColor_0", "#8E8E8E" }, { "GradeColor_1", "#FBFBFB" },
                { "GradeColor_2", "#6DB75F" }, { "GradeColor_3", "#8FBAE7" },
                { "GradeColor_4", "#63CED0" }, { "GradeColor_5", "#AE5AC8" },
                { "GradeColor_6", "#E3C66D" }, { "GradeColor_7", "#F26A34" },
                { "GradeColor_8", "#E4504D" },
            };

        // <color=#别名> 的语义颜色名正则（来自反编译 Extentions._colorReg）
        private static readonly Regex ColorReg =
            new Regex(@"<color=#((?:[A-Za-z][A-Za-z0-9]+_[A-Za-z0-9]+)|(?:[a-z]+))>",
                      RegexOptions.Compiled);

        /// <summary>还原转义：\n→换行, \u002c→逗号（来自 ParseStr）。</summary>
        public static string ParseStr(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\n", "\n").Replace("\\u002c", ",");
        }

        /// <summary>解析 {a,b,c} 数组（来自 ParseStrArray）。</summary>
        public static List<string> ParseStrArray(string s)
        {
            var result = new List<string>();
            if (string.IsNullOrEmpty(s)) return result;
            s = s.Replace("\\n", "\n").Replace("\\u002c", ",");
            string inner;
            if (s.StartsWith("{") && s.EndsWith("}"))
                inner = s.Substring(1, s.Length - 2);
            else
                inner = s;
            foreach (var x in inner.Split(','))
                result.Add(x.TrimStart());
            return result;
        }

        /// <summary>还原 \u003c→&lt; 等（来自 ParseText，在 ColorReplace 之后）。</summary>
        public static string ParseText(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\u003c", "<")
                    .Replace("\\u003e", ">")
                    .Replace("\\u005c", "\\");
        }

        /// <summary>语义颜色别名→hex（来自 ColorReplace）。</summary>
        public static string ColorReplace(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return ColorReg.Replace(s, m =>
            {
                string key = m.Groups[1].Value;
                string lower = key.ToLowerInvariant();
                if (KnownColors.TryGetValue(lower, out var hex))
                    return "<color=" + hex + ">";
                if (GradeColors.TryGetValue(key, out hex) || GradeColors.TryGetValue(lower, out hex))
                    return "<color=" + hex + ">";
                // 未知别名保留原样（渲染时再处理）
                return "<color=" + key + ">";
            });
        }

        /// <summary>完整的正文还原链：ParseStr → ColorReplace → ParseText。</summary>
        public static string ProcessContent(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            return ParseText(ColorReplace(ParseStr(raw)));
        }

        // ---- 文件读取 ----

        public static List<string[]> ReadTsv(string path)
        {
            var rows = new List<string[]>();
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrEmpty(line)) continue;
                rows.Add(line.Split('\t'));
            }
            return rows;
        }

        /// <summary>读取并完整处理数据表（来自 GetTable：转义还原+ColorReplace+ParseText）。</summary>
        public static List<string[]> GetTable(string assetsDir, string tableName)
        {
            var path = Path.Combine(assetsDir, tableName + ".tsv");
            if (!File.Exists(path)) return new List<string[]>();
            var rows = new List<string[]>();
            foreach (var line in File.ReadLines(path, Encoding.UTF8))
            {
                if (string.IsNullOrEmpty(line)) continue;
                var processed = ParseText(ColorReplace(ParseStr(line)));
                rows.Add(processed.Split('\t'));
            }
            return rows;
        }

        // ---- 解析 Content.tsv ----

        public static List<ContentItem> ParseContent(string path)
        {
            var items = new List<ContentItem>();
            foreach (var c in ReadTsv(path))
            {
                if (c.Length < 13) continue;
                var item = new ContentItem
                {
                    Titles = new string[5],
                    LayerRaw = c.Length > 5 ? c[5] : "",
                    Key = c[12],
                    ContentRaw = c.Length > 6 ? c[6] : "",
                    Content = c.Length > 6 ? ProcessContent(c[6]) : "",
                    LevelRaw = c.Length > 7 ? c[7] : "",
                    LayoutRaw = c.Length > 9 ? ParseStrArray(c[9]) : new List<string>(),
                    Inserts = c.Length > 11 ? ParseStrArray(c[11]) : new List<string>(),
                };
                for (int i = 0; i < 5; i++)
                    item.Titles[i] = c.Length > i ? ParseStr(c[i]) : "";

                item.Layer = EnumMaps.Layer.TryGetValue(item.LayerRaw, out var layer)
                    ? layer : ContentLayer.Content;
                item.Layout = new List<ContentLayout>();
                foreach (var l in item.LayoutRaw)
                    item.Layout.Add(EnumMaps.Layout.TryGetValue(l, out var lv)
                        ? lv : ContentLayout.None);

                ComputeParentKeys(item);
                items.Add(item);
            }
            return items;
        }

        /// <summary>计算 ProcessIndex 的父键（来自反编译 EncyclopediaContentItem.ProcessIndex）。</summary>
        public static void ComputeParentKeys(ContentItem item)
        {
            var key = item.Key;
            var layer = item.Layer;
            var positions = new List<int>();
            int start = 0;
            while (true)
            {
                int n = key.IndexOf('-', start);
                if (n < 0) break;
                positions.Add(n);
                start = n + 1;
            }

            item.ParentKey1 = positions.Count >= 1 ? key.Substring(0, positions[0]) : key;

            if (positions.Count >= 2)
                item.ParentKey2 = key.Substring(0, positions[1]);
            else if (layer == ContentLayer.Two)
                item.ParentKey2 = key;
            else
                item.ParentKey2 = "";

            if (positions.Count >= 3)
                item.ParentKey3 = key.Substring(0, positions[2]);
            else if (layer == ContentLayer.Three)
                item.ParentKey3 = key;
            else if (layer == ContentLayer.Content)
                item.ParentKey3 = ContentItem.StripTrailingNumber(key);
            else
                item.ParentKey3 = "";

            if (positions.Count >= 4)
                item.ParentKey4 = key.Substring(0, positions[3]);
            else if (layer == ContentLayer.Four)
                item.ParentKey4 = key;
            else if (layer == ContentLayer.Content)
                item.ParentKey4 = ContentItem.StripTrailingNumber(key);
            else
                item.ParentKey4 = "";
        }

        // ---- 解析 Reference.tsv ----

        public static Dictionary<string, ReferenceItem> ParseReference(string path)
        {
            var dict = new Dictionary<string, ReferenceItem>();
            foreach (var c in ReadTsv(path))
            {
                if (c.Length < 3) continue;
                var item = new ReferenceItem
                {
                    Id = c[0],
                    InsertType = EnumMaps.InsertType.TryGetValue(c[1], out var t)
                        ? t : ReferenceInsertType.Invalid,
                    Param = c.Length > 2 ? ParseStr(c[2]) : "",
                    Params = c.Length > 3 ? ParseStrArray(c[3]) : new List<string>(),
                    Desc = c.Length > 4 ? ParseStrArray(c[4]) : new List<string>(),
                    Title = c.Length > 5 ? ParseStr(c[5]) : "",
                };
                dict[item.Id] = item;
            }
            return dict;
        }

        // ---- 安全文件名 ----

        private static readonly Regex InvalidChars = new Regex(@"[<>:""/\\|?*]");
        public static string SafeName(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            s = InvalidChars.Replace(s, "_");
            return s.Trim().Trim('.') ?? "_";
        }
    }
}
