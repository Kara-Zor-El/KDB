using System;
using System.Collections.Generic;
using SQLInterpreter.Core;

namespace SQLInterpreter {
    public partial class SQLInterpreter {
        private readonly Evaluator evaluator;
        private readonly Database database;

        public SQLInterpreter() {
            database = new Database();
            evaluator = new Evaluator(database);
        }

        public string ExecuteQuery(string sql) {
            try {
                // Tokenize
                var lexer = new Lexer(sql);
                var tokens = new List<Token>();
                Token token;
                do {
                    token = lexer.GetNextToken();
                    tokens.Add(token);
                } while (token.Type != TokenType.EOF);

                // Parse
                var parser = new Parser(tokens);
                var ast = parser.Parse();

                // Evaluate
                var result = ast.Accept(evaluator);

                // Format result
                return FormatResult(result);
            } catch (Exception e) {
                return $"Error: {e.Message}";
            }
        }

    }
}
