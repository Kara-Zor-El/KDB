namespace SQLInterpreter {
    public class SelectNode : ASTNode {
        public List<ASTNode> Columns { get; }
        public IdentifierNode Table { get; }
        public ASTNode WhereClause { get; }
        public List<IdentifierNode> GroupBy { get; }
        public ASTNode HavingClause { get; }

        public SelectNode(List<ASTNode> columns, IdentifierNode table, ASTNode whereClause, List<IdentifierNode> groupBy = null, ASTNode havingClause = null) {
            Columns = columns;
            Table = table;
            WhereClause = whereClause;
            GroupBy = groupBy ?? new List<IdentifierNode>();
            HavingClause = havingClause;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitSelect(this);
    }

    public class InsertNode : ASTNode {
        public IdentifierNode Table { get; }
        public List<IdentifierNode> Columns { get; }
        public List<List<ASTNode>> ValuesList { get; }

        public InsertNode(IdentifierNode table, List<IdentifierNode> columns, List<List<ASTNode>> valuesList) {
            Table = table;
            Columns = columns;
            ValuesList = valuesList;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitInsert(this);
    }

    public class UpdateNode : ASTNode {
        public IdentifierNode Table { get; }
        public Dictionary<IdentifierNode, ASTNode> Assignments { get; }
        public ASTNode WhereClause { get; }

        public UpdateNode(IdentifierNode table, Dictionary<IdentifierNode, ASTNode> assignments, ASTNode whereClause) {
            Table = table;
            Assignments = assignments;
            WhereClause = whereClause;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitUpdate(this);
    }

    public class DeleteNode : ASTNode {
        public IdentifierNode Table { get; }
        public ASTNode WhereClause { get; }

        public DeleteNode(IdentifierNode table, ASTNode whereClause) {
            Table = table;
            WhereClause = whereClause;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitDelete(this);
    }
}
