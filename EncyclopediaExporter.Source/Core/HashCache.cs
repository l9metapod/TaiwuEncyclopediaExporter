using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EncyclopediaExporter.Core
{
    /// <summary>
    /// 源文件哈希缓存。检测百科 TSV 是否变更，避免重复重建。
    /// 缓存文件为简易 JSON（无外部依赖）。
    /// </summary>
    public class HashCache
    {
        private readonly string _cachePath;
        private Dictionary<string, string> _hashes;
        private string _cachedGameVersion;
        private bool _loaded;

        public HashCache(string cachePath)
        {
            _cachePath = cachePath;
        }

        /// <summary>计算 assetsDir 下所有 .tsv 的 SHA256 字典。</summary>
        public static Dictionary<string, string> ComputeHashes(string assetsDir)
        {
            var result = new Dictionary<string, string>();
            if (!Directory.Exists(assetsDir)) return result;
            using (var sha = SHA256.Create())
            {
                foreach (var file in Directory.GetFiles(assetsDir, "*.tsv"))
                {
                    var name = Path.GetFileName(file);
                    byte[] hash;
                    using (var fs = File.OpenRead(file))
                        hash = sha.ComputeHash(fs);
                    result[name] = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            return result;
        }

        private void Load()
        {
            _hashes = new Dictionary<string, string>();
            _cachedGameVersion = "";
            if (!File.Exists(_cachePath)) { _loaded = true; return; }
            try
            {
                foreach (var line in File.ReadAllLines(_cachePath, Encoding.UTF8))
                {
                    var trimmed = line.Trim().TrimStart('"');
                    // 极简解析: "key": "value" 行
                    var idx = trimmed.IndexOf("\": \"");
                    if (idx < 0) continue;
                    var key = trimmed.Substring(0, idx);
                    var rest = trimmed.Substring(idx + 4);
                    // 去掉行尾逗号（JSON 列表分隔符）和结尾引号
                    if (rest.EndsWith(",")) rest = rest.Substring(0, rest.Length - 1);
                    if (rest.EndsWith("\"")) rest = rest.Substring(0, rest.Length - 1);
                    if (key == "__game_version") _cachedGameVersion = rest;
                    else _hashes[key] = rest;
                }
            }
            catch { /* 损坏缓存忽略 */ }
            _loaded = true;
        }

        /// <summary>源文件是否变更（哈希不同、游戏版本不同，或产物目录缺失）。</summary>
        public bool IsChanged(string assetsDir, string gameVersion, string outputDir)
        {
            if (!_loaded) Load();
            // 产物目录缺失 → 必须重建（即使哈希没变，玩家可能删了产物或换了机器）
            if (string.IsNullOrEmpty(outputDir) || !Directory.Exists(outputDir))
                return true;
            var current = ComputeHashes(assetsDir);
            // 文件数不同 → 变更
            if (current.Count != _hashes.Count) return true;
            foreach (var kv in current)
            {
                string old;
                if (!_hashes.TryGetValue(kv.Key, out old) || old != kv.Value)
                    return true;
            }
            // 游戏版本变化也视为变更（结构可能改了）
            if (!string.IsNullOrEmpty(gameVersion) && _cachedGameVersion != gameVersion)
                return true;
            return false;
        }

        /// <summary>更新缓存。</summary>
        public void Update(string assetsDir, string gameVersion)
        {
            var current = ComputeHashes(assetsDir);
            var sb = new StringBuilder();
            sb.AppendLine("{");
            sb.AppendLine("  \"__game_version\": \"" + Escape(gameVersion) + "\",");
            sb.AppendLine("  \"__updated\": \"" + DateTime.Now.ToString("yyyy-MM-dd") + "\",");
            sb.AppendLine("  \"sources\": {");
            int i = 0;
            foreach (var kv in current)
            {
                sb.Append("    \"" + Escape(kv.Key) + "\": \"" + kv.Value + "\"");
                sb.AppendLine(++i < current.Count ? "," : "");
            }
            sb.AppendLine("  }");
            sb.AppendLine("}");
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_cachePath));
                File.WriteAllText(_cachePath, sb.ToString(), new UTF8Encoding(false));
            }
            catch { /* 写缓存失败不影响主流程 */ }
        }

        private static string Escape(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
        }
    }
}
