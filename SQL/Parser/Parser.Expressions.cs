namespace SQLInterpreter {
    public partial class Parser {
        private ASTNode ParseExpression() {
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

            if (Current.Type is TokenType.EQUALS or TokenType.NOT_EQUALS
                or TokenType.LESS_THAN or TokenType.GREATER_THAN
                or TokenType.LESS_EQUALS or TokenType.GREATER_EQUALS
                or TokenType.LIKE) {
                var op = Current.Type;
                Advance();
                var right = ParseAdditive();
                return new BinaryOpNode(op, left, right);
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

            while (Current.Type is TokenType.MULTIPLY or TokenType.DIVIDE or TokenType.MOD) {
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
                    var numValue = decimal.Parse(Current.Value);
                    Advance();
                    return new LiteralNode(numValue, TokenType.NUMBER_LITERAL);

                case TokenType.STRING_LITERAL:
                    var strValue = Current.Value;
                    Advance();
                    return new LiteralNode(strValue, TokenType.STRING_LITERAL);

                case TokenType.IDENTIFIER:
                    var idValue = Current.Value;
                    Advance();
                    return new IdentifierNode(idValue);

                case TokenType.LEFT_PAREN:
                    Advance();
                    var expr = ParseExpression();
                    Expect(TokenType.RIGHT_PAREN);
                    return expr;

                default:
                    throw new Exception($"Unexpected token {Current.Type} at line {Current.Line}, column {Current.Column}");
            }
        }

        private ASTNode ParseValue() {
            switch (Current.Type) {
                case TokenType.NUMBER_LITERAL:
                    var numToken = Expect(TokenType.NUMBER_LITERAL);
                    return new LiteralNode(decimal.Parse(numToken.Value), TokenType.NUMBER_LITERAL);

                case TokenType.STRING_LITERAL:
                    var strToken = Expect(TokenType.STRING_LITERAL);
                    return new LiteralNode(strToken.Value, TokenType.STRING_LITERAL);

                default:
                    throw new Exception($"Unexpected token type in value: {Current.Type}");
            }
        }
    }
}
