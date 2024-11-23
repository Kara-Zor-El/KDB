using System;
using System.Collections.Generic;
using System.Linq;
using DatabaseStructures;

namespace SQLInterpreter.Core {
    public class Table {
        public string Name { get; }
        public List<Column> Columns { get; }
        public BPlusTree<string, Dictionary<string, object>> Data { get; }

        public Table(string name, List<Column> columns) {
            Name = name;
            Columns = columns;
            Data = new BPlusTree<string, Dictionary<string, object>>(4);

            ValidateColumns();
        }

        private void ValidateColumns() {
            // Ensure there is exactly one primary key
            var primaryKeys = Columns.Count(c => c.IsPrimaryKey);
            if (primaryKeys != 1) {
                throw new ArgumentException($"Table must have exactly one primary key column. Found {primaryKeys}.");
            }

            // Ensure column names are unique
            var duplicateNames = Columns
                .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();

            if (duplicateNames.Any()) {
                throw new ArgumentException(
                    $"Duplicate column names found: {string.Join(", ", duplicateNames)}");
            }
        }

        public Column GetColumn(string name) {
            var column = Columns.FirstOrDefault(c =>
                string.Equals(c.Name, name, StringComparison.OrdinalIgnoreCase));

            if (column == null) {
                throw new ArgumentException($"Column '{name}' does not exist in table '{Name}'");
            }

            return column;
        }

        public Column GetPrimaryKeyColumn() {
            return Columns.First(c => c.IsPrimaryKey);
        }

        public void ValidateRecord(Dictionary<string, object> record) {
            foreach (var column in Columns) {
                if (!record.ContainsKey(column.Name)) {
                    if (!column.IsNullable && column.DefaultValue == null) {
                        throw new ArgumentException(
                            $"Missing value for non-nullable column '{column.Name}' in table '{Name}'");
                    }
                    continue;
                }

                var value = record[column.Name];
                if (!column.ValidateValue(value)) {
                    throw new ArgumentException(
                        $"Invalid value for column '{column.Name}' in table '{Name}'. " +
                        $"Expected type: {column.DataType.Name}");
                }
            }
        }

        public Dictionary<string, object> PrepareRecord(Dictionary<string, object> record) {
            var prepared = new Dictionary<string, object>(
                StringComparer.OrdinalIgnoreCase);

            foreach (var column in Columns) {
                if (record.TryGetValue(column.Name, out var value)) {
                    prepared[column.Name] = column.CoerceValue(value);
                } else {
                    prepared[column.Name] = column.DefaultValue;
                }
            }

            return prepared;
        }

        public override string ToString() {
            return $"Table {Name} ({string.Join(", ", Columns.Select(c => c.ToString()))})";
        }
    }
}
