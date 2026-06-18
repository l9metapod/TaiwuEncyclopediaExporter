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
        /// <summary>Tips InsertType 枚举 → 导出元数据（仅核心 6 类）。</summary>
        public static readonly Dictionary<ReferenceInsertType, TipMeta> EnumMap =
            new Dictionary<ReferenceInsertType, TipMeta>
            {
                { ReferenceInsertType.CombatSkillTips, new TipMeta("功法", "CombatSkill") },
                { ReferenceInsertType.FeatureTips, new TipMeta("特性", "CharacterFeature") },
                // 武器（装备）词条暂未与功法/战斗联动，信息孤立，先屏蔽。
                // RenderWeapon 实现保留，待后续补充破体破气/攻击范围/招式等联动字段后恢复。
                // { ReferenceInsertType.WeaponTips, new TipMeta("武器", "Weapon") },
                { ReferenceInsertType.ProtagonistFeatureTips, new TipMeta("出身特质", "ProtagonistFeature") },
                { ReferenceInsertType.NeiliTypeTips, new TipMeta("内力属性", "NeiliType") },
                { ReferenceInsertType.TrickTypeTips, new TipMeta("招式", "TrickType") },
            };

        /// <summary>品阶 sbyte → 可读名（九品～一品）。</summary>
        public static readonly string[] GradeNames =
            { "九品", "八品", "七品", "六品", "五品", "四品", "三品", "二品", "一品" };

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
