using System.Collections.Generic;

namespace EncyclopediaExporter.Models
{
    /// <summary>
    /// 百科内容项（对应 EncyclopediaContent.tsv 一行）。
    /// 列定义来自反编译 EncyclopediaContent.Init：
    ///   col0-4 : Title1-5
    ///   col5   : Layer
    ///   col6   : Content（正文）
    ///   col7   : Level（难度）
    ///   col8   : Fonts
    ///   col9   : Layout（布局数组）
    ///   col10  : EnabledHyperLinks
    ///   col11  : Inserts（引用 Reference id 数组）
    ///   col12  : Key（唯一ID）
    /// </summary>
    public class ContentItem
    {
        public string[] Titles;             // Title1-5（长度5）
        public ContentLayer Layer;
        public string LayerRaw;             // 原始层名字符串
        public string Key;                  // 唯一ID（col12）

        public string Content;              // 已转义还原的正文
        public string ContentRaw;           // 原始正文
        public string LevelRaw;             // 难度原文
        public List<ContentLayout> Layout;  // 布局枚举
        public List<string> LayoutRaw;      // 布局原文
        public List<string> Inserts;        // 引用的 Reference id

        // ProcessIndex 计算的父键（来自反编译 EncyclopediaContentItem.ProcessIndex）
        public string ParentKey1, ParentKey2, ParentKey3, ParentKey4;

        public bool IsHeading => Layer != ContentLayer.Content;

        /// <summary>去掉 Key 末尾的数字后缀（用于正文行找父标题）。</summary>
        public static string StripTrailingNumber(string s)
        {
            int i = s.Length;
            while (i > 0 && s[i - 1] >= '0' && s[i - 1] <= '9') i--;
            return s.Substring(0, i);
        }
    }

    /// <summary>
    /// 百科引用项（对应 EncyclopediaReference.tsv 一行）。
    /// 列定义来自反编译 EncyclopediaReference.Init：
    ///   col0 : id（link 指向它）
    ///   col1 : InsertType
    ///   col2 : param
    ///   col3 : stringParams {a,b,c}
    ///   col4 : desc {a,b,c}
    ///   col5 : title
    /// </summary>
    public class ReferenceItem
    {
        public string Id;                   // col0
        public ReferenceInsertType InsertType;
        public string Param;                // col2
        public List<string> Params;         // col3
        public List<string> Desc;           // col4
        public string Title;                // col5
    }
}
