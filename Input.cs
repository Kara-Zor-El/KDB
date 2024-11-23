using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

public class InputHandler : IAutoCompleteHandler
{
    private readonly List<string> history = new List<string>();
    private readonly StringBuilder buffer = new StringBuilder();
    private bool isMultiLine = false;

    public char[] Separators { get; set; } = new char[] { ' ', '.', '/' };

    public string[] GetSuggestions(string text, int index)
    {
        return Array.Empty<string>();
    }

    private string ConvertToSingleLine(string multiLineInput)
    {
        // Remove new lines and excessive whitespace
        return string.Join(" ", 
            multiLineInput.Split(new[] { Environment.NewLine, "\n", "\r" }, 
            StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.Trim())
            .Where(line => !string.IsNullOrWhiteSpace(line)));
    }

    public string GetNextInput()
    {
        ReadLine.HistoryEnabled = true;
        buffer.Clear();
        isMultiLine = false;

        while (true)
        {
            string prompt = isMultiLine ? "  -> " : "SQL> ";
            
            // Set up history for ReadLine
            if (!isMultiLine)
            {
                ReadLine.GetHistory().Clear();
                foreach (var historyItem in history)
                {
                    ReadLine.AddHistory(ConvertToSingleLine(historyItem));
                }
            }

            string input = ReadLine.Read(prompt);

            // Handle exit command
            if (!isMultiLine && input?.ToLower() == "exit")
            {
                return "exit";
            }

            // Handle empty input
            if (string.IsNullOrWhiteSpace(input))
            {
                if (!isMultiLine)
                {
                    continue;
                }
                // Allow empty lines in multi-line mode
                buffer.AppendLine(input);
                continue;
            }

            // Add the current input to the buffer
            buffer.AppendLine(input);

            // Check if the entire buffer (trimmed) ends with a semicolon
            string currentBuffer = buffer.ToString().TrimEnd();
            if (currentBuffer.EndsWith(";"))
            {
                // Store the original multi-line query in history
                history.Add(currentBuffer);
                
                // Return the complete query
                return currentBuffer;
            }
            else
            {
                // Continue collecting input in multi-line mode
                isMultiLine = true;
                continue;
            }
        }
    }

    // Method to get history items in single-line format
    public IEnumerable<string> GetFormattedHistory()
    {
        return history.Select(ConvertToSingleLine);
    }
}
