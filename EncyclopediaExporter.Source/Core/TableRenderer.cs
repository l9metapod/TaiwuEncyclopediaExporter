using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace EncyclopediaExporter.Core
{
    /// <summary>
    /// ConfigTable 数据表 → Markdown 表格。
    /// 游戏的数据 tsv 自带表头（前两行用 &lt;td rowspan/colspan&gt; 表达合并单元格），
    /// 本渲染器从数据 tsv 自身的表头行解析列结构，而不是依赖 Reference.Desc（后者常为空占位）。
    /// </summary>
    public class TableRenderer
    {
        private readonly string _assetsDir;
        private readonly MarkdownRenderer _md;
        private readonly Dictionary<string, List<string[]>> _cache = new Dictionary<string, List<string[]>>();

        // <td rowspan="N">...|<td colspan="N">...|<td colspan="0" rowspan="0"> 占位
        private static readonly Regex TdReg =
            new Regex(@"<td\s+(?:rowspan|colspan)\s*=\s*""?\d+""?\s*(?:rowspan|colspan\s*=\s*""?\d+""?\s*)?>(.*?)</td>",
                      RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public TableRenderer(string assetsDir, MarkdownRenderer md)
        {
            _assetsDir = assetsDir;
            _md = md;
        }

        public List<string[]> GetTable(string name)
        {
            if (_cache.TryGetValue(name, out var t)) return t;
            t = DataParser.GetTable(_assetsDir, name);
            _cache[name] = t;
            return t;
        }

        /// <summary>渲染数据表为 Markdown 表格字符串。
        /// overrideTitle 非空时用它替代 refItem.Title 作为表上方的小标题（用于表格集合的 sheet 名）。</summary>
        public string Render(Models.ReferenceItem refItem, string overrideTitle = null)
        {
            var sb = new StringBuilder();
            var name = refItem.Param;
            var table = GetTable(name);
            if (table.Count == 0)
                return "*（表格 " + name + " 数据缺失）*";

            // 尝试从数据 tsv 自身的前两行解析内嵌表头（合并单元格格式）
            var parsed = ParseEmbeddedHeaders(table);

            // 表格标题：overrideTitle 优先，其次 refItem.Title
            string title = !string.IsNullOrEmpty(overrideTitle) ? overrideTitle : refItem.Title;
            if (!string.IsNullOrEmpty(title))
            {
                var t0 = title.Split('\n')[0];
                sb.AppendLine("**" + _md.ConvertBody(t0).Trim() + "**");
                sb.AppendLine();
            }

            List<string> headers;
            List<string[]> dataRows;

            if (parsed != null)
            {
                // 成功从内嵌表头解析：使用它，并跳过前两行表头
                headers = parsed;
                dataRows = new List<string[]>();
                for (int i = 2; i < table.Count; i++) dataRows.Add(table[i]);
            }
            else
            {
                // 回退：旧逻辑，表头来自 refItem.Desc（部分表可能仍是空占位）
                headers = new List<string>();
                foreach (var h in refItem.Desc)
                {
                    var col = h.Split(':')[0].Trim();
                    col = Regex.Replace(_md.ConvertBody(col), @"<[^>]*>", "").Trim();
                    headers.Add(col);
                }
                dataRows = table;
            }

            int ncols = headers.Count;
            foreach (var row in dataRows)
                if (row.Length > ncols) ncols = row.Length;
            while (headers.Count < ncols) headers.Add("");

            // MD 表格
            sb.Append("| ");
            sb.Append(string.Join(" | ", headers));
            sb.AppendLine(" |");
            sb.Append("| ");
            for (int i = 0; i < ncols; i++) sb.Append("--- | ");
            sb.AppendLine();

            foreach (var row in dataRows)
            {
                var cells = new List<string>();
                for (int i = 0; i < ncols; i++)
                {
                    string cell = i < row.Length ? _md.ConvertBody(row[i]).Trim() : "";
                    cell = cell.Replace("\n", " ");  // MD 表格不支持换行
                    cells.Add(cell);
                }
                sb.Append("| ");
                sb.Append(string.Join(" | ", cells));
                sb.AppendLine(" |");
            }
            return sb.ToString().TrimEnd();
        }

        /// <summary>
        /// 解析数据 tsv 的前两行内嵌表头（合并单元格格式）。
        /// 第 1 行可能含 &lt;td rowspan="2"&gt;名称&lt;/td&gt;（跨两行的列名，占 1 列）
        ///                  和 &lt;td colspan="N"&gt;组名&lt;/td&gt;（跨 N 列的分组，其下 N 个子列名在第 2 行）
        ///                  和 &lt;td colspan="0" rowspan="0"&gt;&lt;/td&gt;（占位填充，忽略）
        /// 第 2 行是该组下各列的子名。
        /// 若前两行不匹配此格式，返回 null（调用方回退到旧逻辑）。
        /// </summary>
        private List<string> ParseEmbeddedHeaders(List<string[]> table)
        {
            if (table.Count < 2) return null;
            var row1 = table[0];
            var row2 = table[1];

            // 前提：第 1 行至少含一个 <td rowspan/colspan>
            bool row1HasTd = false;
            foreach (var c in row1)
                if (c != null && TdReg.IsMatch(c)) { row1HasTd = true; break; }
            if (!row1HasTd) return null;

            // 第 2 行也应有 <td colspan="0" rowspan="0">（占位）或纯文本子列名
            bool row2HasTd = false;
            foreach (var c in row2)
                if (c != null && (TdReg.IsMatch(c) || c.Contains("<align"))) { row2HasTd = true; break; }
            if (!row2HasTd) return null;

            // 逐列解析：row1 的每个单元格决定它是「跨行列名」还是「分组的开始」
            // colspan 标记本单元格是某个分组的首列，其后 (colspan-1) 列在 row1 里是 colspan="0" 占位
            var headers = new List<string>();
            int j = 0; // row2 游标
            for (int i = 0; i < row1.Length; i++)
            {
                string c1 = row1[i] ?? "";
                var m = TdReg.Match(c1);
                if (!m.Success)
                {
                    // 非 <td> 格式（理论上内嵌表头行应该都是 <td>，跳过异常）
                    continue;
                }
                int colspan = ExtractSpan(c1, "colspan");
                int rowspan = ExtractSpan(c1, "rowspan");
                string text = StripTags(m.Groups[1].Value).Trim();

                if (rowspan >= 2 && colspan <= 1)
                {
                    // 跨行的列名：直接占用 row1 和 row2 的各一列
                    if (string.IsNullOrEmpty(text))
                    {
                        // 空的占位 rowspan（如 colspan="0" rowspan="0"），跳过 row2 对应列
                        j++;
                        continue;
                    }
                    headers.Add(text);
                    j++;
                }
                else if (colspan >= 2)
                {
                    // 分组：本列 + 后续 (colspan-1) 列都是该分组下的子列
                    // 子列名在 row2 的 j..j+colspan-1 位置
                    // （组名本身通常是大类如"决斗属性"，不作为列名，子列名才显示）
                    for (int k = 0; k < colspan; k++)
                    {
                        string subName = (j + k < row2.Length) ? StripTags(row2[j + k] ?? "").Trim() : "";
                        headers.Add(string.IsNullOrEmpty(subName) ? "" : subName);
                    }
                    j += colspan;
                    // 跳过 row1 中跟在 colspan 后的 (colspan-1) 个 colspan="0" 占位
                    // （它们已被上面的 colspan 吸收，但 row1 数组里仍占位，循环会自然略过
                    //  因为它们也是 <td colspan="0" rowspan="0"> 空 text → 进入 rowspan>=2 分支跳过）
                    for (int k = 1; k < colspan && i + 1 < row1.Length; k++)
                        i++; // 消耗后续占位列
                }
                else
                {
                    // colspan<=1 且 rowspan<2：普通单元格或 colspan="0" rowspan="0" 占位
                    // colspan="0" 占位不产生列名
                    j++;
                }
            }

            if (headers.Count == 0) return null;
            return headers;
        }

        /// <summary>从单元格 HTML 里提取 colspan/rowspan 的数值。</summary>
        private static int ExtractSpan(string cell, string attr)
        {
            var reg = new Regex(attr + @"\s*=\s*""?(\d+)", RegexOptions.IgnoreCase);
            var m = reg.Match(cell);
            if (!m.Success) return 1;
            return int.TryParse(m.Groups[1].Value, out int v) ? v : 1;
        }

        /// <summary>剥离所有 HTML 标签（&lt;td&gt;、&lt;align&gt;、&lt;color&gt; 等），保留纯文本。</summary>
        private static string StripTags(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return Regex.Replace(s, @"<[^>]*>", "");
        }
    }
}
