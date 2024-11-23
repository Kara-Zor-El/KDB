namespace SQLInterpreter {
    public enum TokenType {
        // Keywords
        SELECT, INSERT, UPDATE, DELETE, CREATE, DROP, TABLE,
        FROM, WHERE, INTO, VALUES, SET, AND, OR, NOT,
        PRIMARY, KEY, INT, VARCHAR, TEXT, DECIMAL, BOOLEAN, DATETIME, DATE,

        // Operators
        EQUALS, NOT_EQUALS, LESS_THAN, GREATER_THAN,
        LESS_EQUALS, GREATER_EQUALS,
        PLUS, MINUS, MULTIPLY, DIVIDE,
        MOD,

        // Aggregate functions
        COUNT, SUM, AVG, MIN, MAX,

        // Grouping
        GROUP, BY, HAVING,

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
}
