using System;
using System.Collections.Generic;
using System.Text;
using SQLInterpreter;

class Program {
    static void Main(string[] args) {

        var interpreter = new SQLInterpreter.SQLInterpreter("test.db");

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
