using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using DatabaseStructures;
using SQLInterpreter.Core;

namespace SQLInterpreter {
    public class Evaluator : IVisitor<object> {
        private readonly Database db;

        public Evaluator(Database db) {
            this.db = db;
        }

        public object VisitSelect(SelectNode node) {
            // Clear previous table alias context
            tableAliases.Clear();

            // Set up tbale alias mapping
            string tableName;
            if (node.Table is AliasNode tableAlias) {
              tableName = ((IdentifierNode)tableAlias.Expression).Name;
              tableAliases[tableAlias.Alias] = tableName;
            } else {
              tableName = ((IdentifierNode)node.Table).Name;
            }

            var table = db.GetTable(tableName);
            var allRecords = GetAllRecords(table).ToList();  // Materialize the records

            // Apply WHERE clause if present
            if (node.WhereClause != null) {
              allRecords = allRecords.Where(r => EvaluateBoolean(node.WhereClause, r)).ToList();
            }

            // Handle aggregates
                if (node.Columns.Any(c => c is AggregateNode || (c is AliasNode a && a.Expression is AggregateNode))) {
                    var result = new Dictionary<string, object>();
                    foreach (var col in node.Columns) {
                        if (col is AggregateNode aggNode) {
                            result[GetAggregateColumnName(aggNode)] = EvaluateAggregate(aggNode, allRecords);
                        } else if (col is AliasNode aliasNode && aliasNode.Expression is AggregateNode agg) {
                            result[aliasNode.Alias] = EvaluateAggregate(agg, allRecords);
                        }
                    }
                    return new List<Dictionary<string, object>> { result };
                }

    // Handle regular columns
    return allRecords.Select(record => {
        var projection = new Dictionary<string, object>();
        foreach (var col in node.Columns) {
            if (col is AliasNode alias) {
                projection[alias.Alias] = EvaluateOperand(alias.Expression, record);
            } else if (col is TableReferenceNode tableRef) {
                projection[tableRef.ColumnName] = record[tableRef.ColumnName];
            } else if (col is IdentifierNode idNode) {
                projection[idNode.Name] = record[idNode.Name];
            }
        }
        return projection;
    }).ToList();
        }

        private Dictionary<string, object> ProcessGroup(List<ASTNode> columns, List<Dictionary<string, object>> records,
            List<IdentifierNode> groupBy = null) {
            var result = new Dictionary<string, object>();

            // Set up context for aggregate functions
            currentGroup = records;

            foreach (var col in columns) {
                if (col is AggregateNode aggNode) {
                    string colName = GetAggregateColumnName(aggNode);
                    result[colName] = Visit(aggNode);
                } else if (col is IdentifierNode idNode) {
                    if (idNode.Name == "*") {
                        foreach (var field in records.FirstOrDefault() ?? new Dictionary<string, object>()) {
                            result[field.Key] = field.Value;
                        }
                    } else {
                        result[idNode.Name] = records.FirstOrDefault()?[idNode.Name];
                    }
                }
            }

            return result;
        }

        private string GetAggregateColumnName(AggregateNode node) {
            if (node.Argument is IdentifierNode id) {
                return $"{node.Function}({id.Name})";
            }
            return node.Function.ToString();
        }

        private List<Dictionary<string, object>> currentGroup;
        private List<Dictionary<string, object>> GetCurrentGroup() => currentGroup;

        public object VisitAggregate(AggregateNode node) {
            var records = GetCurrentGroup();

            if (!records.Any()) return null;

            switch (node.Function) {
                case TokenType.COUNT:
                    if (node.Argument is IdentifierNode id && id.Name == "*") {
                        return records.Count;
                    }
                    return records.Count(r => EvaluateOperand(node.Argument, r) != null);

                case TokenType.MIN:
                    var minValues = records.Select(r => EvaluateOperand(node.Argument, r)).Where(v => v != null);
                    if (!minValues.Any()) return null;
                    if (minValues.First() is DateTime)
                        return minValues.Cast<DateTime>().Min();
                    return minValues.Cast<IComparable>().Min();

                case TokenType.MAX:
                    var maxValues = records.Select(r => EvaluateOperand(node.Argument, r)).Where(v => v != null);
                    if (!maxValues.Any()) return null;
                    if (maxValues.First() is DateTime)
                        return maxValues.Cast<DateTime>().Max();
                    return maxValues.Cast<IComparable>().Max();

                case TokenType.AVG:
                    return records.Average(r => Convert.ToDecimal(EvaluateOperand(node.Argument, r)));

                case TokenType.SUM:
                    return records.Sum(r => Convert.ToDecimal(EvaluateOperand(node.Argument, r)));

                default:
                    throw new Exception($"Unsupported aggregate function: {node.Function}");
            }
        }

        public object VisitInsert(InsertNode node) {
            var table = db.GetTable(node.Table.Name);
            var insertedRecords = new List<Dictionary<string, object>>();

            foreach (var values in node.ValuesList) {
                // Validate column count matches value count
                if (node.Columns.Count != values.Count) {
                    throw new Exception($"Expected {node.Columns.Count} values but got {values.Count}");
                }

                var record = new Dictionary<string, object>();

                // Build record
                for (int i = 0; i < node.Columns.Count; i++) {
                    var columnName = node.Columns[i].Name;
                    var val = Visit(values[i]);
                    record[columnName] = val;
                }

                // Find primary key column
                var pkColumn = table.Columns.First(c => c.IsPrimaryKey);
                var pkValue = record[pkColumn.Name].ToString();

                // Insert into B+ tree
                table.Data.Insert(pkValue, record);
                insertedRecords.Add(record);
            }

            return insertedRecords;
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
                TokenType.MOD => Modulo(left, right),
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

        public object VisitAlias(AliasNode node) {
            // for column aliases, we just handle in the column projection
            // for table aliases, we just treturn the table name
            return Visit(node.Expression);
        }

        public object VisitTableReference(TableReferenceNode node) {
          // find the actual table alias in the current context
          var table = GetTableByAlias(node.TableAlias);
          if (table == null) {
            throw new Exception($"Unknown table alias: {node.TableAlias}");
          }

          // Store the actual column name for later use
          return $"{node.TableAlias}.{node.ColumnName}";
        }

        private object Visit(ASTNode node) {
            return node.Accept(this);
        }

        private bool EvaluateBoolean(ASTNode node, Dictionary<string, object> record) {
            if (node is BinaryOpNode binaryOp) {
                // Handle aggregate function in HAVING
                if (binaryOp.Left is AggregateNode aggNode) {
                    var aggValue = Visit(aggNode);
                    var rightValue = EvaluateOperand(binaryOp.Right, record);
                    return CompareValues(aggValue, rightValue, binaryOp.Operator);
                }

                // Handle local opperators different from comparison operators
                if (binaryOp.Operator == TokenType.AND || binaryOp.Operator == TokenType.OR) {
                    bool leftResult = EvaluateBoolean(binaryOp.Left, record);

                    if (binaryOp.Operator == TokenType.AND && !leftResult) return false;
                    if (binaryOp.Operator == TokenType.OR && leftResult) return true;

                    bool rightResult = EvaluateBoolean(binaryOp.Right, record);
                    return binaryOp.Operator == TokenType.AND ? leftResult && rightResult : leftResult || rightResult;
                }

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

            int valueIndex = 0;
            int patternIndex = 0;
            int starIndex = -1;
            int tempIndex = -1;
            while (valueIndex < value.Length) {
              if (patternIndex < pattern.Length && 
                  (pattern[patternIndex] == '_' ||
                    value[valueIndex] == pattern[patternIndex])) {
                valueIndex++;
                patternIndex++;
              } else if (patternIndex < pattern.Length && pattern[patternIndex] == '%') {
                starIndex = patternIndex;
                tempIndex = valueIndex;
                patternIndex++;
              } else if (starIndex != -1) {
                patternIndex = starIndex + 1;
                valueIndex = ++tempIndex;
              } else {
                return false;
              }
            }

            while (patternIndex < pattern.Length && pattern[patternIndex] == '%') {
                patternIndex++;
            }

            return patternIndex == pattern.Length;
        }

        private object EvaluateOperand(ASTNode node, Dictionary<string, object> record) {
          switch (node) {
            case TableReferenceNode tableRef:
              if (!record.ContainsKey(tableRef.ColumnName)) {
                throw new Exception($"Column '{tableRef.ColumnName}' not found");
              }
              return record[tableRef.ColumnName];

            case IdentifierNode id:
              if (!record.ContainsKey(id.Name))
                throw new Exception($"Column '{id.Name}' not found");
              return record[id.Name];

            case LiteralNode lit:
              return lit.Value;

            case BinaryOpNode binOp:
              var left = EvaluateOperand(binOp.Left, record);
              var right = EvaluateOperand(binOp.Right, record);
              return binOp.Operator switch {
                TokenType.PLUS => Add(left, right),
                TokenType.MINUS => Subtract(left, right),
                TokenType.MULTIPLY => Multiply(left, right),
                TokenType.DIVIDE => Divide(left, right),
                TokenType.MOD => Modulo(left, right),
                _ => throw new Exception($"Unsupported operator in expression: {binOp.Operator}")
              };

            case AggregateNode aggNode:
              var records = GetCurrentGroup() ?? new List<Dictionary<string, object>>();
              return EvaluateAggregate(aggNode, records);

            default:
              throw new Exception($"Invalid operand node type: {node.GetType()}");
          }
        }

        private object EvaluateAggregate(AggregateNode node, List<Dictionary<string, object>> records) {
          if (!records.Any()) return null;

          switch (node.Function) {
            case TokenType.COUNT:
              if (node.Argument is IdentifierNode id && id.Name == "*") {
                return records.Count();
              }
              return records.Count(r => EvaluateOperand(node.Argument, r) != null);

            case TokenType.AVG:
              var values = records.Select(r => EvaluateOperand(node.Argument, r)).Where(v => v != null).Select(v => Convert.ToDecimal(v)).ToList();
              if (!values.Any()) return null;
              return values.Average();
            case TokenType.MIN:
              var minValues = records.Select(r => EvaluateOperand(node.Argument, r)).Where(v => v != null).ToList();
              if (!minValues.Any()) return null;
              return minValues.Min();
            case TokenType.MAX:
              var maxValues = records.Select(r => EvaluateOperand(node.Argument, r)).Where(v => v != null).ToList();
              if (!maxValues.Any()) return null;
              return maxValues.Max();
            case TokenType.SUM:
              var sumValues = records.Select(r => EvaluateOperand(node.Argument, r)).Where(v => v != null).Select(v => Convert.ToDecimal(v)).ToList();
              if (!sumValues.Any()) return null;
              return sumValues.Sum();

            default:
              throw new Exception($"Unsupported aggregate function: {node.Function}");
          }
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
                TokenType.DATE => typeof(DateOnly),
                _ => throw new Exception($"Unsupported type: {sqlType}")
            };
        }

        private Dictionary<string, string> tableAliases = new Dictionary<string, string>();

        private Table GetTableByAlias(string alias) {
          if (tableAliases.TryGetValue(alias, out string tableName)) {
            return db.GetTable(tableName);
          }
          throw new Exception($"Unknown table alias: {alias}");
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

        private object Modulo(object left, object right) {
            if (left == null || right == null) {
                return null;
            }

            // Convert operands to decimal
            var leftNum = Convert.ToDecimal(left);
            var rightNum = Convert.ToDecimal(right);

            // Handle division by zero
            if (rightNum == 0) {
                throw new DivideByZeroException("Cannot calculate modulo of a number by zero");
            }

            // Calculate modulo
            var result = leftNum - Math.Floor(leftNum / rightNum) * rightNum;

            // If both operands are ints, return an int
            if (left is int || left.ToString().IndexOf('.') == -1 && right is int || right.ToString().IndexOf('.') == -1) {
                return Convert.ToInt32(result);
            }

            return result;
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

        private bool CompareValues(object left, object right, TokenType op) {
            if (left == null || right == null) return false;

            // Handle numeric comparisons
            if (decimal.TryParse(left.ToString(), out decimal leftNum) &&
                decimal.TryParse(right.ToString(), out decimal rightNum)) {
                return op switch {
                    TokenType.EQUALS => leftNum == rightNum,
                    TokenType.GREATER_THAN => leftNum > rightNum,
                    TokenType.LESS_THAN => leftNum < rightNum,
                    TokenType.GREATER_EQUALS => leftNum >= rightNum,
                    TokenType.LESS_EQUALS => leftNum <= rightNum,
                    TokenType.NOT_EQUALS => leftNum != rightNum,
                    _ => throw new Exception($"Unsupported operator: {op}")
                };
            }

            // Handle datetime comparisons
            if (left is DateTime leftDate && DateTime.TryParse(right.ToString(), out DateTime rightDate)) {
                return op switch {
                    TokenType.EQUALS => leftDate == rightDate,
                    TokenType.GREATER_THAN => leftDate > rightDate,
                    TokenType.LESS_THAN => leftDate < rightDate,
                    TokenType.GREATER_EQUALS => leftDate >= rightDate,
                    TokenType.LESS_EQUALS => leftDate <= rightDate,
                    TokenType.NOT_EQUALS => leftDate != rightDate,
                    _ => throw new Exception($"Unsupported operator: {op}")
                };
            }

            // Default string comparison
            return op switch {
                TokenType.EQUALS => left.ToString() == right.ToString(),
                TokenType.NOT_EQUALS => left.ToString() != right.ToString(),
                _ => throw new Exception($"Unsupported operator for type: {op}")
            };
        }
    }
}
