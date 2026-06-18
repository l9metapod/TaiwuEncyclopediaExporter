using System;
using System.Collections.Generic;
using System.Text;
using Config;
using EncyclopediaExporter.Models;

namespace EncyclopediaExporter.Core
{
    /// <summary>
    /// 悬浮信息（Tips）词条解析器。
    /// 按 InsertType 分发到对应渲染器，从运行时 Config 单例读取数据（文字+数值），
    /// 生成单个词条的 Markdown 内容。
    ///
    /// 运行时配置在 mod 的 Initialize() 时已加载（GameApp: 配置初始化 → mod 加载，顺序确认）。
    /// </summary>
    internal static class TipEntryResolver
    {
        /// <summary>
        /// 渲染单个词条。返回 (文件名不含扩展名, markdown 正文)；若 ID 无效返回 null。
        /// </summary>
        public static Tuple<string, string> Render(ReferenceInsertType insertType, int id)
        {
            switch (insertType)
            {
                case ReferenceInsertType.CombatSkillTips: return RenderCombatSkill(id);
                case ReferenceInsertType.FeatureTips: return RenderCharacterFeature(id);
                case ReferenceInsertType.WeaponTips: return RenderWeapon(id);
                case ReferenceInsertType.ProtagonistFeatureTips: return RenderProtagonistFeature(id);
                case ReferenceInsertType.NeiliTypeTips: return RenderNeiliType(id);
                case ReferenceInsertType.TrickTypeTips: return RenderTrickType(id);
                default: return null;
            }
        }

        // ============ 功法 CombatSkill ============
        private static Tuple<string, string> RenderCombatSkill(int id)
        {
            var s = CombatSkill.Instance[id];
            if (s == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("# " + s.Name);
            sb.AppendLine();

            // 标题下聚合行：品阶 · 类型 · 五行 · 门派（紧凑，悬浮卡片一眼可见）
            var tags = new List<string>();
            tags.Add(TipTypeConfig.GradeName(s.Grade));
            tags.Add(SafeName(() => CombatSkillType.Instance[s.Type]?.Name));
            tags.Add(TipTypeConfig.FiveElementsName(s.FiveElements));
            tags.Add(SafeName(() => Organization.Instance[s.SectId]?.Name));
            sb.AppendLine("**" + string.Join(" · ", tags) + "**");
            // 次要属性（内力/格子/最适武器）内联，不占行
            var extras = new List<string>();
            if (s.TotalObtainableNeili != 0) extras.Add("**内力** " + s.TotalObtainableNeili);
            if (s.GridCost != 0) extras.Add("**格子** " + s.GridCost);
            if (s.MobilityCost != 0) extras.Add("**脚力** " + s.MobilityCost + "%");
            if (s.MostFittingWeaponID > 0)
                extras.Add("**最适** " + SafeName(() => Weapon.Instance[s.MostFittingWeaponID]?.Name));
            if (extras.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(string.Join("　", extras));
            }

            // 描述
            if (!string.IsNullOrEmpty(s.Desc))
            {
                sb.AppendLine();
                sb.AppendLine(s.Desc);
            }

            // 心法效果（正练/逆练）
            string direct = GetSpecialEffectDesc(s.DirectEffectID);
            string reverse = GetSpecialEffectDesc(s.ReverseEffectID);
            if (!string.IsNullOrEmpty(direct) || !string.IsNullOrEmpty(reverse))
            {
                sb.AppendLine();
                sb.AppendLine("## 心法效果");
                if (!string.IsNullOrEmpty(direct))
                    sb.AppendLine("- **正练**：" + direct);
                if (!string.IsNullOrEmpty(reverse))
                    sb.AppendLine("- **逆练**：" + reverse);
            }

            // 攻击属性（摧破类）——紧凑表格，横向利用空间
            // 破体/破气由 Penetrate + BaseInnerRatio 推导；命中三段来自 PerHitDamageRateDistribution
            bool hasAtk = (s.Penetrate != 0) || (s.TotalHit != 0)
                || (s.PerHitDamageRateDistribution != null);
            if (hasAtk)
            {
                sb.AppendLine();
                sb.AppendLine("## 攻击属性");

                // 表一：破体/破气/内外比/范围（核心攻击数值）
                var atkHeaders = new List<string>();
                var atkValues = new List<string>();
                if (s.Penetrate != 0)
                {
                    int innerPart = s.Penetrate * s.BaseInnerRatio / 100;
                    int outerPart = s.Penetrate - innerPart;
                    atkHeaders.Add("破体"); atkValues.Add((100 + outerPart) + "%");
                    atkHeaders.Add("破气"); atkValues.Add((100 + innerPart) + "%");
                }
                if (s.BaseInnerRatio != 0)
                {
                    atkHeaders.Add("内外比");
                    atkValues.Add(s.BaseInnerRatio + "%内" + (s.InnerRatioChangeRange != 0 ? "±" + s.InnerRatioChangeRange : ""));
                }
                if (s.DistanceAdditionWhenCast != 0)
                {
                    atkHeaders.Add("范围");
                    atkValues.Add("+" + (s.DistanceAdditionWhenCast / 10f).ToString("f1"));
                }
                if (atkHeaders.Count > 0) AppendCompactTable(sb, atkHeaders, atkValues);

                // 表二：命中段（力道/精妙/迅疾/心神）+ 提气/架势
                if (s.PerHitDamageRateDistribution != null && s.PerHitDamageRateDistribution.Length >= 4)
                {
                    var hitHeaders = new List<string>();
                    var hitValues = new List<string>();
                    string[] hitNames = { "力道", "精妙", "迅疾", "心神" };
                    for (int i = 0; i < 4; i++)
                    {
                        sbyte dist = s.PerHitDamageRateDistribution[i];
                        if (dist == 0) continue;
                        int powerTenths = dist / 10;
                        int hitValue = 100 + s.TotalHit * dist / 100;
                        hitHeaders.Add(hitNames[i]);
                        hitValues.Add(powerTenths + "成·" + hitValue + "%");
                    }
                    if (s.BreathStanceTotalCost != 0)
                    {
                        hitHeaders.Add("提气");
                        hitValues.Add((s.BreathStanceTotalCost * s.BaseInnerRatio / 100) + "%");
                        hitHeaders.Add("架势");
                        hitValues.Add((s.BreathStanceTotalCost * (100 - s.BaseInnerRatio) / 100) + "%");
                    }
                    if (hitHeaders.Count > 0)
                    {
                        if (atkHeaders.Count > 0) sb.AppendLine();
                        AppendCompactTable(sb, hitHeaders, hitValues);
                    }
                }
                sb.AppendLine("> 威力由人物属性与突破状态实时计算，无固定值。");
            }

            // 施展需要：蓄式 + 部位（内联粗体，变长信息不适合表格）
            bool hasTrickCost = s.TrickCost != null && s.TrickCost.Count > 0;
            bool hasBodyPart = s.NeedBodyPartTypes != null && s.NeedBodyPartTypes.Count > 0;
            if (hasTrickCost || hasBodyPart)
            {
                sb.AppendLine();
                sb.AppendLine("## 施展需要");
                var parts = new List<string>();
                if (hasTrickCost)
                {
                    var tricks = new List<string>();
                    foreach (var nt in s.TrickCost)
                    {
                        string tname = SafeName(() => Config.TrickType.Instance[nt.TrickType]?.Name);
                        tricks.Add(tname + "×" + nt.NeedCount);
                    }
                    parts.Add("**蓄式** " + string.Join("、", tricks));
                }
                if (hasBodyPart)
                {
                    var bps = new List<string>();
                    foreach (var bp in s.NeedBodyPartTypes) bps.Add(BodyPartName(bp));
                    parts.Add("**部位** " + string.Join("、", bps));
                }
                sb.AppendLine(string.Join("　", parts));
            }

            // 施展需求（属性要求）——内联粗体，紧凑
            if (s.UsingRequirement != null && s.UsingRequirement.Count > 0)
            {
                sb.AppendLine();
                var parts = new List<string>();
                foreach (var pv in s.UsingRequirement)
                    parts.Add("**" + GetPropertyName(pv.PropertyId) + "** " + pv.Value);
                // 多个时换行排，避免一行过长
                sb.AppendLine("## 施展需求");
                sb.AppendLine(string.Join("　", parts));
            }

            // 属性加成——内联粗体
            if (s.PropertyAddList != null && s.PropertyAddList.Count > 0)
            {
                sb.AppendLine();
                var parts = new List<string>();
                foreach (var pv in s.PropertyAddList)
                {
                    string sign = pv.Value >= 0 ? "+" : "";
                    parts.Add("**" + GetPropertyName(pv.PropertyId) + "** " + sign + pv.Value);
                }
                sb.AppendLine("## 属性加成");
                sb.AppendLine(string.Join("　", parts));
            }

            // 运功路径
            if (!string.IsNullOrEmpty(s.BreakStart) || !string.IsNullOrEmpty(s.BreakEnd))
            {
                sb.AppendLine();
                sb.AppendLine("**运功** " + NullToEmpty(s.BreakStart) + " → " + NullToEmpty(s.BreakEnd));
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(s.Name), sb.ToString());
        }

        /// <summary>紧凑表格：表头行 + 值行，无"属性/值"冗余列。</summary>
        private static void AppendCompactTable(StringBuilder sb, List<string> headers, List<string> values)
        {
            sb.AppendLine("| " + string.Join(" | ", headers) + " |");
            sb.AppendLine("|" + string.Join("|", Repeat("---", headers.Count)) + "|");
            sb.AppendLine("| " + string.Join(" | ", values) + " |");
        }

        private static List<string> Repeat(string s, int n)
        {
            var list = new List<string>(n);
            for (int i = 0; i < n; i++) list.Add(s);
            return list;
        }

        /// <summary>施展部位 sbyte → 可读名（对齐 MouseTipConstant.HitPartNames 显示序）。</summary>
        private static string BodyPartName(sbyte v)
        {
            switch (v)
            {
                case 0: return "头";
                case 1: return "胸";
                case 2: return "腹";
                case 3: return "左手+右手";
                case 4: return "左手/右手";
                case 5: return "左腿+右腿";
                case 6: return "左腿/右腿";
                default: return "部位#" + v;
            }
        }

        /// <summary>取 SpecialEffect 的描述文本（正练/逆练）。优先用游戏的 CommonUtils（已替换占位符）；失败则退回 Desc[0]。</summary>
        private static string GetSpecialEffectDesc(int effectId)
        {
            if (effectId < 0) return "";
            try
            {
                // 游戏自己的 helper：会替换 $0$ 等占位符为数值
                string txt = CommonUtils.GetSpecialEffectDesc(effectId);
                if (!string.IsNullOrEmpty(txt)) return txt.Trim();
            }
            catch { /* CommonUtils 不可用或异常时退回直接读表 */ }
            try
            {
                var item = SpecialEffect.Instance[effectId];
                if (item != null && item.Desc != null && item.Desc.Length > 0)
                    return item.Desc[0];
            }
            catch { }
            return "";
        }

        /// <summary>属性 ID → 可读名（经 CharacterPropertyReferenced → CharacterPropertyDisplay 两级映射）。</summary>
        private static string GetPropertyName(short propertyId)
        {
            if (propertyId == 10000) return "所有毒抗"; // 特殊聚合值
            try
            {
                short displayType = CharacterPropertyReferenced.Instance[propertyId].DisplayType;
                var disp = CharacterPropertyDisplay.Instance[displayType];
                string name = disp?.Name;
                if (!string.IsNullOrEmpty(name)) return name;
            }
            catch { }
            return "属性#" + propertyId;
        }

        // ============ 特性 CharacterFeature ============
        private static Tuple<string, string> RenderCharacterFeature(int id)
        {
            var f = CharacterFeature.Instance[id];
            if (f == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("# " + f.Name);
            sb.AppendLine();

            var rows = new List<KV>();
            rows.Add(new KV("类型", TipTypeConfig.FeatureTypeName((int)f.Type)));
            rows.Add(new KV("等级", f.Level.ToString()));
            // 主要属性加成
            AddIfNonZero(rows, "膂力", f.Strength);
            AddIfNonZero(rows, "体质", f.Vitality);
            AddIfNonZero(rows, "灵敏", f.Dexterity);
            AddIfNonZero(rows, "根骨", f.Energy);
            AddIfNonZero(rows, "悟性", f.Intelligence);
            AddIfNonZero(rows, "定力", f.Concentration);
            // 七元赋性
            AddIfNonZero(rows, "中正", f.PersonalityCalm);
            AddIfNonZero(rows, "机变", f.PersonalityClever);
            AppendTable(sb, rows);

            if (!string.IsNullOrEmpty(f.Desc))
            {
                sb.AppendLine();
                sb.AppendLine(f.Desc);
            }
            if (!string.IsNullOrEmpty(f.EffectDesc) && f.EffectDesc != f.Desc)
            {
                sb.AppendLine();
                sb.AppendLine(f.EffectDesc);
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(f.Name), sb.ToString());
        }

        // ============ 武器 Weapon ============
        private static Tuple<string, string> RenderWeapon(int id)
        {
            var w = Weapon.Instance[id];
            if (w == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("# " + w.Name);
            sb.AppendLine();

            var rows = new List<KV>();
            rows.Add(new KV("品阶", TipTypeConfig.GradeName(w.Grade)));
            rows.Add(new KV("耐久", w.MaxDurability.ToString()));
            rows.Add(new KV("重量", w.BaseWeight.ToString()));
            rows.Add(new KV("价值", w.BaseValue.ToString()));
            rows.Add(new KV("攻击", w.BaseEquipmentAttack.ToString()));
            rows.Add(new KV("防御", w.BaseEquipmentDefense.ToString()));
            rows.Add(new KV("攻击距离", w.MinDistance + " - " + w.MaxDistance));
            AppendTable(sb, rows);

            if (!string.IsNullOrEmpty(w.Desc))
            {
                sb.AppendLine();
                sb.AppendLine(w.Desc);
            }
            if (!string.IsNullOrEmpty(w.FunctionDesc) && w.FunctionDesc != w.Desc)
            {
                sb.AppendLine();
                sb.AppendLine(w.FunctionDesc);
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(w.Name), sb.ToString());
        }

        // ============ 出身特质 ProtagonistFeature ============
        private static Tuple<string, string> RenderProtagonistFeature(int id)
        {
            var p = ProtagonistFeature.Instance[id];
            if (p == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("# " + p.Name);
            sb.AppendLine();

            // 出身特质通常属性较少，主要靠文字描述
            if (!string.IsNullOrEmpty(p.Desc))
            {
                sb.AppendLine(p.Desc);
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(p.Name), sb.ToString());
        }

        // ============ 内力属性 NeiliType ============
        private static Tuple<string, string> RenderNeiliType(int id)
        {
            var n = NeiliType.Instance[id];
            if (n == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("# " + n.Name);
            sb.AppendLine();

            if (!string.IsNullOrEmpty(n.Desc))
            {
                sb.AppendLine(n.Desc);
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(n.Name), sb.ToString());
        }

        // ============ 招式 TrickType ============
        private static Tuple<string, string> RenderTrickType(int id)
        {
            var t = TrickType.Instance[id];
            if (t == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("# " + t.Name);
            sb.AppendLine();

            if (!string.IsNullOrEmpty(t.Desc))
            {
                sb.AppendLine(t.Desc);
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(t.Name), sb.ToString());
        }

        // ============ 辅助 ============
        private struct KV { public string K; public string V; public KV(string k, string v) { K = k; V = v; } }

        private static void AppendTable(StringBuilder sb, List<KV> rows)
        {
            if (rows.Count == 0) return;
            sb.AppendLine("| 属性 | 值 |");
            sb.AppendLine("|---|---|");
            foreach (var r in rows)
                sb.AppendLine("| " + r.K + " | " + r.V + " |");
        }

        private static void AddIfNonZero(List<KV> rows, string label, short v)
        {
            if (v != 0) rows.Add(new KV(label, v.ToString()));
        }
        private static void AddIfNonZero(List<KV> rows, string label, sbyte v)
        {
            if (v != 0) rows.Add(new KV(label, v.ToString()));
        }

        private static void AppendFootnote(StringBuilder sb)
        {
            sb.AppendLine();
            sb.AppendLine("> 具体数值以游戏内为准。");
        }

        /// <summary>安全取配置子表的 Name（越界/异常返回"未知"）。</summary>
        private static string SafeName(Func<string> getter)
        {
            try { var v = getter(); return string.IsNullOrEmpty(v) ? "未知" : v; }
            catch { return "未知"; }
        }

        private static string NullToEmpty(string s) => s ?? "";
    }
}
