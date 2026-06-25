using System;
using System.Collections.Generic;
using System.Text;
using Config;
using EncyclopediaExporter.Models;
using GameData.Domains.Combat;
using GameData.Domains.Item;

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
                case ReferenceInsertType.ArmorTips: return RenderArmor(id);
                case ReferenceInsertType.ClothingTips: return RenderClothing(id);
                case ReferenceInsertType.AccessoryTips: return RenderAccessory(id);
                case ReferenceInsertType.CraftToolTips: return RenderCraftTool(id);
                case ReferenceInsertType.MedicineTips: return RenderMedicine(id);
                case ReferenceInsertType.MaterialTips: return RenderMaterial(id);
                case ReferenceInsertType.SkillBookTips: return RenderSkillBook(id);
                case ReferenceInsertType.MiscTips: return RenderMisc(id);
                case ReferenceInsertType.FoodTips: return RenderFood(id);
                case ReferenceInsertType.TeaWineTips: return RenderTeaWine(id);
                case ReferenceInsertType.CarrierTips: return RenderCarrier(id);
                case ReferenceInsertType.CricketTips: return RenderCricket(id);
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
            // 次要属性（内力/格子消耗/最适武器）内联，不占行
            var extras = new List<string>();
            if (s.TotalObtainableNeili != 0) extras.Add("**内力** " + s.TotalObtainableNeili);
            if (s.GridCost != 0) extras.Add("**消耗格子** " + s.GridCost);
            if (s.MobilityCost != 0) extras.Add("**脚力** " + s.MobilityCost + "%");
            if (s.MostFittingWeaponID > 0)
                extras.Add("**最适** " + SafeName(() => Weapon.Instance[s.MostFittingWeaponID]?.Name));
            if (extras.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine(string.Join("　", extras));
            }

            // 提供栏位（SpecificGrids）：装上此功法后，摧破/轻灵/护体/奇窍各增加的格子数。
            // 下标来自 TooltipCombatSkill：[0]AttackGrid=摧破 [1]AgileGrid=轻灵 [2]DefenceGrid=护体 [3]SpecialGrid=奇窍
            // GenericGrid 是通用格子（可分配到任意栏位）。
            if (s.SpecificGrids != null && s.SpecificGrids.Length >= 4)
            {
                var grids = new List<string>();
                string[] gridNames = { "摧破", "轻灵", "护体", "奇窍" };
                for (int i = 0; i < 4; i++)
                    if (s.SpecificGrids[i] != 0) grids.Add(gridNames[i] + " +" + s.SpecificGrids[i]);
                if (s.GenericGrid != 0) grids.Add("通用 +" + s.GenericGrid);
                if (grids.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("**栏位** " + string.Join("　", grids));
                }
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

                // 命中部位倾向（摧破类，对齐游戏 CombatSkillHitParts 渲染）
                AppendHitParts(sb, s.InjuryPartAtkRateDistribution);

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

            // 运功效果（装备此功法获得的属性加成）——build 搭配的核心数据。两个来源合并：
            // 1) PropertyAddList：绝技类功法的固定属性加成（如"+膂力 10"）
            // 2) CalcDefaultNeiliAllocationBonus：内功/轻功等按内力分配计算的属性加成
            //    （扩展方法 NeiliAllocationBonusHelper.CalcDefaultNeiliAllocationBonus，依赖
            //     GetMapping/GlobalConfig/NeiliAllocationEffect，链太深故反射调用拿结果）
            var effectParts = new List<string>();
            if (s.PropertyAddList != null)
            {
                foreach (var pv in s.PropertyAddList)
                {
                    string sign = pv.Value >= 0 ? "+" : "";
                    effectParts.Add("**" + GetPropertyName(pv.PropertyId) + "** " + sign + pv.Value);
                }
            }
            // 内力分配加成（内功/轻功的核心属性来源）
            var neiliBonus = CalcNeiliAllocationBonus(s);
            if (neiliBonus != null)
            {
                foreach (var kv in neiliBonus)
                {
                    effectParts.Add("**" + GetPropertyName(kv.Key) + "** +" + kv.Value);
                }
            }
            if (effectParts.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 运功效果");
                // 多个时按 4 个一行排版，避免一行过长
                for (int i = 0; i < effectParts.Count; i += 4)
                {
                    int take = Math.Min(4, effectParts.Count - i);
                    sb.AppendLine(string.Join("　", effectParts.GetRange(i, take)));
                }
            }

            // 运功路径（突破起止位置）
            if (!string.IsNullOrEmpty(s.BreakStart) || !string.IsNullOrEmpty(s.BreakEnd))
            {
                sb.AppendLine();
                sb.AppendLine("**运功路径** " + NullToEmpty(s.BreakStart) + " → " + NullToEmpty(s.BreakEnd));
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

        /// <summary>
        /// 命中部位倾向（对齐 CombatSkillHitParts.SetData 的渲染逻辑）。
        /// 配置序下标：0胸 1腹 2头 3左手 4右手 5左腿 6右腿。
        /// 百分比 = round(rate*1000/sum)/10，rate<=0 显示"—"（不以此部位为目标）。
        /// 全 0 或数组不足 7 位则跳过（与游戏 InjuryPartAtkRateDistribution.Length<1 不渲染一致）。
        /// </summary>
        private static void AppendHitParts(StringBuilder sb, sbyte[] rates)
        {
            if (rates == null || rates.Length < 7) return;
            int sum = 0;
            for (int i = 0; i < 7; i++) sum += rates[i];
            if (sum <= 0) return;

            // 配置下标 → 部位名（与 MouseTipConstant.HitPartNamesByConfig 一致）
            string[] names = { "胸", "腹", "头", "左手", "右手", "左腿", "右腿" };
            var headers = new List<string>(7);
            var values = new List<string>(7);
            for (int i = 0; i < 7; i++)
            {
                headers.Add(names[i]);
                if (rates[i] <= 0)
                {
                    values.Add("—");
                }
                else
                {
                    // 游戏算法：Math.Max(1, round(rate*1000/sum))，再拆成 X.Y%
                    int permille = Math.Max(1, (int)(rates[i] * 1000.0 / sum + 0.5));
                    values.Add((permille / 10) + "." + (permille % 10) + "%");
                }
            }
            sb.AppendLine();
            sb.AppendLine("## 命中部位倾向");
            AppendCompactTable(sb, headers, values);
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

        /// <summary>
        /// 反射调用 CombatSkillItem.CalcDefaultNeiliAllocationBonus()，取得内力分配带来的属性加成。
        /// 该扩展方法（NeiliAllocationBonusHelper.CalcDefaultNeiliAllocationBonus）按内力分配计算
        /// 内功/轻功等功法的属性加成，是 build 搭配的核心数据。
        /// 返回 List&lt;(short propertyId, int value)&gt;；失败返回 null。
        /// </summary>
        private static List<KeyValuePair<short, int>> CalcNeiliAllocationBonus(CombatSkillItem s)
        {
            if (s == null) return null;
            try
            {
                // 扩展方法定义在 NeiliAllocationBonusHelper 静态类，通过反射按方法名查找
                System.Reflection.MethodInfo method = null;
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    var helperType = asm.GetType("NeiliAllocationBonusHelper");
                    if (helperType != null)
                    {
                        method = helperType.GetMethod("CalcDefaultNeiliAllocationBonus");
                        if (method != null) break;
                    }
                }
                if (method == null) return null;

                var result = method.Invoke(null, new object[] { s }) as System.Collections.IList;
                if (result == null || result.Count == 0) return null;

                var list = new List<KeyValuePair<short, int>>(result.Count);
                // 返回类型是 List<ValueTuple<short,int>>，用反射读 Item1/Item2
                foreach (var item in result)
                {
                    var t = item.GetType();
                    short pid = (short)t.GetField("Item1").GetValue(item);
                    int val = (int)t.GetField("Item2").GetValue(item);
                    if (val > 0) list.Add(new KeyValuePair<short, int>(pid, val));
                }
                return list.Count > 0 ? list : null;
            }
            catch { /* 反射失败不影响整体渲染 */ }
            return null;
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
        // 字段布局基于 TooltipWeapon 实测 dump（破脉霜/铁手）：
        // 通用区 → 兵器属性(招式/范围/耗时/破甲/坚韧/变招/追击) → 攻击属性(破体破气)
        // → 命中属性(力道/精妙/迅疾/动心) → 含有毒素(仅毒器) → 特殊词条
        // 数值基数 PowerInfo.Default=100，故破甲=BaseEquipmentAttack/100，破体%=BasePenetrationFactor×(100-DefaultInnerRatio)/100
        private static Tuple<string, string> RenderWeapon(int id)
        {
            var w = Weapon.Instance[id];
            if (w == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("# " + w.Name);
            sb.AppendLine();

            // 聚合行：品阶 · 兵器 · 子类（对齐 dump 的 TextName 下信息）
            var tags = new List<string>
            {
                TipTypeConfig.GradeDisplay(w.Grade),
                "兵器",
                WeaponSubTypeLabel(w)
            };
            sb.AppendLine("**" + string.Join(" · ", tags) + "**");
            // 次要属性内联
            sb.AppendLine("价值 " + w.BaseValue + "　重量 " + FormatItemWeight(w.BaseWeight));

            // 描述
            if (!string.IsNullOrEmpty(w.Desc))
            {
                sb.AppendLine();
                sb.AppendLine(w.Desc);
            }

            // ===== 兵器属性 =====
            sb.AppendLine();
            sb.AppendLine("## 兵器属性");

            // 表一：招式 / 攻击范围 / 攻击耗时
            var p1h = new List<string>();
            var p1v = new List<string>();
            p1h.Add("招式"); p1v.Add(WeaponTricksLabel(w));
            p1h.Add("攻击范围"); p1v.Add("近" + (w.MinDistance / 10f).ToString("F1") + "远" + (w.MaxDistance / 10f).ToString("F1"));
            p1h.Add("攻击耗时"); p1v.Add(FormatPrepareFrame(w.BaseStartupFrames) + "秒");
            AppendCompactTable(sb, p1h, p1v);

            // 表二：破甲 / 坚韧 / 变招积蓄 / 追击概率
            sb.AppendLine();
            AppendCompactTable(sb,
                new List<string> { "破甲", "坚韧", "变招积蓄", "追击概率" },
                new List<string> {
                    (w.BaseEquipmentAttack / 100f).ToString("F2"),
                    (w.BaseEquipmentDefense / 100f).ToString("F2"),
                    w.ChangeTrickPercent + "%",
                    w.PursueAttackFactor + "%"
                });

            // ===== 攻击属性：破体/破气 =====
            // 破体 = BasePenetrationFactor × (100-DefaultInnerRatio) / 100
            // 破气 = BasePenetrationFactor × DefaultInnerRatio / 100（百科环境 Power=100）
            sb.AppendLine();
            sb.AppendLine("## 攻击属性");
            int pen = w.BasePenetrationFactor;
            int innerRatio = w.DefaultInnerRatio;
            AppendCompactTable(sb,
                new List<string> { "破体", "破气" },
                new List<string> {
                    (pen * (100 - innerRatio) / 100) + "%",
                    (pen * innerRatio / 100) + "%"
                });

            // ===== 命中属性：力道/精妙/迅疾/动心 =====
            // dump: 0% 的项有灰色遮罩；值 = 100 + BaseHitFactors[i]，0 时显示"—"
            // HitOrAvoidShorts 是 fixed short[4]，用索引器访问
                sb.AppendLine();
                sb.AppendLine("## 命中属性");
                string[] hitNames = { "力道", "精妙", "迅疾", "动心" };
                var hh = new List<string>();
                var hv = new List<string>();
                for (int i = 0; i < 4; i++)
                {
                    short v = w.BaseHitFactors[i];
                    hh.Add(hitNames[i]);
                    hv.Add(v == 0 ? "—" : (100 + v) + "%");
                }
                AppendCompactTable(sb, hh, hv);

            // ===== 含有毒素（仅毒器，InnatePoisons 非全 0 时）=====
            AppendWeaponPoisons(sb, w.InnatePoisons);

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(w.Name), sb.ToString());
        }

        /// <summary>武器子类标签：材料(ResourceType.Name) + 子类名。对齐 dump MaterialLabel。
        /// ResourceType<0 时仅子类名；子类越界用"未知"。</summary>
        private static string WeaponSubTypeLabel(WeaponItem w)
        {
            string sub = TipTypeConfig.WeaponSubTypeName(w.ItemSubType);
            if (w.ResourceType >= 0)
            {
                string res = SafeName(() => Config.ResourceType.Instance[w.ResourceType]?.Name);
                if (!string.IsNullOrEmpty(res) && res != "未知") return res + sub;
            }
            return sub;
        }

        /// <summary>招式标签：Tricks → 招式名列表（崩/点/拿/药，带颜色由 FontColor 决定）。
        /// dump 显示同招式可重复（如"崩×3"），这里聚合计数。</summary>
        private static string WeaponTricksLabel(WeaponItem w)
        {
            if (w.Tricks == null || w.Tricks.Count == 0) return "—";
            var counts = new Dictionary<sbyte, int>();
            var order = new List<sbyte>();
            foreach (var t in w.Tricks)
            {
                if (!counts.ContainsKey(t)) { counts[t] = 0; order.Add(t); }
                counts[t]++;
            }
            var parts = new List<string>();
            foreach (var t in order)
            {
                string name = SafeName(() => Config.TrickType.Instance[t]?.Name);
                parts.Add(counts[t] > 1 ? name + "×" + counts[t] : name);
            }
            return string.Join("、", parts);
        }

        /// <summary>攻击耗时（秒）：直接调用游戏 CFormula（与卡片算法完全一致）。
        /// CalcAttackStartupOrRecoveryFrame(100, BaseStartupFrames) / 60f。
        /// 百科环境 attackSpeed=100（PowerInfo.Default）。</summary>
        private static string FormatPrepareFrame(int frames)
        {
            int adjusted = CFormula.CalcAttackStartupOrRecoveryFrame(100, frames);
            return (adjusted / 60f).ToString("F2");
        }

        /// <summary>武器自带毒区块（仅毒器）。6种毒：烈/郁/寒/赤/腐/幻。
        /// dump 显示带星级(Level)，这里简化为毒名+数值，全 0 不输出。</summary>
        private static void AppendWeaponPoisons(StringBuilder sb, PoisonsAndLevels poisons)
        {
            if (!poisons.IsNonZero()) return;
            sb.AppendLine();
            sb.AppendLine("## 含有毒素");
            // 6 种毒的 sortingOrder → type 映射对齐游戏 PoisonType.GetTypeBySortingOrder
            // 毒名从 Poison.Instance[type].Name 读（运行时可用）
            var headers = new List<string>();
            var values = new List<string>();
            for (sbyte b = 0; b < 6; b++)
            {
                sbyte type = PoisonType.GetTypeBySortingOrder(b);
                short val = poisons.GetValue(type);
                if (val <= 0) continue;
                string name = SafeName(() => Config.Poison.Instance[type]?.Name);
                headers.Add(name);
                values.Add(val.ToString());
            }
            if (headers.Count > 0) AppendCompactTable(sb, headers, values);
        }

        // ============ 盔甲 Armor ============
        // 字段布局基于 TooltipArmor 实测 dump（精钢环臂）：
        // 通用区 → 护具属性(破刃/坚韧/外伤降低/内伤降低) → 防御属性(御体/御气) → 特殊词条
        // 破刃=BaseEquipmentAttack/100，坚韧=BaseEquipmentDefense/100
        // 外伤降低=BaseInjuryFactors.Outer%，御体=BasePenetrationResistFactors.Outer%（Power=100）
        private static Tuple<string, string> RenderArmor(int id)
        {
            var a = Armor.Instance[id];
            if (a == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("# " + a.Name);
            sb.AppendLine();

            // 聚合行：品阶 · 护具 · 子类
            var tags = new List<string>
            {
                TipTypeConfig.GradeDisplay(a.Grade),
                "护具",
                ArmorSubTypeLabel(a)
            };
            sb.AppendLine("**" + string.Join(" · ", tags) + "**");
            sb.AppendLine("价值 " + a.BaseValue + "　重量 " + FormatItemWeight(a.BaseWeight));

            if (!string.IsNullOrEmpty(a.Desc))
            {
                sb.AppendLine();
                sb.AppendLine(a.Desc);
            }

            // ===== 护具属性 =====
            sb.AppendLine();
            sb.AppendLine("## 护具属性");
            AppendCompactTable(sb,
                new List<string> { "破刃", "坚韧", "外伤降低", "内伤降低" },
                new List<string> {
                    (a.BaseEquipmentAttack / 100f).ToString("F2"),
                    (a.BaseEquipmentDefense / 100f).ToString("F2"),
                    a.BaseInjuryFactors.Outer + "%",
                    a.BaseInjuryFactors.Inner + "%"
                });

            // ===== 防御属性：御体/御气 =====
            // dump 精钢环臂: 御体70% 御气0%。公式: 御体=BasePenetrationResistFactors.Outer×Power/100
            sb.AppendLine();
            sb.AppendLine("## 防御属性");
            AppendCompactTable(sb,
                new List<string> { "御体", "御气" },
                new List<string> {
                    a.BasePenetrationResistFactors.Outer + "%",
                    a.BasePenetrationResistFactors.Inner + "%"
                });

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(a.Name), sb.ToString());
        }

        /// <summary>盔甲子类标签：材料 + 子类（暂用 ItemType 文案，子类枚举待补）。</summary>
        private static string ArmorSubTypeLabel(ArmorItem a)
        {
            string sub = "护具"; // 盔甲子类名枚举待提取，暂用通用名
            if (a.ResourceType >= 0)
            {
                string res = SafeName(() => Config.ResourceType.Instance[a.ResourceType]?.Name);
                if (!string.IsNullOrEmpty(res) && res != "未知") return res + sub;
            }
            return sub;
        }

        /// <summary>物品重量格式化（对齐 NumberFormatUtils.FormatItemWeight：/100 一位小数）。</summary>
        private static string FormatItemWeight(int weight)
        {
            return (weight / 100f).ToString("F1");
        }

        /// <summary>通用区头部：标题 + 聚合行(品阶·类型·子类) + 价值重量 + 描述。
        /// 各物品 RenderXxx 共用，typeLabel 如"衣装"/"宝物"/"药毒"。</summary>
        private static void AppendItemHeader(StringBuilder sb, string name, sbyte grade,
            string typeLabel, string subTypeLabel, int baseValue, int baseWeight,
            string desc, string functionDesc)
        {
            sb.AppendLine("# " + name);
            sb.AppendLine();
            var tags = new List<string> { TipTypeConfig.GradeDisplay(grade), typeLabel, subTypeLabel };
            sb.AppendLine("**" + string.Join(" · ", tags) + "**");
            sb.AppendLine("价值 " + baseValue + "　重量 " + FormatItemWeight(baseWeight));
            if (!string.IsNullOrEmpty(desc))
            {
                sb.AppendLine();
                sb.AppendLine(desc);
            }
            if (!string.IsNullOrEmpty(functionDesc) && functionDesc != desc)
            {
                sb.AppendLine();
                sb.AppendLine(functionDesc);
            }
        }

        // ============ 衣装 Clothing ============
        // dump（匠作服）：通用区 + 衣装属性(魅力，男/女)。魅力来自 AvatarAsset 运行时数据，
        // 模板无固定值，这里标注"以游戏内为准"。
        private static Tuple<string, string> RenderClothing(int id)
        {
            var c = Clothing.Instance[id];
            if (c == null) return null;

            var sb = new StringBuilder();
            AppendItemHeader(sb, c.Name, c.Grade, "衣装", "衣装",
                c.BaseValue, c.BaseWeight, c.Desc, c.FunctionDesc);

            sb.AppendLine();
            sb.AppendLine("## 衣装属性");
            sb.AppendLine("**魅力** 以游戏内为准（随外观资产变化）");

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(c.Name), sb.ToString());
        }

        // ============ 宝物 Accessory ============
        // dump（兽头方囊）：通用区 + 宝物属性(行囊大小)。饰品还可能有掉落/捕获/探索/属性加成。
        private static Tuple<string, string> RenderAccessory(int id)
        {
            var a = Accessory.Instance[id];
            if (a == null) return null;

            var sb = new StringBuilder();
            AppendItemHeader(sb, a.Name, a.Grade, "宝物", "宝物",
                a.BaseValue, a.BaseWeight, a.Desc, a.FunctionDesc);

            // 宝物属性：按非零字段显示
            var rows = new List<KV>();
            if (a.MaxInventoryLoadBonus > 0)
                rows.Add(new KV("行囊大小", (a.MaxInventoryLoadBonus / 100f).ToString("F1")));
            if (a.DropRateBonus != 0) rows.Add(new KV("掉落加成", a.DropRateBonus + "%"));
            if (a.BaseCaptureRateBonus != 0) rows.Add(new KV("捕获加成", a.BaseCaptureRateBonus + "%"));
            if (a.BaseExploreBonusRate != 0) rows.Add(new KV("探索加成", a.BaseExploreBonusRate + "%"));
            if (a.Strength != 0) rows.Add(new KV("膂力", a.Strength.ToString()));
            if (a.Dexterity != 0) rows.Add(new KV("灵敏", a.Dexterity.ToString()));
            if (a.Concentration != 0) rows.Add(new KV("定力", a.Concentration.ToString()));
            if (rows.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 宝物属性");
                AppendTable(sb, rows);
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(a.Name), sb.ToString());
        }

        // ============ 工具 CraftTool ============
        // dump（赤纹炼）：通用区 + 工具效果(单一造诣加成，如"锻造造诣 +40")。
        // 造诣名 = LifeSkill.Instance[RequiredLifeSkillTypes[0]].Name，加成 = AttainmentBonus。
        private static Tuple<string, string> RenderCraftTool(int id)
        {
            var t = CraftTool.Instance[id];
            if (t == null) return null;

            var sb = new StringBuilder();
            AppendItemHeader(sb, t.Name, t.Grade, "工具", "工具",
                t.BaseValue, t.BaseWeight, t.Desc, t.FunctionDesc);

            if (t.AttainmentBonus != 0 && t.RequiredLifeSkillTypes != null && t.RequiredLifeSkillTypes.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 工具效果");
                var parts = new List<string>();
                foreach (sbyte lsType in t.RequiredLifeSkillTypes)
                {
                    // RequiredLifeSkillTypes 存的是技艺类型枚举（6=锻造 7=制木 8=医术...），
                    // 必须查 LifeSkillType 表（类型名：锻造/制木/医术...），不能查 LifeSkill 表
                    // （后者是具体功法条目，如「霓裳曲谱」，用类型值当 id 会查到不相干的条目）
                    string lsName = SafeName(() => Config.LifeSkillType.Instance[lsType]?.Name);
                    parts.Add("**" + lsName + "造诣** +" + t.AttainmentBonus);
                }
                sb.AppendLine(string.Join("　", parts));
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(t.Name), sb.ToString());
        }

        // ============ 药毒 Medicine ============
        // dump（止血散）：通用区 + 服食效果(治疗描述) + 战斗使用(机略消耗)。
        // 治疗描述来自 SpecialEffectId，用 CommonUtils.GetSpecialEffectDesc 还原（含图标占位符替换）。
        private static Tuple<string, string> RenderMedicine(int id)
        {
            var m = Medicine.Instance[id];
            if (m == null) return null;

            var sb = new StringBuilder();
            AppendItemHeader(sb, m.Name, m.Grade, "药毒", "药毒",
                m.BaseValue, m.BaseWeight, m.Desc, m.FunctionDesc);

            // 服食效果
            string effect = GetSpecialEffectDesc(m.SpecialEffectId);
            bool hasDuration = m.Duration > 0;
            if (!string.IsNullOrEmpty(effect) || hasDuration)
            {
                sb.AppendLine();
                sb.AppendLine("## 服食效果");
                if (!string.IsNullOrEmpty(effect)) sb.AppendLine(effect);
                if (hasDuration) sb.AppendLine("**持续时间** " + m.Duration + " 月");
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(m.Name), sb.ToString());
        }

        // ============ 引子 Material ============
        // dump（大豆）：通用区 + 功能描述（已在 header 输出）。材料无专属属性区。
        private static Tuple<string, string> RenderMaterial(int id)
        {
            var m = Config.Material.Instance[id];
            if (m == null) return null;

            var sb = new StringBuilder();
            AppendItemHeader(sb, m.Name, m.Grade, "引子", "引子",
                m.BaseValue, m.BaseWeight, m.Desc, m.FunctionDesc);

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(m.Name), sb.ToString());
        }

        // ============ 书籍 SkillBook ============
        // dump（《蔡氏五弄》）：通用区 + 书籍内容(百科显示"无法查看")。
        // 导出技艺类型 + 遗惠点数，便于 AI 理解。
        private static Tuple<string, string> RenderSkillBook(int id)
        {
            var b = SkillBook.Instance[id];
            if (b == null) return null;

            var sb = new StringBuilder();
            AppendItemHeader(sb, b.Name, b.Grade, "书籍", "书籍",
                b.BaseValue, b.BaseWeight, b.Desc, b.FunctionDesc);

            // 书籍类型：技艺类(LifeSkillType≥0) 或 功法类(CombatSkillType≥0)
            var bookInfo = new List<string>();
            if (b.LifeSkillType >= 0)
            {
                string lsName = SafeName(() => LifeSkill.Instance[b.LifeSkillType]?.Name);
                bookInfo.Add("**技艺** " + lsName);
            }
            if (b.CombatSkillType >= 0)
            {
                string csName = SafeName(() => CombatSkillType.Instance[b.CombatSkillType]?.Name);
                bookInfo.Add("**功法类型** " + csName);
            }
            if (b.LegacyPoint > 0) bookInfo.Add("**遗惠** " + b.LegacyPoint);
            if (bookInfo.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 书籍内容");
                sb.AppendLine(string.Join("　", bookInfo));
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(b.Name), sb.ToString());
        }

        // ============ 杂物 Misc ============
        // dump（紫砂促织罐）：通用区 + 服食效果(促织罐：耐久恢复/疗伤几率)。
        // 杂物字段多变，按非零显示促织罐相关字段。
        private static Tuple<string, string> RenderMisc(int id)
        {
            var m = Misc.Instance[id];
            if (m == null) return null;

            var sb = new StringBuilder();
            AppendItemHeader(sb, m.Name, m.Grade, "杂物", "杂物",
                m.BaseValue, m.BaseWeight, m.Desc, m.FunctionDesc);

            // 促织罐类：耐久恢复/疗伤几率（dump 紫砂促织罐实测字段）
            var rows = new List<KV>();
            if (m.CricketHealInjuryOdds > 0)
                rows.Add(new KV("疗伤几率", m.CricketHealInjuryOdds + "%"));
            if (m.Neili != 0) rows.Add(new KV("内力", m.Neili.ToString()));
            if (rows.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 服食效果");
                AppendTable(sb, rows);
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(m.Name), sb.ToString());
        }

        // ============ 食物 Food ============
        // 无 dump 样本，按 TooltipFood.RefreshAddProperty 实现：
        // 遍历六大属性(膂力/灵敏/定力/体质/根骨/悟性)+命中+穿透+闪避+恢复，非零输出。
        private static Tuple<string, string> RenderFood(int id)
        {
            var f = Food.Instance[id];
            if (f == null) return null;

            var sb = new StringBuilder();
            AppendItemHeader(sb, f.Name, f.Grade, "食物", "食物",
                f.BaseValue, f.BaseWeight, f.Desc, f.FunctionDesc);

            // 食物效果：六大属性 + 命中三段（数据驱动，非零才显示）
            var rows = new List<KV>();
            AddIfNonZero(rows, "膂力", f.Strength);
            AddIfNonZero(rows, "体质", f.Vitality);
            AddIfNonZero(rows, "灵敏", f.Dexterity);
            AddIfNonZero(rows, "根骨", f.Energy);
            AddIfNonZero(rows, "悟性", f.Intelligence);
            AddIfNonZero(rows, "定力", f.Concentration);
            AddIfNonZero(rows, "力道命中", f.HitRateStrength);
            AddIfNonZero(rows, "精妙命中", f.HitRateTechnique);
            AddIfNonZero(rows, "迅疾命中", f.HitRateSpeed);
            if (rows.Count > 0 || f.Duration > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 食物效果");
                if (f.Duration > 0) sb.AppendLine("**持续时间** " + f.Duration + " 月");
                if (rows.Count > 0) AppendTable(sb, rows);
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(f.Name), sb.ToString());
        }

        // ============ 茶酒 TeaWine ============
        // 无 dump 样本，按 TooltipTeaWine 实现：持续时间 + 命中/穿透/闪避/御 + 内息紊乱变化。
        private static Tuple<string, string> RenderTeaWine(int id)
        {
            var t = TeaWine.Instance[id];
            if (t == null) return null;

            var sb = new StringBuilder();
            AppendItemHeader(sb, t.Name, t.Grade, "茶酒", "茶酒",
                t.BaseValue, t.BaseWeight, t.Desc, t.FunctionDesc);

            var rows = new List<KV>();
            AddIfNonZero(rows, "力道命中", t.HitRateStrength);
            AddIfNonZero(rows, "精妙命中", t.HitRateTechnique);
            AddIfNonZero(rows, "迅疾命中", t.HitRateSpeed);
            AddIfNonZero(rows, "心神命中", t.HitRateMind);
            AddIfNonZero(rows, "破体", t.PenetrateOfOuter);
            AddIfNonZero(rows, "破气", t.PenetrateOfInner);
            AddIfNonZero(rows, "御体", t.PenetrateResistOfOuter);
            AddIfNonZero(rows, "御气", t.PenetrateResistOfInner);
            if (t.DirectChangeOfQiDisorder != 0) rows.Add(new KV("内息紊乱", t.DirectChangeOfQiDisorder.ToString()));
            if (rows.Count > 0 || t.Duration > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 茶酒效果");
                if (t.Duration > 0) sb.AppendLine("**持续时间** " + t.Duration + " 月");
                if (rows.Count > 0) AppendTable(sb, rows);
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(t.Name), sb.ToString());
        }

        // ============ 代步 Carrier ============
        // 无 dump 样本，按 TooltipCarrier.RefreshCarrierProperty 实现：
        // 旅行速度 + 行囊 + 捕获 + 探索 + 驯服点 + 是否飞行。
        private static Tuple<string, string> RenderCarrier(int id)
        {
            var c = Carrier.Instance[id];
            if (c == null) return null;

            var sb = new StringBuilder();
            AppendItemHeader(sb, c.Name, c.Grade, "代步", "代步",
                c.BaseValue, c.BaseWeight, c.Desc, c.FunctionDesc);

            var rows = new List<KV>();
            if (c.BaseTravelTimeReduction > 0) rows.Add(new KV("旅行速度", "-" + c.BaseTravelTimeReduction + "%"));
            if (c.BaseMaxInventoryLoadBonus > 0)
                rows.Add(new KV("行囊大小", "+" + (c.BaseMaxInventoryLoadBonus / 100f).ToString("F1")));
            if (c.BaseCaptureRateBonus != 0) rows.Add(new KV("捕获加成", c.BaseCaptureRateBonus + "%"));
            if (c.BaseExploreBonusRate != 0) rows.Add(new KV("探索加成", c.BaseExploreBonusRate + "%"));
            if (c.TamePoint > 0) rows.Add(new KV("驯服点", c.TamePoint.ToString()));
            if (c.IsFlying) rows.Add(new KV("飞行", "可"));
            if (rows.Count > 0)
            {
                sb.AppendLine();
                sb.AppendLine("## 代步属性");
                AppendTable(sb, rows);
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(c.Name), sb.ToString());
        }

        // ============ 促织 Cricket ============
        // 百科里促织Tips 参数格式特殊：Params[0] = "{n,}"（品类索引）。
        // 卡片实际用 Part1+Part2 双参数组合（颜色+部位），这里取品类基础信息（名+描述+品阶）。
        // 完整部位组合渲染待有 dump 样本后补。
        private static Tuple<string, string> RenderCricket(int id)
        {
            var c = Cricket.Instance[id];
            if (c == null) return null;

            var sb = new StringBuilder();
            sb.AppendLine("# " + c.Name);
            sb.AppendLine();
            sb.AppendLine("**" + TipTypeConfig.GradeDisplay(c.Grade) + " · 促织**");
            sb.AppendLine("价值 " + c.BaseValue + "　重量 " + FormatItemWeight(c.BaseWeight));

            if (!string.IsNullOrEmpty(c.Desc))
            {
                sb.AppendLine();
                sb.AppendLine(c.Desc);
            }

            AppendFootnote(sb);
            return Tuple.Create(DataParser.SafeName(c.Name), sb.ToString());
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
            var t = Config.TrickType.Instance[id];
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
