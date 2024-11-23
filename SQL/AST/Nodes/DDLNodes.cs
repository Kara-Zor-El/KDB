using SQLInterpreter;

namespace SQLInterpreter {
    public class CreateTableNode : ASTNode {
        public IdentifierNode TableName { get; }
        public List<ColumnDefNode> Columns { get; }

        public CreateTableNode(IdentifierNode tableName, List<ColumnDefNode> columns) {
            TableName = tableName;
            Columns = columns;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitCreate(this);
    }

    public class DropTableNode : ASTNode {
        public IdentifierNode TableName { get; }

        public DropTableNode(IdentifierNode tableName) {
            TableName = tableName;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitDrop(this);
    }

    public class ColumnDefNode : ASTNode {
        public IdentifierNode Name { get; }
        public TokenType DataType { get; }
        public bool IsPrimaryKey { get; }

        public ColumnDefNode(IdentifierNode name, TokenType dataType, bool isPrimaryKey) {
            Name = name;
            DataType = dataType;
            IsPrimaryKey = isPrimaryKey;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitColumnDef(this);
    }
}
