using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SQLInterpreter {
    public enum TokenType {
        // Keywords
        SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, TABLE,
        FROM, WHERE, INTO, VALUES, SET, AND, OR, NOT,
        PRIMARY, KEY, INT, VARCHAR, TEXT, DECIMAL, BOOLEAN, DATETIME,

        // Operators
        EQUALS, NOT_EQUALS, LESS_THAN, GREATER_THAN,
        LESS_EQUALS, GREATER_EQUALS,
        PLUS, MINUS, MULTIPLY, DIVIDE,

        // Symbols
        LEFT_PAREN, RIGHT_PAREN,
        COMMA, SEMICOLON, ASTERISK,
        SINGLE_QUOTE, DOUBLE_QUOTE,

        // LIKE
        LIKE,

        // Other
        IDENTIFIER, STRING_LITERAL, NUMBER_LITERAL,

        // Special
        EOF,
        INVALID
    }

    public class Token {
        public TokenType Type { get; set; }
        public string Value { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }

        public Token(TokenType type, string value, int line, int column) {
            Type = type;
            Value = value;
            Line = line;
            Column = column;
        }

        public override string ToString() => $"Token({Type}, '{Value}', line={Line}, col={Column})";
    }

    public class Lexer {
        private readonly string input;
        private int position;
        private int line;
        private int column;

        private static readonly Dictionary<string, TokenType> Keywords;

        static Lexer() {
            Keywords = new Dictionary<string, TokenType>(StringComparer.OrdinalIgnoreCase) {
        { "SELECT", TokenType.SELECT },
        { "INSERT", TokenType.INSERT },
        { "UPDATE", TokenType.UPDATE },
        { "DELETE", TokenType.DELETE },
        { "CREATE", TokenType.CREATE },
        { "DROP", TokenType.DROP },
        { "TABLE", TokenType.TABLE },
        { "FROM", TokenType.FROM },
        { "WHERE", TokenType.WHERE },
        { "INTO", TokenType.INTO },
        { "VALUES", TokenType.VALUES },
        { "SET", TokenType.SET },
        { "AND", TokenType.AND },
        { "OR", TokenType.OR },
        { "NOT", TokenType.NOT },
        { "PRIMARY", TokenType.PRIMARY },
        { "KEY", TokenType.KEY },
        { "INT", TokenType.INT },
        { "VARCHAR", TokenType.VARCHAR },
        { "TEXT", TokenType.TEXT },
        { "DECIMAL", TokenType.DECIMAL },
        { "BOOLEAN", TokenType.BOOLEAN },
        { "DATETIME", TokenType.DATETIME },
        { "LIKE", TokenType.LIKE }
      };
        }

        public Lexer(string input) {
            this.input = input;
            position = 0;
            line = 1;
            column = 1;
        }

        private char Peek(int ahead = 0) {
            int index = position + ahead;
            if (index >= input.Length) {
                return '\0';
            }
            return input[index];
        }

        private void Advance(int count = 1) {
            for (int i = 0; i < count; i++) {
                if (position < input.Length) {
                    if (input[position] == '\n') {
                        line++;
                        column = 1;
                    } else {
                        column++;
                    }
                    position++;
                }
            }
        }

        private void SkipWhitespace() {
            while (position < input.Length && char.IsWhiteSpace(input[position])) {
                Advance();
            }
        }

        private Token ReadIdentifier() {
            int startColumn = column;
            string value = "";

            while (position < input.Length &&
                (char.IsLetterOrDigit(Peek()) || Peek() == '_')) {
                value += Peek();
                Advance();
            }

            if (Keywords.TryGetValue(value, out TokenType type)) {
                return new Token(type, value, line, startColumn);
            }

            return new Token(TokenType.IDENTIFIER, value, line, startColumn);
        }

        private Token ReadNumber() {
            int startColumn = column;
            string value = "";
            bool hasDecimal = false;

            while (position < input.Length && char.IsDigit(Peek()) || Peek() == '.') {
                if (Peek() == '.') {
                    hasDecimal = true;
                }
                value += Peek();
                Advance();
            }

            return new Token(TokenType.NUMBER_LITERAL, value, line, startColumn);
        }

        private Token ReadString(char quoteChar) {
            int startColumn = column;
            Advance(); // Skip opening quote
            string value = "";

            while (position < input.Length && Peek() != quoteChar) {
                // Handle escape quotes
                if (Peek() == '\\' && Peek(1) == quoteChar) {
                    Advance(); // Skip the backslash
                }
                value += Peek();
                Advance();
            }

            if (position >= input.Length) {
                throw new Exception($"Unterminated string literal at line {line}, column {column}");
            }

            Advance(); // Skip closing quote
            return new Token(TokenType.STRING_LITERAL, value, line, startColumn);
        }

        public Token GetNextToken() {
            SkipWhitespace();

            if (position >= input.Length) {
                return new Token(TokenType.EOF, "", line, column);
            }

            char currentChar = Peek();
            int startColumn = column;

            switch (currentChar) {
                case '(':
                    Advance();
                    return new Token(TokenType.LEFT_PAREN, "(", line, startColumn);
                case ')':
                    Advance();
                    return new Token(TokenType.RIGHT_PAREN, ")", line, startColumn);
                case ',':
                    Advance();
                    return new Token(TokenType.COMMA, ",", line, startColumn);
                case ';':
                    Advance();
                    return new Token(TokenType.SEMICOLON, ";", line, startColumn);
                case '*':
                    Advance();
                    return new Token(TokenType.ASTERISK, "*", line, startColumn);
                case '+':
                    Advance();
                    return new Token(TokenType.PLUS, "+", line, startColumn);
                case '-':
                    Advance();
                    return new Token(TokenType.MINUS, "-", line, startColumn);
                case '/':
                    Advance();
                    return new Token(TokenType.DIVIDE, "/", line, startColumn);
            }

            // Handle 2 character operators
            if (position + 1 < input.Length) {
                switch (input.Substring(position, 2)) {
                    case "<=":
                        Advance(2);
                        return new Token(TokenType.LESS_EQUALS, "<=", line, startColumn);
                    case ">=":
                        Advance(2);
                        return new Token(TokenType.GREATER_EQUALS, ">=", line, startColumn);
                    case "<>":
                    case "!=":
                        Advance(2);
                        return new Token(TokenType.NOT_EQUALS, "<>", line, startColumn);
                }
            }

            // Handle single character operators
            switch (currentChar) {
                case '=':
                    Advance();
                    return new Token(TokenType.EQUALS, "=", line, startColumn);
                case '<':
                    Advance();
                    return new Token(TokenType.LESS_THAN, "<", line, startColumn);
                case '>':
                    Advance();
                    return new Token(TokenType.GREATER_THAN, ">", line, startColumn);
            }

            // Handle strings
            if (currentChar == '\'' || currentChar == '"') {
                return ReadString(currentChar);
            }

            // Handle numbers
            if (char.IsDigit(currentChar)) {
                return ReadNumber();
            }

            // Handle identifiers and keywords
            if (char.IsLetter(currentChar) || currentChar == '_') {
                return ReadIdentifier();
            }

            throw new Exception($"Unexpected character '{currentChar}' at line {line}, column {column}");
        }
    }
}
