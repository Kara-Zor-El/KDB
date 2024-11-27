namespace SQLInterpreter {
    public class TableReferenceNode : ASTNode {
        public string TableAlias { get; }
        public string ColumnName { get; }

        public TableReferenceNode(string tableAlias, string columnName) {
            TableAlias = tableAlias;
            ColumnName = columnName;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitTableReference(this);
    }
}
