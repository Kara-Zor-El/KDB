using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Globalization;

namespace SQLInterpreter {
    public partial class SQLInterpreter {
        private void LoadFromFile() {
            try {
                using (var fs = new FileStream(filePath, FileMode.Open))
                using (var reader = new BinaryReader(fs, Encoding.UTF8, true)) {
                    // Read number of tables
                    int tableCount = reader.ReadInt32();

                    for (int i = 0; i < tableCount; i++) {
                        string tableName = reader.ReadString();
                        int columnCount = reader.ReadInt32();

                        var columns = new List<Core.Column>();

                        // Read column definitions
                        for (int j = 0; j < columnCount; j++) {
                            string columnName = reader.ReadString();
                            string typeStr = reader.ReadString();
                            bool isPrimaryKey = reader.ReadBoolean();
                            bool isNullable = reader.ReadBoolean();

                            Type dataType = Type.GetType(typeStr) ?? typeof(string);

                            // Primary key columns should never be nullable
                            if (isPrimaryKey) {
                                isNullable = false;
                            }

                            // Create the column with proper nullability settings
                            var column = new Core.Column(
                                name: columnName,
                                dataType: dataType,
                                isPrimaryKey: isPrimaryKey,
                                isNullable: isNullable
                            );

                            columns.Add(column);
                        }

                        // Validate columns before creating table
                        ValidateColumns(columns);

                        // Create table
                        database.CreateTable(tableName, columns);
                        var table = database.GetTable(tableName);

                        // Read number of records
                        int recordCount = reader.ReadInt32();

                        // Read records
                        for (int j = 0; j < recordCount; j++) {
                            var record = new Dictionary<string, object>();
                            foreach (var column in columns) {
                                object value = ReadValue(reader, column.DataType);
                                record[column.Name] = value;
                            }

                            // Find primary key
                            var pkColumn = columns.Find(c => c.IsPrimaryKey);
                            if (pkColumn == null) {
                                throw new Exception($"No primary key found for table {tableName}");
                            }

                            string pkValue = record[pkColumn.Name]?.ToString() ??
                                throw new Exception($"Primary key value cannot be null for table {tableName}");

                            // Insert into table
                            table.Data.Insert(pkValue, record);
                        }
                    }
                }
            } catch (Exception ex) {
                throw new Exception($"Error loading database file: {ex.Message}");
            }
        }

        private object ReadValue(BinaryReader reader, Type type) {
            // Check for null value
            if (reader.ReadBoolean()) {
                return null;
            }

            if (type == typeof(int)) {
                return reader.ReadInt32();
            } else if (type == typeof(string)) {
                return reader.ReadString();
            } else if (type == typeof(decimal)) {
                return reader.ReadDecimal();
            } else if (type == typeof(bool)) {
                return reader.ReadBoolean();
            } else if (type == typeof(DateTime)) {
                return DateTime.FromBinary(reader.ReadInt64());
            } else if (type == typeof(DateOnly)) {
                return DateOnly.FromDayNumber(reader.ReadInt32());
            }

            throw new NotSupportedException($"Unsupported type: {type}");
        }

        private bool IsCompatibleType(object value, Type targetType) {
            if (value == null) return true;

            try {
                // Special handling for numeric types
                if (IsNumericType(targetType)) {
                    if (value is decimal || value is double || value is float ||
                        value is int || value is long || value is short ||
                        value is byte || value is uint || value is ulong ||
                        value is ushort || value is sbyte) {
                        // Try converting to the target numeric type
                        Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture);
                        return true;
                    }
                    if (value is string strVal) {
                        // Try parsing string to the target numeric type
                        return TryParseNumeric(strVal, targetType);
                    }
                }

                // Handle DateTime
                if (targetType == typeof(DateTime) && value is string strDate) {
                    return DateTime.TryParse(strDate, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _);
                }

                // Handle DateOnly
                if (targetType == typeof(DateOnly) && value is string strDateOnly) {
                    return DateOnly.TryParse(strDateOnly, CultureInfo.InvariantCulture,
                        DateTimeStyles.None, out _);
                }

                // Default type compatibility check
                return value.GetType() == targetType ||
                       targetType.IsAssignableFrom(value.GetType()) ||
                       Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture) != null;
            } catch {
                return false;
            }
        }

        private bool IsNumericType(Type type) {
            return type == typeof(int) || type == typeof(long) || type == typeof(short) ||
                   type == typeof(decimal) || type == typeof(double) || type == typeof(float) ||
                   type == typeof(byte) || type == typeof(sbyte) || type == typeof(uint) ||
                   type == typeof(ulong) || type == typeof(ushort);
        }

        private bool TryParseNumeric(string value, Type targetType) {
            try {
                if (targetType == typeof(int)) return int.TryParse(value, out _);
                if (targetType == typeof(decimal)) return decimal.TryParse(value, out _);
                if (targetType == typeof(double)) return double.TryParse(value, out _);
                if (targetType == typeof(float)) return float.TryParse(value, out _);
                if (targetType == typeof(long)) return long.TryParse(value, out _);
                if (targetType == typeof(short)) return short.TryParse(value, out _);
                return false;
            } catch {
                return false;
            }
        }

        private void WriteValue(BinaryWriter writer, object value, Type type) {
            // Write null indicator
            writer.Write(value == null);
            if (value == null) return;

            try {
                if (IsNumericType(type)) {
                    // Convert the value to the correct numeric type before writing
                    var convertedValue = Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
                    if (type == typeof(int)) {
                        writer.Write((int)convertedValue);
                    } else if (type == typeof(decimal)) {
                        writer.Write((decimal)convertedValue);
                    } else if (type == typeof(double)) {
                        writer.Write((double)convertedValue);
                    }
                    // Add other numeric types as needed
                } else if (type == typeof(string)) {
                    writer.Write(value.ToString());
                } else if (type == typeof(bool)) {
                    writer.Write(Convert.ToBoolean(value));
                } else if (type == typeof(DateTime)) {
                    DateTime dateTime;
                    if (value is string dateStr) {
                        if (!DateTime.TryParse(dateStr, CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out dateTime)) {
                            throw new FormatException($"Invalid datetime format: {dateStr}");
                        }
                    } else if (value is DateTime dt) {
                        dateTime = dt;
                    } else {
                        throw new ArgumentException($"Cannot convert {value} to DateTime");
                    }
                    writer.Write(dateTime.ToBinary());
                } else if (type == typeof(DateOnly)) {
                    DateOnly date;
                    if (value is string dateStr) {
                        if (!DateOnly.TryParse(dateStr, CultureInfo.InvariantCulture,
                            DateTimeStyles.None, out date)) {
                            throw new FormatException($"Invalid date format: {dateStr}");
                        }
                    } else if (value is DateOnly d) {
                        date = d;
                    } else {
                        throw new ArgumentException($"Cannot convert {value} to DateOnly");
                    }
                    writer.Write(date.DayNumber);
                } else {
                    throw new NotSupportedException($"Unsupported type: {type}");
                }
            } catch (Exception ex) {
                throw new Exception($"Error converting value '{value}' to type {type.Name}: {ex.Message}");
            }
        }

        private void ValidateColumns(List<Core.Column> columns) {
            // Check for primary key
            var primaryKeys = columns.FindAll(c => c.IsPrimaryKey);
            if (primaryKeys.Count == 0) {
                throw new Exception("Table must have a primary key");
            }
            if (primaryKeys.Count > 1) {
                throw new Exception("Table cannot have multiple primary keys");
            }

            // Check for duplicate column names
            var columnNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var column in columns) {
                if (!columnNames.Add(column.Name)) {
                    throw new Exception($"Duplicate column name: {column.Name}");
                }
            }

            // Validate each column
            foreach (var column in columns) {
                if (column.IsPrimaryKey && column.IsNullable) {
                    throw new Exception("Primary key columns cannot be nullable");
                }
            }
        }

        public void SaveToFile() {
            if (filePath == null) return;

            try {
                using (var fs = new FileStream(filePath, FileMode.Create))
                using (var writer = new BinaryWriter(fs, Encoding.UTF8, true)) {
                    var tables = database.GetTableNames();
                    writer.Write(tables.Count());

                    foreach (var tableName in tables) {
                        var table = database.GetTable(tableName);

                        // Write table metadata
                        writer.Write(tableName);
                        writer.Write(table.Columns.Count);

                        // Write column definitions
                        foreach (var column in table.Columns) {
                            writer.Write(column.Name);
                            writer.Write(column.DataType.AssemblyQualifiedName ?? "");
                            writer.Write(column.IsPrimaryKey);
                            writer.Write(column.IsNullable);
                        }

                        // Validate records before saving
                        var records = table.Data.Range("\0", "￿").ToList();
                        foreach (var record in records) {
                            ValidateRecord(table, record.Value);
                        }

                        // Write records count
                        writer.Write(records.Count);

                        // Write records
                        foreach (var kvp in records) {
                            var record = kvp.Value;
                            foreach (var column in table.Columns) {
                                WriteValue(writer, record[column.Name], column.DataType);
                            }
                        }
                    }
                }
            } catch (Exception ex) {
                throw new Exception($"Error saving database file: {ex.Message}");
            }
        }

        private void ValidateRecord(Core.Table table, Dictionary<string, object> record) {
            foreach (var column in table.Columns) {
                if (!record.ContainsKey(column.Name)) {
                    throw new Exception($"Missing value for column {column.Name}");
                }

                var value = record[column.Name];
                if (value == null) {
                    if (!column.IsNullable) {
                        throw new Exception($"NULL value not allowed for non-nullable column {column.Name}");
                    }
                    continue;
                }

                // Validate type compatibility
                if (!IsCompatibleType(value, column.DataType)) {
                    throw new Exception($"Invalid type for column {column.Name}: expected {column.DataType.Name}, got {value.GetType().Name}");
                }
            }
        }
    }
}
