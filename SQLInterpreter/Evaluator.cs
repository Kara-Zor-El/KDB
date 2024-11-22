using System;
using System.Collections.Generic;
using System.Linq;
using ORMK2.DatabaseStructures;
using System.Text.RegularExpressions;

namespace SQLInterpreter {
    public class Database {
        private Dictionary<string, Table> tables;

        public Database() {
            tables = new Dictionary<string, Table>(StringComparer.OrdinalIgnoreCase);
        }

        public void CreateTable(string name, List<Column> columns) {
            if (tables.ContainsKey(name)) {
                throw new Exception($"Table '{name}' already exists");
            }
            tables[name] = new Table(name, columns);
        }

        public Table GetTable(string name) {
            if (!tables.TryGetValue(name, out Table table)) {
                throw new Exception($"Table '{name}' does not exist");
            }
            return table;
        }

        public void DropTable(string name) {
            if (!tables.Remove(name)) {
                throw new Exception($"Table '{name}' does not exist");
            }
        }
    }

    public class Table {
        public string Name { get; }
        public List<Column> Columns { get; }
        public BPlusTree<string, Dictionary<string, object>> Data { get; }

        public Table(string name, List<Column> columns) {
            Name = name;
            Columns = columns;
            Data = new BPlusTree<string, Dictionary<string, object>>(4);
        }
    }

    public class Evaluator : IVisitor<object> {
        private readonly Database db;

        public Evaluator(Database db) {
            this.db = db;
        }

        public object VisitSelect(SelectNode node) {
            var table = db.GetTable(node.Table.Name);
            var results = new List<Dictionary<string, object>>();

            // Get all records and filter
            foreach (var record in GetAllRecords(table)) {
                if (node.WhereClause == null || EvaluateBoolean(node.WhereClause, record)) {
                    // If selecting all columns
                    if (node.Columns.Count == 1 && node.Columns[0] is IdentifierNode id && id.Name == "*") {
                        results.Add(record);
                    } else {
                        // Select specific columns
                        var projection = new Dictionary<string, object>();
                        foreach (var col in node.Columns) {
                            if (col is IdentifierNode colId) {
                                projection[colId.Name] = record[colId.Name];
                            }
                        }
                        results.Add(projection);
                    }
                }
            }

            return results;
        }

        public object VisitInsert(InsertNode node) {
            var table = db.GetTable(node.Table.Name);
            var record = new Dictionary<string, object>();

            // Validate column count matches value count
            if (node.Columns.Count != node.Values.Count) {
                throw new Exception($"Expected {node.Columns.Count} values but got {node.Values.Count}");
            }

            // Build record
            for (int i = 0; i < node.Columns.Count; i++) {
                var columnName = node.Columns[i].Name;
                var val = Visit(node.Values[i]);
                record[columnName] = val;
            }

            // Find primary key column
            var pkColumn = table.Columns.First(c => c.IsPrimaryKey);
            var pkValue = record[pkColumn.Name].ToString();

            // Insert into B+ tree
            table.Data.Insert(pkValue, record);
            return record;
        }

        public object VisitUpdate(UpdateNode node) {
            var table = db.GetTable(node.Table.Name);
            int updatedCount = 0;

            foreach (var record in GetAllRecords(table).ToList()) {
                if (node.WhereClause == null || EvaluateBoolean(node.WhereClause, record)) {
                    foreach (var assignment in node.Assignments) {
                      var column = table.Columns.FirstOrDefault(c => c.Name == assignment.Key.Name);
                      if (column == null) throw new Exception($"Column '{assignment.Key.Name}' does not exist");
                      var value = Visit(assignment.Value);
                      record[column.Name] = column.CoerceValue(value);
                    }

                    // Find primary key and value
                    var pkColumn = table.Columns.First(c => c.IsPrimaryKey);
                    var pkValue = record[pkColumn.Name].ToString();

                    // Update record in B+ tree
                    table.Data.Insert(pkValue, record);
                    updatedCount++;
                }
            }
            return updatedCount;
        }

        public object VisitDelete(DeleteNode node) {
            var table = db.GetTable(node.Table.Name);
            int deletedCount = 0;

            // Find primary key column
            var pkColumn = table.Columns.First(c => c.IsPrimaryKey);

            foreach (var record in GetAllRecords(table).ToList()) {
                if (node.WhereClause == null || EvaluateBoolean(node.WhereClause, record)) {
                    var pkValue = record[pkColumn.Name]?.ToString();
                    table.Data.Remove(pkValue);
                    deletedCount++;
                }
            }
            return deletedCount;
        }

        public object VisitCreate(CreateTableNode node) {
            var columns = new List<Column>();
            foreach (var colDef in node.Columns) {
                columns.Add(new Column {
                    Name = colDef.Name.Name,
                    DataType = GetSystemType(colDef.DataType),
                    IsPrimaryKey = colDef.IsPrimaryKey
                });
            }

            db.CreateTable(node.TableName.Name, columns);
            return null;
        }

        public object VisitDrop(DropTableNode node) {
            db.DropTable(node.TableName.Name);
            return null;
        }

        public object VisitBinaryOp(BinaryOpNode node) {
            var left = Visit(node.Left);
            var right = Visit(node.Right);

            return node.Operator switch {
                TokenType.PLUS => Add(left, right),
                TokenType.MINUS => Subtract(left, right),
                TokenType.MULTIPLY => Multiply(left, right),
                TokenType.DIVIDE => Divide(left, right),
                TokenType.EQUALS => Equals(left, right),
                TokenType.NOT_EQUALS => !Convert.ToBoolean(Equals(left, right)),
                TokenType.LESS_THAN => Compare(left, right) < 0,
                TokenType.LESS_EQUALS => Compare(left, right) <= 0,
                TokenType.GREATER_THAN => Compare(left, right) > 0,
                TokenType.GREATER_EQUALS => Compare(left, right) >= 0,
                TokenType.AND => Convert.ToBoolean(left) && Convert.ToBoolean(right),
                TokenType.OR => Convert.ToBoolean(left) || Convert.ToBoolean(right),
                _ => throw new Exception($"Unsupported operator: {node.Operator}")
            };
        }

        public object VisitLiteral(LiteralNode node) {
            return node.Value;
        }

        public object VisitIdentifier(IdentifierNode node) {
            return node.Name;
        }

        public object VisitColumnDef(ColumnDefNode node) {
            throw new NotImplementedException();
        }

        private object Visit(ASTNode node) {
            return node.Accept(this);
        }

        private bool EvaluateBoolean(ASTNode node, Dictionary<string, object> record) {
            if (node is BinaryOpNode binaryOp) {
                var left = EvaluateOperand(binaryOp.Left, record);
                var right = EvaluateOperand(binaryOp.Right, record);

                return binaryOp.Operator switch {
                    TokenType.EQUALS => Compare(left, right) == 0,
                    TokenType.NOT_EQUALS => Compare(left, right) != 0,
                    TokenType.LESS_THAN => Compare(left, right) < 0,
                    TokenType.LESS_EQUALS => Compare(left, right) <= 0,
                    TokenType.GREATER_THAN => Compare(left, right) > 0,
                    TokenType.GREATER_EQUALS => Compare(left, right) >= 0,
                    TokenType.LIKE => EvaluateLike(left?.ToString(), right?.ToString()),
                    TokenType.AND => Convert.ToBoolean(left) && Convert.ToBoolean(right),
                    TokenType.OR => Convert.ToBoolean(left) || Convert.ToBoolean(right),
                    _ => throw new Exception($"Unsupported operator in WHERE clause: {binaryOp.Operator}")
                };
            }

            if (node is IdentifierNode idNode) {
              return record[idNode.Name] != null;
            }

            throw new Exception($"Invalid WHERE clause node type: {node.GetType()}");
        }

        private bool EvaluateLike(string value, string pattern) {
            if (value == null || pattern == null) return false;

            pattern = Regex.Escape(pattern)
              .Replace("%", ".*")
              .Replace("_", ".");

            return Regex.IsMatch(value, $"^{pattern}$", RegexOptions.IgnoreCase);
        }

        private object EvaluateOperand(ASTNode node, Dictionary<string, object> record) {
            if (node is IdentifierNode id) {
                if (!record.ContainsKey(id.Name))
                    throw new Exception($"Column '{id.Name}' not found");
                return record[id.Name];
            }

            if (node is LiteralNode lit) {
                return lit.Value;
            }

            throw new Exception($"Invalid operand node type: {node.GetType()}");
        }

        private IEnumerable<Dictionary<string, object>> GetAllRecords(Table table) {
            return table.Data.Range("\0", "￿").Select(kvp => kvp.Value);
        }

        private Type GetSystemType(TokenType sqlType) {
            return sqlType switch {
                TokenType.INT => typeof(int),
                TokenType.VARCHAR => typeof(string),
                TokenType.TEXT => typeof(string),
                TokenType.DECIMAL => typeof(decimal),
                TokenType.BOOLEAN => typeof(bool),
                TokenType.DATETIME => typeof(DateTime),
                _ => throw new Exception($"Unsupported type: {sqlType}")
            };
        }

        private object Add(object left, object right) {
            if (left is decimal dl && right is decimal dr) {
                return dl + dr;
            }
            if (left is string || right is string) {
                return Convert.ToString(left) + Convert.ToString(right);
            }
            return Convert.ToDecimal(left) + Convert.ToDecimal(right);
        }

        private object Subtract(object left, object right) {
            return Convert.ToDecimal(left) - Convert.ToDecimal(right);
        }

        private object Multiply(object left, object right) {
            return Convert.ToDecimal(left) * Convert.ToDecimal(right);
        }

        private object Divide(object left, object right) {
            var divisor = Convert.ToDecimal(right);
            if (divisor == 0) {
                throw new DivideByZeroException();
            }
            return Convert.ToDecimal(left) / divisor;
        }

        private object Equals(object left, object right) {
            if (left == null && right == null) {
                return true;
            }
            if (left == null || right == null) {
                return false;
            }

            return left.ToString().Equals(right.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private int Compare(object left, object right) {
            if (left == null && right == null) return 0;
            if (left == null) return -1;
            if (right == null) return 1;

            // If both are strings, use string comparison
            if (left is string leftStr && right is string rightStr) {
                return string.Compare(leftStr, rightStr, StringComparison.OrdinalIgnoreCase);
            }

            // Try numeric comparison
            if (decimal.TryParse(left.ToString(), out decimal leftNum) && decimal.TryParse(right.ToString(), out decimal rightNum)) {
                return leftNum.CompareTo(rightNum);
            }

            // Fallback to string comparison
            return string.Compare(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
        }
    }
}
