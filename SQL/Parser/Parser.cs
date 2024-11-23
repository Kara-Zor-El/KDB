using System;
using System.Collections.Generic;

namespace SQLInterpreter {
    public partial class Parser {
        private readonly List<Token> tokens;
        private int position;

        public Parser(List<Token> tokens) {
            this.tokens = tokens;
            position = 0;
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

        // Token access and utility methods
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
    }
}
