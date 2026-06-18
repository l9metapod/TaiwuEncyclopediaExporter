using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;

namespace EncyclopediaExporter.Core
{
    /// <summary>
    /// 卡片布局探针（开发用）。
    /// 用 Harmony Postfix 挂到各类 TooltipXxx.Refresh，在游戏渲染完卡片后
    /// 递归 dump transform 树（active 节点 + 文本 + 尺寸位置），输出到 Player.log。
    ///
    /// 用法：mod 设置面板开「卡片探针」→ 游戏内鼠标悬停任意物品 → 日志搜 [CardProbe]。
    /// 关掉开关则卸载 patch。
    ///
    /// 目的：确认百晓册/背包等环境下，各类卡片实际激活了哪些字段节点，
    /// 避免对着反编译代码猜"显示/不显示"。已用于核对武器卡片字段。
    /// </summary>
    internal static class CardProbe
    {
        private static Harmony _harmony;
        private static bool _enabled;

        /// <summary>所有要 dump 的物品卡片类（继承 TooltipItemBase，均 override Refresh）。
        /// 命名空间统一 Game.Views.MouseTips.Item。</summary>
        private static readonly string[] CardTypes =
        {
            "Game.Views.MouseTips.Item.TooltipWeapon",     // 武器
            "Game.Views.MouseTips.Item.TooltipArmor",      // 盔甲
            "Game.Views.MouseTips.Item.TooltipClothing",   // 衣服
            "Game.Views.MouseTips.Item.TooltipAccessory",  // 饰品/宝物
            "Game.Views.MouseTips.Item.TooltipCarrier",    // 阅历/书籍
            "Game.Views.MouseTips.Item.TooltipBook",       // 书
            "Game.Views.MouseTips.Item.TooltipMaterial",   // 材料
            "Game.Views.MouseTips.Item.TooltipMedicine",   // 药毒
            "Game.Views.MouseTips.Item.TooltipFood",       // 食物
            "Game.Views.MouseTips.Item.TooltipTeaWine",    // 茶酒
            "Game.Views.MouseTips.Item.TooltipCraftTool",  // 制造工具
            "Game.Views.MouseTips.Item.TooltipMisc",       // 杂物
            "Game.Views.MouseTips.Item.TooltipJiao",       // 蛟
            "Game.Views.MouseTips.Item.TooltipLegendaryBook", // 奇书
            // 非物品类卡片（不同基类，单独处理）
            "MouseTipCombatSkill",                          // 功法（旧版路径，可能仍在用）
            "Game.Views.MouseTips.Item.TooltipDisorderOfQi", // 内息紊乱
        };

        public static bool IsEnabled => _enabled;

        /// <summary>启用探针：对每个卡片类 patch Refresh。</summary>
        public static void Enable()
        {
            if (_enabled) return;
            try
            {
                _harmony = new Harmony("EncyclopediaExporter.CardProbe");
                int ok = 0, fail = 0;
                var patched = new List<MethodInfo>();
                foreach (var fullName in CardTypes)
                {
                    var t = AccessTools.TypeByName(fullName);
                    if (t == null)
                    {
                        Log("跳过(类型不存在): " + fullName);
                        fail++;
                        continue;
                    }
                    var refresh = AccessTools.Method(t, "Refresh");
                    if (refresh == null)
                    {
                        Log("跳过(无 Refresh 方法): " + fullName);
                        fail++;
                        continue;
                    }
                    var postfix = typeof(Patches).GetMethod(nameof(Patches.Any_Refresh_Postfix));
                    _harmony.Patch(refresh, postfix: new HarmonyMethod(postfix));
                    ok++;
                }
                _enabled = true;
                Log($"探针已启用：成功 patch {ok} 个卡片类，{fail} 个跳过。");
                Log("游戏内悬停任意物品即可 dump 卡片布局。");

                // 一次性 dump Colors 颜色表（用于补全 DataParser 的语义颜色映射）
                DumpColorsTable();
            }
            catch (Exception ex)
            {
                LogErr("启用探针失败: " + ex);
            }
        }

        /// <summary>禁用探针：卸载 patch。</summary>
        public static void Disable()
        {
            if (!_enabled) return;
            try
            {
                _harmony?.UnpatchSelf();
                _enabled = false;
                if (_runner != null)
                {
                    UnityEngine.Object.Destroy(_runner.gameObject);
                    _runner = null;
                }
                _lastName = "";
                Log("探针已禁用。");
            }
            catch (Exception ex)
            {
                LogErr("禁用探针失败: " + ex);
            }
        }

        public static void SetEnabled(bool on)
        {
            if (on) Enable(); else Disable();
        }

        private static void Log(string msg) => Debug.Log("[CardProbe] " + msg);
        private static void LogErr(string msg) => Debug.LogError("[CardProbe] " + msg);

        // ============ Harmony Patches ============
        private static class Patches
        {
            /// <summary>所有卡片 Refresh 的 Postfix。
            /// 不立即 dump——异步数据(OnGetWeaponPrepareFrame 等)在 Refresh 返回后才到达。
            /// 用协程等待几帧让内容就绪，再 dump。同一物品 2 秒内去重。</summary>
            public static void Any_Refresh_Postfix(Component __instance)
            {
                try
                {
                    ScheduleDump(__instance);
                }
                catch (Exception ex)
                {
                    LogErr("调度 dump 失败: " + ex);
                }
            }
        }

        // ============ 延迟 Dump（等待异步数据就绪）============
        private static ProbeRunner _runner;
        private static float _lastDumpTime = -10f;
        private static string _lastName = "";

        /// <summary>确保有 MonoBehaviour 宿主跑协程。</summary>
        private static ProbeRunner EnsureRunner()
        {
            if (_runner != null) return _runner;
            var go = new GameObject("[EncyclopediaExporter.CardProbe]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<ProbeRunner>();
            return _runner;
        }

        private static void ScheduleDump(Component ctx)
        {
            // 去重：同名物品 2 秒内不重复 dump（鼠标移动会反复触发 Refresh）
            string key = ctx.GetInstanceID() + ":" + ctx.GetType().Name;
            float now = Time.realtimeSinceStartup;
            if (key == _lastName && now - _lastDumpTime < 2f) return;
            _lastName = key;
            _lastDumpTime = now;

            var runner = EnsureRunner();
            runner.StartCoroutine(DumpAfterDelay(ctx, 0.3f));
        }

        private static System.Collections.IEnumerator DumpAfterDelay(Component ctx, float delay)
        {
            // 等待 delay 秒（约 18 帧），让异步回调填充内容
            yield return new WaitForSeconds(delay);
            // 再等 2 帧确保 LayoutRebuilder 完成
            yield return null;
            yield return null;
            try
            {
                DumpCard(ctx.transform, ctx);
            }
            catch (Exception ex)
            {
                LogErr("dump 失败: " + ex);
            }
        }

        /// <summary>跑协程用的 MonoBehaviour。</summary>
        private sealed class ProbeRunner : MonoBehaviour { }

        // ============ Dump 逻辑 ============
        /// <summary>一次性 dump Colors.Instance 的完整颜色表（PresetColorNames + PresetColors）。
        /// 用于补全 DataParser.KnownColors 的语义颜色映射，解决 <color=#pinkyellow> 等漏词问题。</summary>
        private static void DumpColorsTable()
        {
            try
            {
                var colorsType = AccessTools.TypeByName("Colors");
                if (colorsType == null) { LogErr("DumpColors: 找不到 Colors 类型"); return; }
                var instanceProp = colorsType.GetProperty("Instance");
                object instance = instanceProp?.GetValue(null, null);
                if (instance == null) { LogErr("DumpColors: Colors.Instance 为 null（游戏未加载）"); return; }

                var names = Traverse.Create(instance).Field("PresetColorNames").GetValue<List<string>>();
                var cols = Traverse.Create(instance).Field("PresetColors").GetValue<List<UnityEngine.Color>>();
                if (names == null) { LogErr("DumpColors: PresetColorNames 为 null"); return; }

                var sb = new StringBuilder();
                sb.AppendLine("=================== [CardProbe] 颜色表 dump ===================");
                sb.AppendLine($"// 共 {names.Count} 项。格式: \"别名\" => \"#hex\"");
                sb.AppendLine("// 用于补全 DataParser.KnownColors，对齐游戏 Colors.Instance 语义颜色");
                sb.AppendLine("private static readonly Dictionary<string, string> KnownColors =");
                sb.AppendLine("    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)");
                sb.AppendLine("    {");
                for (int i = 0; i < names.Count; i++)
                {
                    string name = names[i];
                    string hex = "#FFFFFF";
                    if (cols != null && i < cols.Count)
                    {
                        var c = cols[i];
                        hex = "#" + UnityEngine.ColorUtility.ToHtmlStringRGBA(c);
                    }
                    sb.AppendLine($"        {{ \"{name}\", \"{hex}\" }},");
                }
                sb.AppendLine("    };");
                sb.AppendLine(new string('=', 80));
                Debug.Log("[CardProbe]\n" + sb.ToString());
                Log($"颜色表已 dump：{names.Count} 项。日志搜 [CardProbe] 颜色表 dump。");
            }
            catch (Exception ex)
            {
                LogErr("DumpColors 失败: " + ex);
            }
        }

        private static void DumpCard(Transform root, Component ctx)
        {
            var sb = new StringBuilder();
            sb.AppendLine("=================== [CardProbe] 卡片 dump ===================");

            // 顶部元信息：卡片类型 + 环境（模板/带角色）
            string typeName = ctx.GetType().Name;
            sb.AppendLine($"[类型] {typeName}");
            try
            {
                var data = Traverse.Create(ctx).Field("_itemData").GetValue();
                var tplOnly = Traverse.Create(ctx).Field("_templateDataOnly").GetValue<bool>();
                var charId = Traverse.Create(ctx).Field("_charId").GetValue<int>();
                sb.AppendLine($"[环境] TemplateDataOnly={tplOnly}  CharId={charId}  ItemData={(data == null ? "null" : data.GetType().Name)}");
            }
            catch
            {
                // 非物品卡片（如功法）没有这些字段，忽略
            }

            sb.AppendLine("[active 节点树] 路径 | 文本 | 尺寸 wxh | 位置 xy");
            sb.AppendLine(new string('-', 80));
            DumpTransform(root, "", sb, 0);
            sb.AppendLine(new string('=', 80));

            Debug.Log("[CardProbe]\n" + sb.ToString());
        }

        /// <summary>递归输出 transform：只保留 activeSelf 的节点。
        /// 加深度上限避免某些卡片层级过深刷屏。</summary>
        private static void DumpTransform(Transform t, string indent, StringBuilder sb, int depth)
        {
            if (t == null || depth > 12) return;
            if (!t.gameObject.activeSelf) return; // 卡片靠 active 控制字段显隐

            string name = t.gameObject.name;
            var rt = t as RectTransform;

            string text = "";
            var tmp = t.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
            {
                text = tmp.text;
                if (!string.IsNullOrEmpty(text)) text = "\"" + text.Replace("\n", "\\n").Trim() + "\"";
                else text = "(空)";
            }

            string geom = "";
            if (rt != null)
            {
                var sd = rt.sizeDelta;
                var ap = rt.anchoredPosition;
                geom = $"[{sd.x:F0}x{sd.y:F0} @{ap.x:F0},{ap.y:F0}]";
            }

            sb.AppendLine($"{indent}{name}  {text}  {geom}");

            for (int i = 0; i < t.childCount; i++)
            {
                DumpTransform(t.GetChild(i), indent + "  ", sb, depth + 1);
            }
        }
    }
}
