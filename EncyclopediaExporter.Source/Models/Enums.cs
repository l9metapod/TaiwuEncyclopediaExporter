using System.Collections.Generic;

namespace EncyclopediaExporter.Models
{
    /// <summary>
    /// 百科内容层（来自反编译 EnumMap.Layer）。
    /// 对应 Content.tsv col5。
    /// </summary>
    public enum ContentLayer
    {
        Content,    // 正文
        One,        // Heading1
        Two,        // Heading2
        Three,      // Heading3
        Four,       // Heading4
        Five        // Heading5
    }

    /// <summary>
    /// 布局类型（来自反编译 EnumMap.Layout / Element.GetLayoutPadding）。
    /// 枚举0 = 列表项；枚举440/840/1260/1680 = 递增缩进。
    /// </summary>
    public enum ContentLayout
    {
        None,       // 无 / 无缩进
        NoIdent,
        Enum0,      // 列表项（圆点前缀）
        Enum1,      // 缩进47px
        Enum2,      // 缩进71px
        Enum3,      // 缩进95px
        Enum4,      // 缩进119px
        EnumInvalid
    }

    /// <summary>
    /// 引用插入类型（来自反编译 EnumMap.InsertType）。
    /// 对应 Reference.tsv col1。
    /// </summary>
    public enum ReferenceInsertType
    {
        Invalid,
        ConfigTable,        // 表
        Figure,             // 图片
        HyperLink,          // 超链接（跳百科页）
        Equation,
        FeatureTips,        // 特性Tips
        WeaponTips,
        MiscTips,
        AccessoryTips,
        ArmorTips,
        CarrierTips,
        ClothingTips,
        CraftToolTips,
        CricketTips,
        FoodTips,
        MaterialTips,
        MedicineTips,
        TeaWineTips,
        SkillBookTips,
        CombatSkillTips,
        BuildingBlockTips,
        ConsummateLevelTips,
        DebateStrategyTips,
        NeiliTypeTips,
        ProtagonistFeatureTips,
        WorldStateTips,
        LifeSkillTips,
        TrickTypeTips,
        ProfessionTips,
        ProfessionSkillTips,
        MixedTips,
        TableCollection,    // 表格集合
        FixedText           // 固定文本
    }

    /// <summary>
    /// 所有枚举的中文→枚举映射（来自反编译 EnumMap.cs）。
    /// </summary>
    public static class EnumMaps
    {
        public static readonly Dictionary<string, ContentLayer> Layer =
            new Dictionary<string, ContentLayer>
            {
                { "Content", ContentLayer.Content },
                { "Heading1", ContentLayer.One },
                { "Heading2", ContentLayer.Two },
                { "Heading3", ContentLayer.Three },
                { "Heading4", ContentLayer.Four },
                { "Heading5", ContentLayer.Five },
            };

        public static readonly Dictionary<string, ContentLayout> Layout =
            new Dictionary<string, ContentLayout>
            {
                { "None", ContentLayout.None },
                { "无缩进", ContentLayout.NoIdent },
                { "枚举0", ContentLayout.Enum0 },
                { "枚举440", ContentLayout.Enum1 },
                { "枚举440_400_840", ContentLayout.Enum2 },
                { "枚举440_600_1260", ContentLayout.Enum3 },
                { "枚举440_800_1680", ContentLayout.Enum4 },
                { "枚举440_0_0", ContentLayout.EnumInvalid },
            };

        public static readonly Dictionary<string, ReferenceInsertType> InsertType =
            new Dictionary<string, ReferenceInsertType>
            {
                { "说明", ReferenceInsertType.Invalid },
                { "表", ReferenceInsertType.ConfigTable },
                { "图片", ReferenceInsertType.Figure },
                { "超链接", ReferenceInsertType.HyperLink },
                { "公式", ReferenceInsertType.Equation },
                { "特性Tips", ReferenceInsertType.FeatureTips },
                { "武器Tips", ReferenceInsertType.WeaponTips },
                { "杂物Tips", ReferenceInsertType.MiscTips },
                { "宝物Tips", ReferenceInsertType.AccessoryTips },
                { "盔甲Tips", ReferenceInsertType.ArmorTips },
                { "代步Tips", ReferenceInsertType.CarrierTips },
                { "衣装Tips", ReferenceInsertType.ClothingTips },
                { "工具Tips", ReferenceInsertType.CraftToolTips },
                { "促织Tips", ReferenceInsertType.CricketTips },
                { "食物Tips", ReferenceInsertType.FoodTips },
                { "引子Tips", ReferenceInsertType.MaterialTips },
                { "药毒Tips", ReferenceInsertType.MedicineTips },
                { "茶酒Tips", ReferenceInsertType.TeaWineTips },
                { "书籍Tips", ReferenceInsertType.SkillBookTips },
                { "功法Tips", ReferenceInsertType.CombatSkillTips },
                { "建筑Tips", ReferenceInsertType.BuildingBlockTips },
                { "精纯Tips", ReferenceInsertType.ConsummateLevelTips },
                { "较艺策略Tips", ReferenceInsertType.DebateStrategyTips },
                { "内力属性Tips", ReferenceInsertType.NeiliTypeTips },
                { "出身特质Tips", ReferenceInsertType.ProtagonistFeatureTips },
                { "世界状态Tips", ReferenceInsertType.WorldStateTips },
                { "技艺Tips", ReferenceInsertType.LifeSkillTips },
                { "招式Tips", ReferenceInsertType.TrickTypeTips },
                { "志向Tips", ReferenceInsertType.ProfessionTips },
                { "志向技能Tips", ReferenceInsertType.ProfessionSkillTips },
                { "图文混排Tips", ReferenceInsertType.MixedTips },
                { "表格集合", ReferenceInsertType.TableCollection },
                { "固定文本", ReferenceInsertType.FixedText },
            };

        /// <summary>Layout→MD缩进层级（0=列表项本身，1+为缩进深度）</summary>
        public static readonly Dictionary<ContentLayout, int> LayoutIndent =
            new Dictionary<ContentLayout, int>
            {
                { ContentLayout.Enum0, 0 },
                { ContentLayout.Enum1, 1 },
                { ContentLayout.Enum2, 2 },
                { ContentLayout.Enum3, 3 },
                { ContentLayout.Enum4, 4 },
            };
    }
}
