using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SQLInterpreter {
    public partial class SQLInterpreter {
        private string FormatResult(object result) {
            if (result == null) {
                return "Query executed successfully";
            }

            if (result is int count) {
                return $"Query executed successfully. {count} rows affected";
            }

            if (result is IEnumerable<Dictionary<string, object>> rows) {
                return FormatTable(rows);
            }

            if (result is Dictionary<string, object> row) {
                return FormatTable(new[] { row });
            }

            return result.ToString();
        }

        private string FormatTable(IEnumerable<Dictionary<string, object>> rows) {
            if (!rows.Any())
                return "No rows returned.";

            var columns = rows.First().Keys.ToList();
            var sb = new StringBuilder();

            // Calculate column widths
            var columnWidths = new Dictionary<string, int>();
            foreach (var column in columns) {
                int maxWidth = column.Length;
                foreach (var row in rows) {
                    int valueWidth = row[column]?.ToString()?.Length ?? 4; // "null".Length
                    maxWidth = Math.Max(maxWidth, valueWidth);
                }
                columnWidths[column] = maxWidth;
            }

            // Build header
            BuildTableLine(sb, columnWidths);
            sb.AppendLine();
            foreach (var column in columns) {
                sb.Append("| ").Append(column.PadRight(columnWidths[column])).Append(' ');
            }
            sb.Append('|');
            sb.AppendLine();
            BuildTableLine(sb, columnWidths);
            sb.AppendLine();

            // Build rows
            foreach (var row in rows) {
                foreach (var column in columns) {
                    var value = row[column]?.ToString() ?? "null";
                    sb.Append("| ").Append(value.PadRight(columnWidths[column])).Append(' ');
                }
                sb.Append('|');
                sb.AppendLine();
            }

            BuildTableLine(sb, columnWidths);
            return sb.ToString();
        }


        private void BuildTableLine(StringBuilder sb, Dictionary<string, int> columnWidths) {
            foreach (var width in columnWidths.Values) {
                sb.Append('+').Append(new string('-', width + 2));
            }
            sb.Append('+');
        }
    }
}
