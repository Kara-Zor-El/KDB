using System;
using System.Collections.Generic;

namespace SQLInterpreter.Core {
    public class Database {
        private Dictionary<string, Table> tables;
        private readonly string filePath;

        public Database(string filePath = null) {
            tables = new Dictionary<string, Table>(StringComparer.OrdinalIgnoreCase);
            this.filePath = filePath;
        }

        public void CreateTable(string name, List<Column> columns) {
            if (tables.ContainsKey(name)) {
                throw new Exception($"Table '{name}' already exists");
            }
            tables[name] = new Table(name, columns);
        }

        public Table GetTable(string name) {
            if (!tables.TryGetValue(name, out Table table)) {
                throw new Exception($"Table '{name}' does not exist");
            }
            return table;
        }

        public void DropTable(string name) {
            if (!tables.Remove(name)) {
                throw new Exception($"Table '{name}' does not exist");
            }
        }

        public bool TableExists(string name) {
            return tables.ContainsKey(name);
        }

        public IEnumerable<string> GetTableNames() {
            return tables.Keys;
        }
    }
}
