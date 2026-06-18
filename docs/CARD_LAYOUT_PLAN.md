# 物品卡片重建规划

> 基于 CardProbe 探针对游戏实际卡片（`TooltipXxx.Refresh`）的 dump 实测 + 反编译源码核对。
> 探针数据见 `_probe/samples/`，原始 log 见 `_probe/raw/`。
> 所有样本均为百科环境（`TemplateDataOnly=True, CharId=-1`），数值来自 `WeaponItem`/`ArmorItem` 等模板字段 + `ItemDisplayData(itemType, templateId)` 构造（`PowerInfo.Default` = Power 100）。

---

## 一、数据来源与计算基数

### 关键事实：百科环境的威力基数

百科链接点击物品时，游戏构造 `new ItemDisplayData(itemType, templateId)`（`LinkElement.cs:350`），其构造函数（`ItemDisplayData.cs:893-938`）会：

```csharp
case 0: // 武器
    EquipmentAttack = weaponItem.BaseEquipmentAttack;      // 破甲基数
    EquipmentDefense = weaponItem.BaseEquipmentDefense;    // 坚韧基数
    HitAvoidFactor = weaponItem.BaseHitFactors;            // 4种命中
    PenetrationInfo.Item1 = weaponItem.BasePenetrationFactor;
case 1: // 盔甲
    ...（同构，读 ArmorItem）
PowerInfo = ItemPowerInfo.Default;  // Power=100, MaxPower=100, RequirementsPower=100
```

**所以所有"威力衍生"字段在百科环境下 = `模板字段 × 100 / 100` = 模板字段本身**。这让我们能用纯模板数据精确复现百科卡片数值。

### 数值公式（已用 dump 验证）

| 卡片字段 | 公式（百科环境） | 验证样本 |
|---|---|---|
| 破甲 | `BaseEquipmentAttack / 100f` (F2) | 铁手: 530→5.30 ✓ |
| 坚韧 | `BaseEquipmentDefense / 100f` (F2) | 铁手: 950→9.50 ✓ |
| 变招积蓄 | `ChangeTrickPercent`%（直接读）| 铁手: 110% ✓ |
| 追击概率 | `PursueAttackFactor`%（直接读）| 铁手: 13% ✓ |
| 破体% | `BasePenetrationFactor × (100 - DefaultInnerRatio) / 100` | 破脉霜: 120% ✓ |
| 破气% | `BasePenetrationFactor × DefaultInnerRatio / 100` | 破脉霜: 0% ✓ |
| 力道/精妙/迅疾/动心% | `BaseHitFactors[i] != 0 ? (100 + [i])% : 0%` | 铁手: 115/110/85/0 ✓ |
| 攻击耗时(秒) | `CalcAttackStartupOrRecoveryFrame(100, BaseStartupFrames) / 60f` (F2) | 铁手: 0.77 ✓ |
| 攻击范围 | `MinDistance/10` ~ `MaxDistance/10` (F1) | 铁手: 2.0-5.0 ✓ |

`CalcAttackStartupOrRecoveryFrame(100, frames)` 算法（`CFormula.cs:108`）：
```
attackSpeed=100 时: frames - frames × 100 × AttackSpeedFactor / 1000
```
（AttackSpeedFactor 是 GlobalConfig 常量，需运行时读，或硬编码常见值）

---

## 二、各类卡片字段布局（实测）

### 卡片整体结构（所有物品通用骨架）

```
TooltipXxx
└── MainPanel
    ├── CommonArea        ← 通用区（所有物品一致）
    ├── XxxArea           ← 专属属性区（各物品不同）
    ├── EffectArea        ← 攻击/防御/毒素区（仅装备类）
    ├── SpecialArea       ← 特殊效果区（部分物品）
    └── OtherArea/OperationArea ← 功能禁用 + 快捷键
```

### 通用区字段（CommonArea，所有物品一致）

| UI 节点 | 字段 | 备注 |
|---|---|---|
| TextName | Name | 物品名 |
| Grade.GradeLabel | Grade | 品阶（如"中·八品"），格式 `LK_Num_{9-Grade}` + 等级词 |
| Value.ValueLabel | BaseValue | 价值 |
| Type.TypeLabel | 类型名 | 固定文案（兵器/护具/衣装/宝物/药毒/书籍/工具/杂物/引子...）|
| Weight.WeightLabel | BaseWeight | 重量（FormatItemWeight）|
| Material.MaterialLabel | 子类 | `ResourceType.Instance[r].Name` + `LK_ItemSubType_{n}` |
| Durability（部分） | 耐久 | 书/材料显示（满耐久时武器/盔甲省略）|
| DescLabel | Desc | 描述 |
| FunctionDescLabel | FunctionDesc | 功能描述（部分物品）|

### 专属属性区（差异最大，按类型分）

#### 1. 武器（WeaponArea - 兵器属性）

| 节点 | 字段 | 显示 |
|---|---|---|
| Tricks.TextTrick[] | `Tricks` (List<sbyte>) | 招式名列表（崩/点/拿/药，带颜色）|
| Distance | `MinDistance/MaxDistance` | `近X.X远X.X` |
| PrepareFrame.Num | `BaseStartupFrames` | `攻击耗时 X.XX`（秒）|
| EquipmentAttack(破甲) | `BaseEquipmentAttack/100` | F2 |
| ChangeTrick(变招积蓄) | `ChangeTrickPercent` | `XX%` |
| EquipmentDefense(坚韧) | `BaseEquipmentDefense/100` | F2 |
| PursueRate(追击概率) | `PursueAttackFactor` | `XX%` |
| RequirmentPower | PowerInfo | `发挥威力 -（最大-）`（百科环境未达成）|

#### 2. 盔甲（WeaponArea - 护具属性，节点名复用但字段不同）

| 节点 | 字段 | 显示 |
|---|---|---|
| EquipmentAttack(破刃) | `ArmorItem.BaseEquipmentAttack/100` | F2 |
| ReduceOuterInjury(外伤降低) | ArmorItem 字段 | `XX%` |
| EquipmentDefense(坚韧) | `BaseEquipmentDefense/100` | F2 |
| ReduceInnerInjury(内伤降低) | ArmorItem 字段 | `XX%` |
| RequirmentPower | PowerInfo | 同武器 |

#### 3. 衣服（AccessoryArea - 衣装属性，极简）

| 节点 | 字段 | 显示 |
|---|---|---|
| Charm | ClothingItem 魅力字段 | `XX（男）/XX（女）` |

#### 4. 饰品/宝物（AccessoryArea - 宝物属性）

| 节点 | 字段 | 显示 |
|---|---|---|
| Inventory(行囊大小) | AccessoryItem 字段 | `X.X` |

#### 5. 药毒（EatArea + CombatUseArea）

| 区 | 节点 | 字段 |
|---|---|---|
| 服食效果 | HealTitle/SubHealInjuryContent/DamageStep | MedicineItem 治疗效果（含富文本图标）|
| 服食效果 | PropertyTime(持续时间) | 持续月数 |
| 战斗使用 | PropertyCost(机略消耗) | 消耗值 |

#### 6. 工具（ToolArea - 工具效果）

| 节点 | 字段 |
|---|---|
| PropertyItem2 | CraftToolItem 单一属性加成（如"锻造造诣 +40"）|

#### 7. 材料/书籍/杂物

- **材料**：无专属属性区，仅描述+功能描述
- **书籍**：书籍内容区（百科环境显示"无法查看"）
- **杂物**：CricketJarArea（促织罐类：耐久恢复/疗伤几率）

### 攻击/防御/毒素区（EffectArea，仅装备类）

#### 武器的 EffectArea

```
AttackArea（攻击属性）：破体% / 破气%
HitArea（命中属性）：力道% / 精妙% / 迅疾% / 动心%（0%有灰色遮罩）
PoisonArea（含有毒素）：仅毒器有，烈/郁/寒/赤/腐/幻毒 各带星级+数值
```

#### 盔甲的 EffectArea

```
DefendArea（防御属性）：御体% / 御气%
```

### 特殊效果区（SpecialArea）

`特殊词条：XXX`（如武器"摧坚"、盔甲"刚体"、毒器"药毒"）—— 来自 `EquipmentEffect`，需单独查来源。

### 功能禁用区（OtherArea）

`此物品无法 拆解/修理/淬毒/精制` —— 由 `Repairable/Poisonable/Refinable/Detachable` 等 bool 反向显示。

---

## 三、代码实现方案

### 架构：分类型渲染器 + 共用工具

当前 `TipEntryResolver` 是一个大静态类，各 `RenderXxx` 方法。扩展策略：

```
TipEntryResolver（调度入口）
├── RenderWeapon(int id)    ← 重写（战斗核心，字段最全）
├── RenderArmor(int id)     ← 新增
├── RenderClothing(int id)  ← 新增
├── RenderAccessory(int id) ← 新增
├── RenderMedicine(int id)  ← 新增
├── RenderMaterial(int id)  ← 新增
├── RenderBook(int id)      ← 新增
├── RenderCraftTool(int id) ← 新增
├── RenderMisc(int id)      ← 新增
└── 共用工具
    ├── AppendCommonArea()  ← 通用区（品阶/价值/类型/重量/子类/描述）
    ├── AppendCompactTable()← 紧凑表格（已有）
    ├── AppendDisableList() ← 功能禁用区
    ├── GetItemSubTypeName()← 子类名（LK_ItemSubType 映射）
    └── FormatItemWeight()  ← 重量格式化
```

### 关键实现细节

#### 1. 子类型名映射（硬编码表）

游戏用 `LK_ItemSubType_{n}`，我们避免依赖 LocalStringManager，需硬编码。从 `ui_language.txt` 提取（已确认存在）。各 ItemType 的 ItemSubType 枚举：

```csharp
// 武器子类（ItemType=0）：长剑/短剑/琴/针/拳套/毒器/暗器/长兵...
// 盔甲子类（ItemType=1）：头/护臂/鞋...
// 衣服子类（ItemType=3）：织造衣装/...
// 材料子类（ItemType=5）：食材/药材/...
```
→ 第一阶段先硬编码武器和盔甲的，其余按需补。

#### 2. 招式颜色（武器 Tricks）

dump 显示招式名带颜色：`崩/点/拿` 是绿色(#73A9A1)，`药` 是青色(#B5DEDE)。来自 `TrickTypeItem.FontColor`。我们读 `Config.TrickType.Instance[x].FontColor` 即可（不用硬编码）。

#### 3. 毒素显示（仅毒器）

`PoisonsAndLevels` 结构：6种毒（烈/郁/寒/赤/腐/幻）各带 Value + Level。dump 显示带星级(Star)，Level 决定星星数。我们简化：显示毒名+数值（星级可省略，或用★符号）。

毒名映射（从 `Poison.Instance[type].Name` 读，运行时可用）。

#### 4. 命中 0% 的处理

dump 显示 0% 的命中类型有 `_DisableMaskObject`（灰色遮罩）。Markdown 里用 `~~删除线~~` 或标注"（无）"模拟。建议：0% 显示 `—` 或不显示该列。

### Markdown 输出格式设计

#### 武器示例（破脉霜）

```markdown
# 破脉霜

**超·三品 · 兵器 · 药霜药霜**

价值 27600　重量 0.4

破脉霜无色无质，一旦见光...

## 兵器属性

| 招式 | 攻击范围 | 攻击耗时 |
|---|---|---|
| 点、拿、药×4 | 近2.0远5.0 | 0.77秒 |

| 破甲 | 坚韧 | 变招积蓄 | 追击概率 |
|---|---|---|---|
| 3.95 | 3.95 | 185% | 25% |

## 攻击属性

| 破体 | 破气 |
|---|---|
| 120% | 0% |

## 命中属性

| 力道 | 精妙 | 迅疾 | 动心 |
|---|---|---|---|
| — | 125% | — | — |

## 含有毒素

| 烈毒 | 郁毒 | 寒毒 | 赤毒 | 腐毒 | 幻毒 |
|---|---|---|---|---|---|
| 40 | 40 | 40 | 40 | 40 | 40 |

**特殊词条** 药毒

> 发挥威力、破体破气等数值随角色属性实时变化，以游戏内为准。
```

#### 盔甲示例（精钢环臂）

```markdown
# 精钢环臂

**奇·六品 · 护具 · 铁制护臂**

## 护具属性

| 破刃 | 坚韧 | 外伤降低 | 内伤降低 |
|---|---|---|---|
| 8.80 | 8.80 | 20% | 0% |

## 防御属性

| 御体 | 御气 |
|---|---|
| 70% | 0% |

**特殊词条** 刚体
```

---

## 四、分阶段实施计划

### 第一阶段：武器 + 盔甲（战斗核心，最高优先级）

**目标**：重写 `RenderWeapon`、新增 `RenderArmor`，覆盖玩家最关心的战斗装备。

**任务**：
1. 提取 `LK_ItemSubType_*` 武器/盔甲子类名映射表
2. 重写 `RenderWeapon`：通用区 + 兵器属性(招式/范围/耗时/破甲/坚韧/变招/追击) + 攻击属性(破体破气) + 命中属性 + 含有毒素(条件) + 特殊词条
3. 新增 `RenderArmor`：通用区 + 护具属性(破刃/坚韧/外伤降低/内伤降低) + 防御属性(御体御气) + 特殊词条
4. 抽出 `AppendCommonArea` 共用方法
5. 在 `TipTypeConfig.EnumMap` 启用 WeaponTips 和 ArmorTips
6. 编译 + 测试

**验证**：用 dump 样本对照导出结果，数值必须一致。

### 第二阶段：衣服 + 饰品 + 工具（装备补全）

**目标**：补全剩余装备类，字段简单。

**任务**：
1. `RenderClothing`：通用区 + 衣装属性(魅力)
2. `RenderAccessory`：通用区 + 宝物属性(行囊大小等)
3. `RenderCraftTool`：通用区 + 工具效果(属性加成)
4. 启用对应 EnumMap 项

### 第三阶段：药毒 + 材料 + 书籍 + 杂物（消耗品/其他）

**目标**：覆盖剩余物品类型。

**任务**：
1. `RenderMedicine`：通用区 + 服食效果 + 战斗使用（含治疗效果富文本，较复杂）
2. `RenderMaterial`：通用区 + 功能描述
3. `RenderBook`：通用区 + 书籍内容
4. `RenderMisc`：通用区 + 特殊属性（促织罐等）

### 第四阶段：打磨

1. 颜色标记（品阶、毒、命中高低用不同色）
2. 词条跳转链接（武器→对应功法等）
3. README 更新覆盖范围说明
4. 移除/保留探针开关（发布前关闭默认值）

---

## 五、待确认问题

1. **`LK_ItemSubType_*` 完整映射**：需从 `ui_language.txt` 提取所有子类名（武器/盔甲/衣服/材料各一套枚举）
2. **`CalcAttackStartupOrRecoveryFrame` 的 `AttackSpeedFactor`**：GlobalConfig 常量，运行时可读，或硬编码（需确认值）
3. **特殊词条来源**：`EquipmentEffect` vs `UnlockEffect`，需确认 dump 里的"摧坚/刚体/药毒"分别对应哪个字段
4. **衣服/饰品字段多样性**：dump 样本少（衣服只看到魅力，饰品只看到行囊大小），不同子类可能有不同字段，需扩扫
5. **药毒治疗效果富文本**：含 `<SpName=...>` 图标标记，Markdown 里如何呈现（可用文字替代如"[外伤]")

---

## 六、探针使用说明（开发参考）

- 开关：Mod 设置 → 调试 → 卡片布局探针
- hook：`TooltipXxx.Refresh` 的 Postfix，延迟 0.3s dump（等异步数据）
- 去重：同物品 2 秒内不重复
- 日志：`Player.log` 搜 `[CardProbe]`
- 样本：`_probe/samples/<类型>__<物品名>.txt`
- 用完务必关闭开关（避免刷屏）
