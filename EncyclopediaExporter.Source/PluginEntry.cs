using System;
using System.IO;
using EncyclopediaExporter.Core;
using EncyclopediaExporter.Models;
using TaiwuModdingLib.Core.Plugin;
using UnityEngine;

namespace EncyclopediaExporter
{
    /// <summary>
    /// 百科导出 Mod 入口。
    /// 启用时读取百科源数据 + 运行时配置，重建为 Markdown 文档。
    /// </summary>
    [PluginConfig("EncyclopediaExporter", "EncyclopediaExporter", "1.0.0")]
    public class PluginEntry : TaiwuRemakePlugin
    {
        private string _assetsDir;
        private string _outputDir;
        private string _cachePath;
        private string _gameVersion;
        private string _modDir;

        public override void Initialize()
        {
            ResolvePaths();
            if (!Directory.Exists(_assetsDir))
            {
                LogWarn("找不到百科数据目录: " + _assetsDir);
                return;
            }
            DoExport(force: false);
        }

        /// <summary>
        /// 响应玩家在 mod 设置面板改设置后点「应用」。
        /// 检查「强制重新导出」开关：若打开则立即重建，完成后关掉开关。
        /// </summary>
        public override void OnModSettingUpdate()
        {
            ResolvePaths();
            bool force = false;
            if (ModManager.GetSetting(ModIdStr, "ForceReExport", ref force) && force)
            {
                Log("检测到「强制重新导出」，开始重建...");
                DoExport(force: true);
                // 关掉开关，避免下次误触发
                TrySetSetting("ForceReExport", false);
            }
        }

        public override void Dispose() { }

        /// <summary>解析所有路径（供 Initialize/OnModSettingUpdate 复用）。</summary>
        private void ResolvePaths()
        {
            _assetsDir = Path.Combine(Application.streamingAssetsPath,
                "Language_CN", "EncyclopediaAssets");
            var gameRoot = new DirectoryInfo(Application.dataPath).Parent.FullName;
            var modRoot = ModManager.GetModRootFolder();
            _modDir = Path.Combine(modRoot, "EncyclopediaExporter");
            // 输出到游戏根目录下（独立于 mod 目录），避免上传创意工坊时把生成的文档一起打包。
            _outputDir = Path.Combine(gameRoot, "EncyclopediaExporter_Output");
            _cachePath = Path.Combine(Application.persistentDataPath,
                "EncyclopediaExporter.cache.json");
            _gameVersion = Application.version;
        }

        /// <summary>
        /// 执行重建。force=true 时跳过哈希缓存检查，无条件重建。
        /// </summary>
        private void DoExport(bool force)
        {
            try
            {
                var hasher = new HashCache(_cachePath);
                if (!force)
                {
                    if (!hasher.IsChanged(_assetsDir, _gameVersion, _outputDir))
                    {
                        Log("百科源文件未变更，跳过重建。");
                        Log("输出目录: " + _outputDir);
                        return;
                    }
                    Log("检测到变更，开始重建百科...");
                }
                else
                {
                    Log("强制重建百科...");
                }

                Directory.CreateDirectory(_outputDir);
                var builder = new EncyclopediaBuilder(_assetsDir, _outputDir);
                int count = builder.Build();

                hasher.Update(_assetsDir, _gameVersion);

                Log("重建完成，共 " + count + " 项。");
                Log("输出目录: " + _outputDir);
            }
            catch (Exception ex)
            {
                LogError("重建失败: " + ex);
            }
        }

        private void TrySetSetting(string key, bool value)
        {
            try
            {
                var modInfo = ModManager.GetModInfo(ModIdStr);
                if (modInfo == null) return;
                var entries = modInfo.ModSettingEntries;
                if (entries == null) return;
                foreach (var e in entries)
                {
                    if (e is FrameWork.ModSystem.ToggleSetting ts && ts.Key == key)
                    {
                        ts.Value = value;
                        break;
                    }
                }
            }
            catch { /* 写不回不影响重建结果，开关下次启动会重置 */ }
        }

        private void Log(string msg) => Debug.Log("[EncyclopediaExporter] " + msg);
        private void LogWarn(string msg) => Debug.LogWarning("[EncyclopediaExporter] " + msg);
        private void LogError(string msg) => Debug.LogError("[EncyclopediaExporter] " + msg);
    }
}
