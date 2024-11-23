namespace SQLInterpreter {
    public class AggregateNode : ASTNode {
        public TokenType Function { get; }
        public ASTNode Argument { get; }

        public AggregateNode(TokenType function, ASTNode argument) {
            Function = function;
            Argument = argument;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitAggregate(this);
    }
}
