using System;
using System.IO;
using EncyclopediaExporter.Core;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace EncyclopediaExporter
{
    /// <summary>
    /// 百科导出 Mod 入口。玩家启用本 Mod 时调用 Initialize()，
    /// 自动检测百科源文件变更并重建为 Markdown。
    /// </summary>
    [PluginConfig("EncyclopediaExporter", "EncyclopediaExporter", "1.0.0")]
    public class PluginEntry : TaiwuRemakePlugin
    {
        private const string TAG = "[EncyclopediaExporter]";

        public override void Initialize()
        {
            try
            {
                Log("开始检测百科源文件...");

                // 1. 路径解析
                var assetsDir = Path.Combine(Application.streamingAssetsPath,
                    "Language_CN", "EncyclopediaAssets");
                var modRoot = GetModRootFolder();
                var modDir = Path.Combine(modRoot, "EncyclopediaExporter");
                var outputDir = Path.Combine(modDir, "output");
                // 缓存放 persistentDataPath（避免创意工坊覆盖、无需权限）
                var cachePath = Path.Combine(Application.persistentDataPath,
                    "EncyclopediaExporter.cache.json");
                var gameVersion = Application.version;

                if (!Directory.Exists(assetsDir))
                {
                    LogWarn("找不到百科数据目录: " + assetsDir);
                    return;
                }

                // 2. 哈希比对
                var hasher = new HashCache(cachePath);
                if (!hasher.IsChanged(assetsDir, gameVersion))
                {
                    Log("百科源文件未变更，跳过重建。");
                    Log("输出目录: " + outputDir);
                    return;
                }

                // 3. 重建
                Log("检测到变更，开始重建百科...");
                Directory.CreateDirectory(modDir);
                var builder = new EncyclopediaBuilder(assetsDir, outputDir);
                int count = builder.Build();

                // 4. 更新缓存
                hasher.Update(assetsDir, gameVersion);

                Log("重建完成，共 " + count + " 页。");
                Log("输出目录: " + outputDir);
            }
            catch (Exception ex)
            {
                LogError("重建失败: " + ex);
            }
        }

        public override void Dispose() { }

        /// <summary>获取 Mod 根目录（ModManager.GetModRootFolder 的等价实现）。</summary>
        private static string GetModRootFolder()
        {
            var dataParent = new DirectoryInfo(Application.dataPath).Parent;
            return Path.Combine(dataParent.FullName, "Mod");
        }

        private static void Log(string msg) => Debug.Log(TAG + " " + msg);
        private static void LogWarn(string msg) => Debug.LogWarning(TAG + " " + msg);
        private static void LogError(string msg) => Debug.LogError(TAG + " " + msg);
    }
}
