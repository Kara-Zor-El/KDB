using System;
using System.Collections.Generic;
using SQLInterpreter.Core;

namespace SQLInterpreter {
    public partial class SQLInterpreter {
        private readonly Evaluator evaluator;
        private readonly Database database;
        private readonly String filePath;

        public SQLInterpreter(String filePath = null) {
            this.filePath = filePath;
            database = new Database(filePath);
            evaluator = new Evaluator(database);

            if (filePath != null && File.Exists(filePath)) {
                LoadFromFile();
            }
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

                // Save changes to file if path is specified
                if (filePath != null && (tokens[0].Type == TokenType.INSERT ||
                                       tokens[0].Type == TokenType.UPDATE ||
                                       tokens[0].Type == TokenType.DELETE ||
                                       tokens[0].Type == TokenType.CREATE ||
                                       tokens[0].Type == TokenType.DROP)) {
                    SaveToFile();
                }

                // Format result
                return FormatResult(result);
            } catch (Exception e) {
                return $"Error: {e.Message}";
            }
        }
    }
}
