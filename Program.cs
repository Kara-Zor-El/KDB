using System;

class Program {
    static void Main(string[] args) {

        var interpreter = new SQLInterpreter.SQLInterpreter();

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

          // Select queries
          "SELECT * FROM users",
          "SELECT id, name FROM users WHERE id > 1",
          "SELECT name, email FROM users WHERE name LIKE '%Smith%'",

          // Update data
          "UPDATE users SET email = 'john.doe@example.com' WHERE id = 1",
          "SELECT * FROM users WHERE id = 1",

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

        Console.WriteLine("Enter SQL queries (type 'exit' to quit):");
        while (true) {
            Console.Write("\nSQL> ");
            string input = Console.ReadLine();

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
