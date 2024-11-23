using System;
using System.Collections.Generic;
using System.Text;

class Program {
    static void Main(string[] args) {

        var interpreter = new SQLInterpreter.SQLInterpreter();

        /*
        // Example queries
        string[] queries = {
          // Create a table
          @"CREATE TABLE users (
              id INT PRIMARY KEY,
              name VARCHAR,
              email VARCHAR,
              created_at DATETIME
          )",

          // Insert some data
          "INSERT INTO users (id, name, email, created_at) VALUES (1, 'John Doe', 'john@example.com', '2024-01-01')",
          "INSERT INTO users (id, name, email, created_at) VALUES (2, 'Jane Smith', 'jane@example.com', '2024-01-02')",
          "INSERT INTO users (id, name, email, created_at) VALUES (3, 'Bob Wilson', 'bob@example.com', '2024-01-03')",
          "INSERT INTO users (id, name, email, created_at) VALUES (4, 'Alice Brown', 'alice@example.com', '2024-01-04')",
          "INSERT INTO users (id, name, email, created_at) VALUES (5, 'Charlie Smith', 'charlie@example.com', '2024-01-05')",
          "INSERT INTO users (id, name, email, created_at) VALUES (6, 'Charlie Smith', 'charlie2@example.com', '2024-01-05')",

          // Select queries
          "SELECT * FROM users",
          "SELECT id, name FROM users WHERE id > 1",
          "SELECT name, email FROM users WHERE name LIKE '%Smith%'",
          "SELECT name, email FROM users WHERE name = 'Charlie Smith'",
          "SELECT name, email FROM users WHERE name = 'Charlie Smith' AND email LIKE '%2%'",

          // Update data
          "UPDATE users SET email = 'john.doe@example.com' WHERE id = 1",
          "SELECT * FROM users WHERE id = 1",
          "SELECT * FROM users",

          // Delete data
          "DELETE FROM users WHERE id = 2",
          "SELECT * FROM users"
      };

        Console.WriteLine("SQL Interpreter Demo\n");

        foreach (var query in queries) {
            Console.WriteLine($"Executing: {query}");
            Console.WriteLine("\nResult:");
            Console.WriteLine(interpreter.ExecuteQuery(query));
            Console.WriteLine("\n" + new string('-', 50) + "\n");
        }
        */

        var inputHandler = new InputHandler();

        Console.WriteLine("Enter SQL queries (type 'exit' to quit):");
        while (true) {
            string input = inputHandler.GetNextInput();

            if (string.IsNullOrWhiteSpace(input)) {
                continue;
            }

            if (input.ToLower() == "exit") {
                break;
            }

            Console.WriteLine("\nResult:");
            Console.WriteLine(interpreter.ExecuteQuery(input));

        }
    }
}
