// CsvExport.cs
using System.Data;
using System.Text;

namespace DhcpWmiViewer
{
    internal static class CsvExport
    {
        /// <summary>Schreibt die Tabelle (mit Kopfzeile) als UTF-8-CSV.</summary>
        public static void Write(DataTable dt, string filePath)
        {
            var sb = new StringBuilder();
            sb.AppendLine(string.Join(",", dt.Columns.Cast<DataColumn>().Select(c => Escape(c.ColumnName))));
            foreach (DataRow row in dt.Rows)
                sb.AppendLine(string.Join(",", row.ItemArray.Select(v => Escape(v?.ToString() ?? string.Empty))));

            File.WriteAllText(filePath, sb.ToString(), Encoding.UTF8);
        }

        private static string Escape(string s)
            => s.IndexOfAny(new[] { '"', ',', '\n', '\r' }) >= 0 ? "\"" + s.Replace("\"", "\"\"") + "\"" : s;
    }
}
