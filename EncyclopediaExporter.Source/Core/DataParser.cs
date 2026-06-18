using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using EncyclopediaExporter.Models;
using UnityEngine;

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
        // 语义颜色别名→hex（探针 dump 自游戏 Colors.Instance，188 项，2026-06-18）
        // 优先级低于运行时 TryGetRuntimeColor（Colors.Instance），作为兜底。
        // 数据源：_probe/colors_table.txt
        private static readonly Dictionary<string, string> KnownColors =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                // 基础色
                { "lightgrey", "#B9B6B1FF" }, { "pinkyellow", "#F7F7F7FF" }, { "yellow", "#FFF5DDFF" },
                { "click", "#8F755FFF" }, { "brightred", "#EC5F68FF" }, { "brightblue", "#8DC3C3FF" },
                { "white", "#FFFFFFFF" }, { "lowwarning", "#CDB149FF" }, { "lightblue", "#6FB6FFFF" },
                { "darkred", "#D62B35FF" }, { "grey", "#6C6C6CFF" }, { "orange", "#FFF693FF" },
                { "secondary", "#B9B6B1FF" }, { "black", "#323232FF" }, { "darkgrey", "#8F8F8FFF" },
                { "darkbrown", "#B97D4BFF" }, { "lightbrown", "#9B8773FF" }, { "lightyellow", "#FFE100FF" },
                { "pink", "#FF44A7FF" }, { "lightwhite", "#E1CDAAFF" }, { "lightgreen", "#6DB75FFF" },
                { "darkcyan", "#63CED0FF" }, { "darkpurple", "#AE5AC8FF" }, { "red", "#E4504DFF" },
                { "goldyellow", "#EDA723FF" }, { "supportyellow", "#A5A67FFF" }, { "brightyellow", "#CFCAC2FF" },
                { "samsara", "#FFFFFFFF" }, { "title", "#FFFFFFFF" }, { "organization", "#FFFFFFFF" },
                { "darkyellow", "#E8E5E1FF" },
                // 性别/角色
                { "famale", "#F99796FF" }, { "male", "#9FE0DCFF" }, { "purplishred", "#C6272EFF" },
                // 战斗属性
                { "attack", "#CF4742FF" }, { "agile", "#6CC7B0FF" }, { "defense", "#C8A954FF" },
                { "assist", "#AB64C5FF" }, { "favorite", "#B9CFE8FF" }, { "hate", "#AF3C30FF" },
                { "outterinjury", "#D85718FF" }, { "innerinjury", "#8462C1FF" }, { "poisoned", "#D4AD48FF" },
                { "mystery", "#EE7592FF" }, { "forceresistance", "#FFB29AFF" }, { "accuracyparry", "#B2F4FEFF" },
                { "dexteritydodge", "#99F99DFF" },
                // 奇遇/门派
                { "mainstoryadventure", "#D67E40FF" }, { "normaladventure", "#90C95BFF" },
                { "sectstoryadventure", "#72D1D9FF" }, { "swordtomb", "#DC462EFF" }, { "caravan", "#FFEA8DFF" },
                { "eventadventure", "#A25AD6FF" }, { "goodsect", "#E9C13AFF" }, { "neutralsect", "#5987F3FF" },
                { "evilsect", "#B2373BFF" }, { "sectfulongflame", "#F55F69FF" },
                // 回合/战斗标记
                { "taiwuround", "#FFC586FF" }, { "enemyround", "#B6E1F1FF" }, { "brokenarea", "#B84A1FFF" },
                { "fataldamage", "#878787FF" }, { "deadlyenemy", "#A92713FF" }, { "friendorfamily", "#69CFECFF" },
                // 毒
                { "hotpoison", "#E5CD5DFF" }, { "gloomypoison", "#C682FFFF" }, { "redpoison", "#F45959FF" },
                { "coldpoison", "#70B1F2FF" }, { "rottenpoison", "#68EB6BFF" }, { "illusorypoison", "#DFDFDFFF" },
                { "anypoison", "#C8CED5FF" }, { "additionalpoison", "#AE5AC8FF" },
                // 杂项功能色
                { "debt", "#D40404FF" }, { "repair", "#EAB360FF" }, { "gift", "#5D80E7FF" },
                { "diemark", "#992A06FF" }, { "preexistenceoldfriend", "#9EE1C8FF" },
                { "scrollchapter", "#A79D82FF" }, { "scrollblack", "#201B13FF" }, { "wugking", "#C440BFFF" },
                { "grayscaleblack", "#545454FF" }, { "upgradeteammatecommand", "#C1FDFDFF" },
                { "negativecommand", "#E4613EFF" }, { "whitehair", "#FAFAFAFF" },
                // 季节/突破
                { "spring", "#AEE368FF" }, { "summer", "#4DB9C2FF" }, { "autumn", "#DD7631FF" }, { "winter", "#E8F1F5FF" },
                { "breakplatenormal", "#83DABCFF" }, { "breakplatespecial", "#C892E4FF" },
                { "breakplatestart", "#C5EFFFFF" }, { "breakplateend", "#FAED97FF" },
                // 功法/技艺
                { "lifeskillcombatblue", "#8FF8FFFF" }, { "lifeskillcombatred", "#FFBDAEFF" },
                { "skillbreakyellow", "#FFA800FF" }, { "hotkeyyellow", "#FFDF8CFF" }, { "newfeature", "#FFF1BBFF" },
                { "specialyellow", "#CDB149FF" }, { "specialred", "#843936FF" }, { "unique", "#B370A9FF" },
                { "contemplation", "#83CB98FF" }, { "obtainedofqi", "#6282E8FF" }, { "concentrate", "#5CAB44FF" },
                { "understand", "#C0AA44FF" }, { "legendbook", "#983427FF" }, { "disassemble", "#F9E8D3FF" },
                { "refined", "#8FBAE7FF" },
                // 品阶色（GradeColor）
                { "GradeColor_0", "#80817FFF" }, { "GradeColor_1", "#DCDEE0FF" }, { "GradeColor_2", "#9EB767FF" },
                { "GradeColor_3", "#5D92C3FF" }, { "GradeColor_4", "#509296FF" }, { "GradeColor_5", "#885390FF" },
                { "GradeColor_6", "#BE8A2FFF" }, { "GradeColor_7", "#CE5D20FF" }, { "GradeColor_8", "#D43F38FF" },
                // 魅力/心情/名望/好感/行为/性格（attractiontype/happinesstype/fametype/favorabilitytype/behaviortype/personalitytype）
                { "attractiontype_nonhuman", "#8E8E8EFF" }, { "attractiontype_odious", "#8E8E8EFF" },
                { "attractiontype_ugly", "#8E8E8EFF" }, { "attractiontype_normal", "#B6B1ABFF" },
                { "attractiontype_outstanding", "#239360FF" }, { "attractiontype_beautiful", "#91CCC9FF" },
                { "attractiontype_brilliant", "#B975FFFF" }, { "attractiontype_stunning", "#E9D382FF" },
                { "attractiontype_godlike", "#F3802AFF" }, { "attractiontype_naked", "#8E8E8EFF" },
                { "attractiontype_childish", "#8E8E8EFF" },
                { "happinesstype_saddest", "#C6272EFF" }, { "happinesstype_painful", "#B975FFFF" },
                { "happinesstype_depressed", "#E9D382FF" }, { "happinesstype_normal", "#8E8E8EFF" },
                { "happinesstype_pleased", "#239360FF" }, { "happinesstype_delighted", "#91CCC9FF" },
                { "happinesstype_happiest", "#F3802AFF" },
                { "fametype_bothgoodandbad", "#8E8E8EFF" }, { "fametype_worst", "#C6272EFF" },
                { "fametype_worse", "#B975FFFF" }, { "fametype_bad", "#E9D382FF" },
                { "fametype_normal", "#8E8E8EFF" }, { "fametype_good", "#239360FF" },
                { "fametype_better", "#91CCC9FF" }, { "fametype_best", "#F3802AFF" },
                { "favorabilitytype_unknown", "#8E8E8EFF" },
                { "favorabilitytype_hateful6", "#C6272EFF" }, { "favorabilitytype_hateful5", "#C6272EFF" },
                { "favorabilitytype_hateful4", "#C6272EFF" }, { "favorabilitytype_hateful3", "#C6272EFF" },
                { "favorabilitytype_hateful2", "#C6272EFF" }, { "favorabilitytype_hateful1", "#C6272EFF" },
                { "favorabilitytype_unfamiliar", "#B6B1ABFF" },
                { "favorabilitytype_favorite1", "#239360FF" }, { "favorabilitytype_favorite2", "#91CCC9FF" },
                { "favorabilitytype_favorite3", "#1ABAC8FF" }, { "favorabilitytype_favorite4", "#B975FFFF" },
                { "favorabilitytype_favorite5", "#E9D382FF" }, { "favorabilitytype_favorite6", "#F3802AFF" },
                { "favorabilitytype_loveself", "#B6B1ABFF" },
                { "behaviortype_just", "#FFE78FFF" }, { "behaviortype_kind", "#9FE0DCFF" },
                { "behaviortype_even", "#FFFFFFFF" }, { "behaviortype_rebel", "#B975FFFF" },
                { "behaviortype_egoistic", "#C6272EFF" },
                { "personalitytype_calm", "#BA9140FF" }, { "personalitytype_clever", "#8757B3FF" },
                { "personalitytype_enthusiastic", "#4F82B7FF" }, { "personalitytype_brave", "#C94B4BFF" },
                { "personalitytype_firm", "#81995EFF" }, { "personalitytype_lucky", "#FFFFFFFF" },
                { "personalitytype_perceptive", "#B1B1B1FF" },
                // 毒类型/五行/警觉（poisontype/fiveelementtype/alertnesstype）
                { "poisontype_hot", "#BA9140FF" }, { "poisontype_gloomy", "#8757B3FF" },
                { "poisontype_cold", "#4F82B7FF" }, { "poisontype_red", "#C94B4BFF" },
                { "poisontype_rotten", "#81995EFF" }, { "poisontype_illusory", "#858585FF" },
                { "poisontype_anypoison", "#C8CED5FF" },
                { "fiveelementtype_jingang", "#BA9140FF" }, { "fiveelementtype_zixia", "#8757B3FF" },
                { "fiveelementtype_xuanyin", "#4F82B7FF" }, { "fiveelementtype_chunyang", "#C94B4BFF" },
                { "fiveelementtype_guiyuan", "#81995EFF" }, { "fiveelementtype_hunyuan", "#858585FF" },
                { "alertnesstype_level0", "#F3802AFF" }, { "alertnesstype_level1", "#91CCC9FF" },
                { "alertnesstype_level2", "#239360FF" }, { "alertnesstype_level3", "#8E8E8EFF" },
                { "alertnesstype_level4", "#E9D382FF" }, { "alertnesstype_level5", "#B975FFFF" },
                { "alertnesstype_level6", "#C6272EFF" },
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

        // 运行时 Colors.Instance 缓存（反射访问，避免编译期硬依赖）
        private static bool _colorsChecked;
        private static System.Reflection.PropertyInfo _colorsInstanceProp;
        private static System.Reflection.PropertyInfo _colorsIndexer;
        private static readonly Dictionary<string, string> _runtimeColorCache =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        /// <summary>运行时查 Colors.Instance[name] 得到 hex。失败/未加载返回 null。
        /// 优先于硬编码 KnownColors 使用，保证与游戏 100% 一致（解决 pinkyellow 等漏词）。</summary>
        private static string TryGetRuntimeColor(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string lower = name.ToLowerInvariant();
            if (_runtimeColorCache.TryGetValue(lower, out string cached)) return cached;

            if (!_colorsChecked)
            {
                _colorsChecked = true;
                try
                {
                    var colorsType = System.Type.GetType("Colors");
                    if (colorsType == null)
                    {
                        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                        {
                            colorsType = asm.GetType("Colors");
                            if (colorsType != null) break;
                        }
                    }
                    if (colorsType != null)
                    {
                        _colorsInstanceProp = colorsType.GetProperty("Instance");
                        _colorsIndexer = colorsType.GetProperty("Item"); // this[string]
                    }
                }
                catch { /* 忽略，回退硬编码 */ }
            }

            string result = null;
            try
            {
                if (_colorsInstanceProp != null && _colorsIndexer != null)
                {
                    object instance = _colorsInstanceProp.GetValue(null, null);
                    if (instance != null)
                    {
                        object color = _colorsIndexer.GetValue(instance, new object[] { lower });
                        if (color is UnityEngine.Color c)
                        {
                            result = "#" + UnityEngine.ColorUtility.ToHtmlStringRGBA(c);
                        }
                    }
                }
            }
            catch { /* 忽略，回退硬编码 */ }

            _runtimeColorCache[lower] = result; // null 也缓存，避免重复反射
            return result;
        }

        /// <summary>语义颜色别名→hex（来自反编译 Extentions.ColorReplace）。
        /// 优先用运行时 Colors.Instance（与游戏 100% 一致），失败回退到硬编码 KnownColors。</summary>
        public static string ColorReplace(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return ColorReg.Replace(s, m =>
            {
                string key = m.Groups[1].Value;

                // 1. 优先：运行时 Colors.Instance（根本解，永不漏词）
                string hex = TryGetRuntimeColor(key);
                if (hex != null) return "<color=" + hex + ">";

                // 2. 兜底：硬编码表（已合并品阶色，188 项）
                string lower = key.ToLowerInvariant();
                if (KnownColors.TryGetValue(lower, out hex))
                    return "<color=" + hex + ">";

                // 3. 未知别名：保留原样（探针 dump 出 Colors 表后可补全）
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
