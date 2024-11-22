using System;

namespace SQLInterpreter {
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

        public Column(string name, Type dataType, bool isPrimaryKey = false, bool isNullable = true, object defaultValue = null) {
            Name = name;
            DataType = dataType;
            IsPrimaryKey = isPrimaryKey;
            IsNullable = isNullable;
            DefaultValue = defaultValue;
        }

        public bool ValidateValue(object value) {
            if (value == null)
                return IsNullable;

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
                if (!IsNullable)
                    throw new InvalidOperationException($"Column {Name} cannot be null");
                return DefaultValue;
            }

            if (value.GetType() != DataType) {
                try {
                    return Convert.ChangeType(value, DataType);
                } catch (Exception ex) {
                    throw new InvalidOperationException($"Cannot convert value to {DataType.Name} for column {Name}: {ex.Message}");
                }
            }

            return value;
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

        private string GetSqlType() {
            if (DataType == typeof(int))
                return "INT";
            if (DataType == typeof(long))
                return "BIGINT";
            if (DataType == typeof(string))
                return "VARCHAR";
            if (DataType == typeof(decimal))
                return "DECIMAL";
            if (DataType == typeof(bool))
                return "BOOLEAN";
            if (DataType == typeof(DateTime))
                return "DATETIME";

            throw new NotSupportedException($"Unsupported data type: {DataType.Name}");
        }
    }
}
