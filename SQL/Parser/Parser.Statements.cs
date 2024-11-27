namespace SQLInterpreter {
    public partial class Parser {
        private SelectNode ParseSelect() {
            Expect(TokenType.SELECT);

            // Parse columns
            var columns = new List<ASTNode>();
            do {
                ASTNode column;
                if (Current.Type is TokenType.COUNT or TokenType.MIN or TokenType.MAX or TokenType.AVG or TokenType.SUM) {
                  column = ParsePrimary();

                  // Handle optional alias for aggregate
                  if (Match(TokenType.AS)) {
                    string alias = Expect(TokenType.IDENTIFIER).Value;
                    column = new AliasNode(column, alias);
                  }
                } else if (Match(TokenType.ASTERISK)) {
                  column = new IdentifierNode("*");
                } else {
                  column = ParseExpression();

                  // Handle optional alias
                  if (Match(TokenType.AS)) {
                    string alias = Expect(TokenType.IDENTIFIER).Value;
                    column = new AliasNode(column, alias);
                  }
                }
                columns.Add(column);
            } while (Match(TokenType.COMMA));

            Expect(TokenType.FROM);

            // Parse table name
            var tableName = new IdentifierNode(Expect(TokenType.IDENTIFIER).Value);

            // Handle table alias
            ASTNode tableRef = tableName;
            if (Current.Type == TokenType.IDENTIFIER || Current.Type == TokenType.AS) {
              if (Match(TokenType.AS)) {
                string alias = Expect(TokenType.IDENTIFIER).Value;
                tableRef = new AliasNode(tableName, alias);
              } else if (Current.Type == TokenType.IDENTIFIER) {
                string alias = Current.Value;
                Advance();  // consume the alias identifier
                tableRef = new AliasNode(tableName, alias);
              }
            }


            // Parse optional clauses in correct order: WHERE -> GROUP BY -> HAVING
            ASTNode whereClause = null;
            List<IdentifierNode> groupBy = null;
            ASTNode havingClause = null;

            // First check for WHERE
            if (Match(TokenType.WHERE)) {
                whereClause = ParseExpression();
            }

            // Then GROUP BY
            if (Match(TokenType.GROUP)) {
                Expect(TokenType.BY);
                groupBy = new List<IdentifierNode>();
                do {
                    groupBy.Add(new IdentifierNode(Expect(TokenType.IDENTIFIER).Value));
                } while (Match(TokenType.COMMA));
            }

            // Finally HAVING (only valid after GROUP BY)
            if (Match(TokenType.HAVING)) {
                if (groupBy == null) {
                    throw new Exception("HAVING clause requires GROUP BY");
                }
                havingClause = ParseExpression();
            }

            // Throw error if WHERE appears after GROUP BY
            if (Current.Type == TokenType.WHERE) {
                throw new Exception("WHERE clause must come before GROUP BY");
            }

            // Validate end of statement
            if (Current.Type != TokenType.SEMICOLON && Current.Type != TokenType.EOF) {
                throw new Exception($"Unexpected token '{Current.Value}' at line {Current.Line}, column {Current.Column}. Expected end of statement.");
            }

            return new SelectNode(columns, tableName, whereClause, groupBy, havingClause);
        }

        private InsertNode ParseInsert() {
            Expect(TokenType.INSERT);
            Expect(TokenType.INTO);
            var tableName = new IdentifierNode(Expect(TokenType.IDENTIFIER).Value);
            var columns = new List<IdentifierNode>();

            // Parse optional column list
            var hasColumnList = Match(TokenType.LEFT_PAREN);
            if (hasColumnList) {
              do {
                  columns.Add(new IdentifierNode(Expect(TokenType.IDENTIFIER).Value));
              } while (Match(TokenType.COMMA));
              Expect(TokenType.RIGHT_PAREN);
            } else {
              // get all the columns from the table
              var table = db.GetTable(tableName.Name);
              columns = table.Columns.Select(c => new IdentifierNode(c.Name)).ToList();
            }

            Expect(TokenType.VALUES);
            var valuesList = new List<List<ASTNode>>();

            do {
                Expect(TokenType.LEFT_PAREN);
                var values = new List<ASTNode>();
                // First value
                values.Add(ParseValue());

                // Additional values
                while (Match(TokenType.COMMA)) {
                    values.Add(ParseValue());
                }

                Expect(TokenType.RIGHT_PAREN);
                valuesList.Add(values);
            } while (Match(TokenType.COMMA));

            return new InsertNode(tableName, columns, valuesList);
        }

        private UpdateNode ParseUpdate() {
            Expect(TokenType.UPDATE);
            var tableName = new IdentifierNode(Expect(TokenType.IDENTIFIER).Value);
            Expect(TokenType.SET);

            var assignments = new Dictionary<IdentifierNode, ASTNode>();
            do {
                var column = new IdentifierNode(Expect(TokenType.IDENTIFIER).Value);
                Expect(TokenType.EQUALS);
                var value = ParseExpression();
                assignments.Add(column, value);
            } while (Match(TokenType.COMMA));

            ASTNode whereClause = null;
            if (Match(TokenType.WHERE)) {
                whereClause = ParseExpression();
            }

            return new UpdateNode(tableName, assignments, whereClause);
        }

        private DeleteNode ParseDelete() {
            Expect(TokenType.DELETE);
            Expect(TokenType.FROM);
            var tableName = new IdentifierNode(Expect(TokenType.IDENTIFIER).Value);

            ASTNode whereClause = null;
            if (Match(TokenType.WHERE)) {
                whereClause = ParseExpression();
            }

            return new DeleteNode(tableName, whereClause);
        }

        private CreateTableNode ParseCreate() {
            Expect(TokenType.CREATE);
            Expect(TokenType.TABLE);
            var tableName = new IdentifierNode(Expect(TokenType.IDENTIFIER).Value);

            Expect(TokenType.LEFT_PAREN);
            var columns = new List<ColumnDefNode>();

            do {
                var columnName = new IdentifierNode(Expect(TokenType.IDENTIFIER).Value);
                var dataType = ParseDataType();
                bool isPrimaryKey = false;

                if (Match(TokenType.PRIMARY)) {
                    Expect(TokenType.KEY);
                    isPrimaryKey = true;
                }

                columns.Add(new ColumnDefNode(columnName, dataType, isPrimaryKey));
            } while (Match(TokenType.COMMA));

            Expect(TokenType.RIGHT_PAREN);

            return new CreateTableNode(tableName, columns);
        }

        private DropTableNode ParseDrop() {
            Expect(TokenType.DROP);
            Expect(TokenType.TABLE);
            return new DropTableNode(new IdentifierNode(Expect(TokenType.IDENTIFIER).Value));
        }

        private ASTNode ParseColumnWithOptionalAlias() {
            ASTNode expr = ParseExpression();

            if (Match(TokenType.AS)) {
              string alias = Expect(TokenType.IDENTIFIER).Value;
              return new AliasNode(expr, alias);
            } else if (Match(TokenType.IDENTIFIER)) {
              // handle implicis alias (without AS keyword)
              string alias = Expect(TokenType.IDENTIFIER).Value;
              return new AliasNode(expr, alias);
            }

            return expr;
        }

        private ASTNode ParseTableWithOptionalAlias() {
          var tableName = new IdentifierNode(Expect(TokenType.IDENTIFIER).Value);

          if (Match(TokenType.AS)) {
            string alias = Expect(TokenType.IDENTIFIER).Value;
            return new AliasNode(tableName, alias);
          } else if (Match(TokenType.IDENTIFIER)) {
            // handle implicis alias (without AS keyword)
            string alias = Expect(TokenType.IDENTIFIER).Value;
            return new AliasNode(tableName, alias);
          }

          return tableName;
        }

        private TokenType ParseDataType() {
            Token type = Current;
            switch (type.Type) {
                case TokenType.INT:
                case TokenType.VARCHAR:
                case TokenType.TEXT:
                case TokenType.DECIMAL:
                case TokenType.BOOLEAN:
                case TokenType.DATETIME:
                case TokenType.DATE:
                    Advance();
                    return type.Type;
                default:
                    throw new Exception($"Expected data type but found {type.Type} at line {type.Line}, column {type.Column}");
            }
        }
    }
}
