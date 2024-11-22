using System;
using System.Collections.Generic;

namespace SQLInterpreter {
    public abstract class ASTNode {
        public abstract T Accept<T>(IVisitor<T> visitor);
    }

    public interface IVisitor<T> {
        T VisitSelect(SelectNode node);
        T VisitInsert(InsertNode node);
        T VisitUpdate(UpdateNode node);
        T VisitDelete(DeleteNode node);
        T VisitCreate(CreateTableNode node);
        T VisitDrop(DropTableNode node);
        T VisitBinaryOp(BinaryOpNode node);
        T VisitLiteral(LiteralNode node);
        T VisitIdentifier(IdentifierNode node);
        T VisitColumnDef(ColumnDefNode node);
    }

    // AST Nodes
    public class SelectNode : ASTNode {
        public List<ASTNode> Columns { get; }
        public IdentifierNode Table { get; }
        public ASTNode WhereClause { get; }

        public SelectNode(List<ASTNode> columns, IdentifierNode table, ASTNode whereClause) {
            Columns = columns;
            Table = table;
            WhereClause = whereClause;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitSelect(this);
    }

    public class InsertNode : ASTNode {
        public IdentifierNode Table { get; }
        public List<IdentifierNode> Columns { get; }
        public List<ASTNode> Values { get; }

        public InsertNode(IdentifierNode table, List<IdentifierNode> columns, List<ASTNode> values) {
            Table = table;
            Columns = columns;
            Values = values;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitInsert(this);
    }

    public class UpdateNode : ASTNode {
        public IdentifierNode Table { get; }
        public Dictionary<IdentifierNode, ASTNode> Assignments { get; }
        public ASTNode WhereClause { get; }

        public UpdateNode(IdentifierNode table, Dictionary<IdentifierNode, ASTNode> assignments, ASTNode whereClause) {
            Table = table;
            Assignments = assignments;
            WhereClause = whereClause;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitUpdate(this);
    }

    public class DeleteNode : ASTNode {
        public IdentifierNode Table { get; }
        public ASTNode WhereClause { get; }

        public DeleteNode(IdentifierNode table, ASTNode whereClause) {
            Table = table;
            WhereClause = whereClause;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitDelete(this);
    }

    public class CreateTableNode : ASTNode {
        public IdentifierNode TableName { get; }
        public List<ColumnDefNode> Columns { get; }

        public CreateTableNode(IdentifierNode tableName, List<ColumnDefNode> columns) {
            TableName = tableName;
            Columns = columns;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitCreate(this);
    }

    public class DropTableNode : ASTNode {
        public IdentifierNode TableName { get; }

        public DropTableNode(IdentifierNode tableName) {
            TableName = tableName;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitDrop(this);
    }

    public class BinaryOpNode : ASTNode {
        public TokenType Operator { get; }
        public ASTNode Left { get; }
        public ASTNode Right { get; }

        public BinaryOpNode(TokenType op, ASTNode left, ASTNode right) {
            Operator = op;
            Left = left;
            Right = right;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitBinaryOp(this);
    }

    public class LiteralNode : ASTNode {
        public object Value { get; }
        public TokenType Type { get; }

        public LiteralNode(object value, TokenType type) {
            Value = value;
            Type = type;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitLiteral(this);
    }

    public class IdentifierNode : ASTNode {
        public string Name { get; }

        public IdentifierNode(string name) {
            Name = name;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitIdentifier(this);
    }

    public class ColumnDefNode : ASTNode {
        public IdentifierNode Name { get; }
        public TokenType DataType { get; }
        public bool IsPrimaryKey { get; }

        public ColumnDefNode(IdentifierNode name, TokenType dataType, bool isPrimaryKey) {
            Name = name;
            DataType = dataType;
            IsPrimaryKey = isPrimaryKey;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitColumnDef(this);
    }

    public class Parser {
        private readonly List<Token> tokens;
        private int position;

        public Parser(List<Token> tokens) {
            this.tokens = tokens;
            position = 0;
        }

        private Token Current => position < tokens.Count ? tokens[position] : new Token(TokenType.EOF, "", 0, 0);
        private Token Next => position + 1 < tokens.Count ? tokens[position + 1] : new Token(TokenType.EOF, "", 0, 0);

        private void Advance() => position++;

        private bool Match(TokenType type) {
            if (Current.Type == type) {
                Advance();
                return true;
            }
            return false;
        }

        private Token Expect(TokenType type) {
            if (Current.Type != type) {
                throw new Exception($"Expected {type}, got {Current.Type} at line {Current.Line}, column {Current.Column}");
            }
            Token current = Current;
            Advance();
            return current;
        }

        public ASTNode Parse() {
            switch (Current.Type) {
                case TokenType.SELECT: return ParseSelect();
                case TokenType.INSERT: return ParseInsert();
                case TokenType.UPDATE: return ParseUpdate();
                case TokenType.DELETE: return ParseDelete();
                case TokenType.CREATE: return ParseCreate();
                case TokenType.DROP: return ParseDrop();
                default:
                    throw new Exception($"Unexpected token {Current.Type} at line {Current.Line}, column {Current.Column}");
            }
        }

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

            // Parse optional WHERE clause
            ASTNode whereClause = null;
            if (Match(TokenType.WHERE)) {
                whereClause = ParseWhereClause();
            }

            return new SelectNode(columns, tableName, whereClause);
        }

        private ASTNode ParseWhereClause() {
            var left = ParseWhereOperand();
            var op = Current;

            switch (op.Type) {
                case TokenType.EQUALS:
                case TokenType.NOT_EQUALS:
                case TokenType.LESS_THAN:
                case TokenType.LESS_EQUALS:
                case TokenType.GREATER_THAN:
                case TokenType.GREATER_EQUALS:
                case TokenType.LIKE:
                    Advance();
                    var right = ParseWhereOperand();
                    return new BinaryOpNode(op.Type, left, right);
                default:
                    throw new Exception($"Unexpected operator in WHERE clause: {op.Type}");
            }
        }

        private ASTNode ParseWhereOperand() {
            if (Current.Type == TokenType.IDENTIFIER) {
                return new IdentifierNode(Expect(TokenType.IDENTIFIER).Value);
            } else if (Current.Type == TokenType.NUMBER_LITERAL) {
                return new LiteralNode(decimal.Parse(Expect(TokenType.NUMBER_LITERAL).Value), TokenType.NUMBER_LITERAL);
            } else if (Current.Type == TokenType.STRING_LITERAL) {
                return new LiteralNode(Expect(TokenType.STRING_LITERAL).Value, TokenType.STRING_LITERAL);
            }
            throw new Exception($"Unexpected token in WHERE clause: {Current.Type}");
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
            Expect(TokenType.LEFT_PAREN);
            var values = new List<ASTNode>();

            // First value
            values.Add(ParseValue());

            // Additional values
            while (Match(TokenType.COMMA)) {
                values.Add(ParseValue());
            }

            Expect(TokenType.RIGHT_PAREN);

            return new InsertNode(tableName, columns, values);
        }

        private ASTNode ParseValue() {
            switch (Current.Type) {
                case TokenType.NUMBER_LITERAL:
                    var numToken = Expect(TokenType.NUMBER_LITERAL);
                    return new LiteralNode(decimal.Parse(numToken.Value), TokenType.NUMBER_LITERAL);

                case TokenType.STRING_LITERAL:
                    var strToken = Expect(TokenType.STRING_LITERAL);
                    return new LiteralNode(strToken.Value, TokenType.STRING_LITERAL);

                case TokenType.SINGLE_QUOTE:
                    Advance(); // consume opening quote
                    var value = Current.Value;
                    Advance(); // consume the string content
                    Expect(TokenType.SINGLE_QUOTE); // consume closing quote
                    return new LiteralNode(value, TokenType.STRING_LITERAL);

                default:
                    throw new Exception($"Unexpected token type in value: {Current.Type}");
            }
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
                bool isPrimaryKey = Match(TokenType.PRIMARY) && Match(TokenType.KEY);

                columns.Add(new ColumnDefNode(columnName, dataType, isPrimaryKey));
            } while (Match(TokenType.COMMA));

            Expect(TokenType.RIGHT_PAREN);

            return new CreateTableNode(tableName, columns);
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
                    Advance();
                    return type.Type;
                default:
                    throw new Exception($"Expected data type but found {type.Type} at line {type.Line}, column {type.Column}");
            }
        }

        private DropTableNode ParseDrop() {
            Expect(TokenType.DROP);
            Expect(TokenType.TABLE);
            return new DropTableNode(new IdentifierNode(Expect(TokenType.IDENTIFIER).Value));
        }

        private ASTNode ParseExpression() {
            return ParseLogicalOr();
        }

        private ASTNode ParseLogicalOr() {
            var left = ParseLogicalAnd();

            while (Match(TokenType.OR)) {
                var right = ParseLogicalAnd();
                left = new BinaryOpNode(TokenType.OR, left, right);
            }

            return left;
        }

        private ASTNode ParseLogicalAnd() {
            var left = ParseComparison();

            while (Match(TokenType.AND)) {
                var right = ParseComparison();
                left = new BinaryOpNode(TokenType.AND, left, right);
            }

            return left;
        }

        private ASTNode ParseComparison() {
            var left = ParseAdditive();

            while (Current.Type is TokenType.EQUALS or TokenType.NOT_EQUALS
                   or TokenType.LESS_THAN or TokenType.GREATER_THAN
                   or TokenType.LESS_EQUALS or TokenType.GREATER_EQUALS) {
                var op = Current.Type;
                Advance();
                var right = ParseAdditive();
                left = new BinaryOpNode(op, left, right);
            }

            return left;
        }

        private ASTNode ParseAdditive() {
            var left = ParseMultiplicative();

            while (Current.Type is TokenType.PLUS or TokenType.MINUS) {
                var op = Current.Type;
                Advance();
                var right = ParseMultiplicative();
                left = new BinaryOpNode(op, left, right);
            }

            return left;
        }

        private ASTNode ParseMultiplicative() {
            var left = ParsePrimary();

            while (Current.Type is TokenType.MULTIPLY or TokenType.DIVIDE) {
                var op = Current.Type;
                Advance();
                var right = ParsePrimary();
                left = new BinaryOpNode(op, left, right);
            }

            return left;
        }

        private ASTNode ParsePrimary() {
            switch (Current.Type) {
                case TokenType.NUMBER_LITERAL:
                    return new LiteralNode(decimal.Parse(Current.Value), TokenType.NUMBER_LITERAL);

                case TokenType.STRING_LITERAL:
                    return new LiteralNode(Current.Value, TokenType.STRING_LITERAL);

                case TokenType.IDENTIFIER:
                    return new IdentifierNode(Current.Value);

                case TokenType.LEFT_PAREN:
                    Advance();
                    var expr = ParseExpression();
                    Expect(TokenType.RIGHT_PAREN);
                    return expr;

                default:
                    throw new Exception($"Unexpected token {Current.Type} at line {Current.Line}, column {Current.Column}");
            }
        }
    }
}
