namespace SQLInterpreter {
    public abstract class ASTNode {
        public abstract T Accept<T>(IVisitor<T> visitor);
    }

    public interface IVisitor<T> {
        T VisitSelect(SelectNode node);
        T VisitInsert(InsertNode node);
        T VisitUpdate(UpdateNode node);
        T VisitDelete(DeleteNode node);
        T VisitCreate(CreateTableNode node);
        T VisitDrop(DropTableNode node);
        T VisitBinaryOp(BinaryOpNode node);
        T VisitLiteral(LiteralNode node);
        T VisitIdentifier(IdentifierNode node);
        T VisitColumnDef(ColumnDefNode node);
        T VisitAggregate(AggregateNode node);
        T VisitAlias(AliasNode node);
        T VisitTableReference(TableReferenceNode node);
    }
}
