namespace SQLInterpreter {
    public partial class Parser {
        private SelectNode ParseSelect() {
            Expect(TokenType.SELECT);

            // Parse columns
            var columns = new List<ASTNode>();
            if (Match(TokenType.ASTERISK)) {
                columns.Add(new IdentifierNode("*"));
            } else {
                // First column
                columns.Add(new IdentifierNode(Expect(TokenType.IDENTIFIER).Value));

                // Additional columns
                while (Match(TokenType.COMMA)) {
                    columns.Add(new IdentifierNode(Expect(TokenType.IDENTIFIER).Value));
                }
            }

            Expect(TokenType.FROM);
            var tableName = new IdentifierNode(Expect(TokenType.IDENTIFIER).Value);

            ASTNode whereClause = null;
            if (Match(TokenType.WHERE)) {
                whereClause = ParseExpression();
            }

            return new SelectNode(columns, tableName, whereClause);
        }

        private InsertNode ParseInsert() {
            Expect(TokenType.INSERT);
            Expect(TokenType.INTO);
            var tableName = new IdentifierNode(Expect(TokenType.IDENTIFIER).Value);

            // Parse column list
            Expect(TokenType.LEFT_PAREN);
            var columns = new List<IdentifierNode>();
            do {
                columns.Add(new IdentifierNode(Expect(TokenType.IDENTIFIER).Value));
            } while (Match(TokenType.COMMA));
            Expect(TokenType.RIGHT_PAREN);

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
