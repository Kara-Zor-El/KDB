using System;

namespace SQLInterpreter.Core {
    public class Column {
        public string Name { get; set; }
        public Type DataType { get; set; }
        public bool IsPrimaryKey { get; set; }
        public bool IsNullable { get; set; }
        public object DefaultValue { get; set; }

        public Column() {
            IsNullable = true;
            DefaultValue = null;
        }

        public Column(string name, Type dataType, bool isPrimaryKey = false,
            bool isNullable = true, object defaultValue = null) {
            Name = name;
            DataType = dataType;
            IsPrimaryKey = isPrimaryKey;
            IsNullable = isNullable;
            DefaultValue = defaultValue;

            ValidateConfiguration();
        }

        private void ValidateConfiguration() {
            if (string.IsNullOrWhiteSpace(Name)) {
                throw new ArgumentException("Column name cannot be empty");
            }

            if (DataType == null) {
                throw new ArgumentException("Column data type cannot be null");
            }

            if (IsPrimaryKey && IsNullable) {
                throw new ArgumentException("Primary key columns cannot be nullable");
            }

            if (DefaultValue != null) {
                if (!ValidateValue(DefaultValue)) {
                    throw new ArgumentException(
                        $"Default value type {DefaultValue.GetType()} does not match column type {DataType}");
                }
            }
        }

        public bool ValidateValue(object value) {
            if (value == null) {
                return IsNullable;
            }

            if (value.GetType() != DataType) {
                try {
                    Convert.ChangeType(value, DataType);
                    return true;
                } catch {
                    return false;
                }
            }

            return true;
        }

        public object CoerceValue(object value) {
            if (value == null) {
                if (!IsNullable) {
                    throw new InvalidOperationException($"Column {Name} cannot be null");
                }
                return DefaultValue;
            }

            if (value.GetType() != DataType) {
                try {
                    return Convert.ChangeType(value, DataType);
                } catch (Exception ex) {
                    throw new InvalidOperationException(
                        $"Cannot convert value to {DataType.Name} for column {Name}: {ex.Message}");
                }
            }

            return value;
        }

        public string GetSqlType() {
            return DataType.Name switch {
                nameof(Int32) => "INT",
                nameof(Int64) => "BIGINT",
                nameof(String) => "VARCHAR",
                nameof(Decimal) => "DECIMAL",
                nameof(Boolean) => "BOOLEAN",
                nameof(DateTime) => "DATETIME",
                nameof(DateOnly) => "DATE",
                _ => throw new NotSupportedException($"Unsupported data type: {DataType.Name}")
            };
        }

        public override string ToString() {
            var result = $"{Name} {GetSqlType()}";
            if (IsPrimaryKey)
                result += " PRIMARY KEY";
            if (!IsNullable)
                result += " NOT NULL";
            if (DefaultValue != null)
                result += $" DEFAULT {DefaultValue}";
            return result;
        }
    }
}
