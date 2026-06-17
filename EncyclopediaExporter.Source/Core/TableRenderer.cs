using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace EncyclopediaExporter.Core
{
    /// <summary>
    /// ConfigTable 数据表 → Markdown 表格。
    /// 表头来自 Reference.Desc，数据来自 <表名>.tsv。
    /// </summary>
    public class TableRenderer
    {
        private readonly string _assetsDir;
        private readonly MarkdownRenderer _md;
        private readonly Dictionary<string, List<string[]>> _cache = new Dictionary<string, List<string[]>>();

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

        /// <summary>渲染数据表为 Markdown 表格字符串。</summary>
        public string Render(Models.ReferenceItem refItem)
        {
            var sb = new StringBuilder();
            var name = refItem.Param;
            var table = GetTable(name);
            if (table.Count == 0)
                return "*（表格 " + name + " 数据缺失）*";

            // 表头: Desc 每项取冒号前（来自反编译 TableElement.ParsingCfgHeader）
            var headers = new List<string>();
            foreach (var h in refItem.Desc)
            {
                var col = h.Split(':')[0].Trim();
                col = Regex.Replace(_md.ConvertBody(col), @"<[^>]*>", "").Trim();
                headers.Add(col);
            }

            int ncols = headers.Count;
            foreach (var row in table)
                if (row.Length > ncols) ncols = row.Length;
            while (headers.Count < ncols) headers.Add("");

            // 表格标题
            if (!string.IsNullOrEmpty(refItem.Title))
            {
                var title = refItem.Title.Split('\n')[0];
                sb.AppendLine("**" + _md.ConvertBody(title).Trim() + "**");
                sb.AppendLine();
            }

            // MD 表格
            sb.Append("| ");
            sb.Append(string.Join(" | ", headers));
            sb.AppendLine(" |");
            sb.Append("| ");
            for (int i = 0; i < ncols; i++) sb.Append("--- | ");
            sb.AppendLine();

            foreach (var row in table)
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
    }
}
