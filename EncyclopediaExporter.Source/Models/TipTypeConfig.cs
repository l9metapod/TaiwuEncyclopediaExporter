using System.Collections.Generic;

namespace EncyclopediaExporter.Models
{
    /// <summary>
    /// Tips 类型（悬浮卡片）的导出配置。
    /// 每个 Tips 类型对应一个游戏配置表 + 一组需要导出的字段。
    /// 字段翻译优先用 Config 子表的 .Name；枚举值用硬编码数组（避免依赖 LocalStringManager 运行时状态）。
    /// </summary>
    internal static class TipTypeConfig
    {
        /// <summary>Tips InsertType 枚举 → 导出元数据（核心装备 + 词条类）。</summary>
        public static readonly Dictionary<ReferenceInsertType, TipMeta> EnumMap =
            new Dictionary<ReferenceInsertType, TipMeta>
            {
                { ReferenceInsertType.CombatSkillTips, new TipMeta("功法", "CombatSkill") },
                { ReferenceInsertType.FeatureTips, new TipMeta("特性", "CharacterFeature") },
                // 装备类（第一阶段+第二阶段）
                { ReferenceInsertType.WeaponTips, new TipMeta("武器", "Weapon") },
                { ReferenceInsertType.ArmorTips, new TipMeta("盔甲", "Armor") },
                { ReferenceInsertType.ClothingTips, new TipMeta("衣装", "Clothing") },
                { ReferenceInsertType.AccessoryTips, new TipMeta("宝物", "Accessory") },
                { ReferenceInsertType.CraftToolTips, new TipMeta("工具", "CraftTool") },
                // 消耗品/其他（第三阶段）
                { ReferenceInsertType.MedicineTips, new TipMeta("药毒", "Medicine") },
                { ReferenceInsertType.MaterialTips, new TipMeta("引子", "Material") },
                { ReferenceInsertType.SkillBookTips, new TipMeta("书籍", "SkillBook") },
                { ReferenceInsertType.MiscTips, new TipMeta("杂物", "Misc") },
                { ReferenceInsertType.FoodTips, new TipMeta("食物", "Food") },
                { ReferenceInsertType.TeaWineTips, new TipMeta("茶酒", "TeaWine") },
                { ReferenceInsertType.CarrierTips, new TipMeta("代步", "Carrier") },
                { ReferenceInsertType.CricketTips, new TipMeta("促织", "Cricket") },
                // 基础词条类
                { ReferenceInsertType.ProtagonistFeatureTips, new TipMeta("出身特质", "ProtagonistFeature") },
                { ReferenceInsertType.NeiliTypeTips, new TipMeta("内力属性", "NeiliType") },
                { ReferenceInsertType.TrickTypeTips, new TipMeta("招式", "TrickType") },
            };

        /// <summary>武器子类 short(ItemSubType) → 可读名（LK_ItemSubType_0~17，已确认）。
        /// 0针匣 1对刺 2暗器 3箫笛 4掌套 5短杵 6拂尘 7长鞭 8剑 9刀 10长兵 11瑶琴 12机关 13令符 14药霜 15毒砂 16神兵 17动物</summary>
        public static readonly string[] WeaponSubTypeNames =
        {
            "针匣", "对刺", "暗器", "箫笛", "掌套", "短杵", "拂尘", "长鞭",
            "剑", "刀", "长兵", "瑶琴", "机关", "令符", "药霜", "毒砂", "神兵", "动物"
        };

        /// <summary>武器子类名（越界返回"未知"）。</summary>
        public static string WeaponSubTypeName(short subType) =>
            (subType >= 0 && subType < WeaponSubTypeNames.Length) ? WeaponSubTypeNames[subType] : "未知";

        /// <summary>品阶 sbyte → 可读名（九品～一品）。</summary>
        public static readonly string[] GradeNames =
            { "九品", "八品", "七品", "六品", "五品", "四品", "三品", "二品", "一品" };

        /// <summary>品阶前缀（下/中/上/奇/秘/极/超/绝/神），对应 Grade 0-8。
        /// 来自 LK_ShortGrade_0~8。显示格式："前缀·X品"（如九品="下·九品"、三品="超·三品"）。</summary>
        public static readonly string[] GradePrefixes =
            { "下", "中", "上", "奇", "秘", "极", "超", "绝", "神" };

        /// <summary>完整品阶显示（如"超·三品"）。对齐游戏卡片 GradeLabel 格式。</summary>
        public static string GradeDisplay(sbyte g)
        {
            if (g < 0 || g >= GradeNames.Length) return "未知";
            return GradePrefixes[g] + "·" + GradeNames[g];
        }

        /// <summary>功法五行/内力属性 sbyte → 可读名（金刚/紫霞/玄阴/纯阳/归元/混元）。</summary>
        public static readonly string[] FiveElementsNames =
            { "金刚", "紫霞", "玄阴", "纯阳", "归元", "混元" };

        /// <summary>特性类型 ECharacterFeatureType → 可读名。</summary>
        public static readonly string[] FeatureTypeNames =
            { "特殊", "良性", "恶性", "临时" };

        /// <summary>安全的品阶/五行取值（越界返回"未知"）。</summary>
        public static string GradeName(sbyte g) =>
            (g >= 0 && g < GradeNames.Length) ? GradeNames[g] : "未知";

        public static string FiveElementsName(sbyte e) =>
            (e >= 0 && e < FiveElementsNames.Length) ? FiveElementsNames[e] : "未知";

        public static string FeatureTypeName(int t) =>
            (t >= 0 && t < FeatureTypeNames.Length) ? FeatureTypeNames[t] : "未知";
    }

    /// <summary>单个 Tips 类型的导出元数据。</summary>
    internal sealed class TipMeta
    {
        /// <summary>词条子目录名（output/词条/&lt;SubDir&gt;/）。</summary>
        public string SubDir { get; }
        /// <summary>游戏配置表类名（用于日志，实际访问在 TipEntryResolver 里按类分发）。</summary>
        public string ConfigTable { get; }

        public TipMeta(string subDir, string configTable)
        {
            SubDir = subDir;
            ConfigTable = configTable;
        }
    }
}
