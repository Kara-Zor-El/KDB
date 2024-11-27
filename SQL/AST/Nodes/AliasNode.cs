namespace SQLInterpreter {
    public class AliasNode : ASTNode {
      public ASTNode Expression { get; }
      public string Alias { get; }

      public AliasNode(ASTNode expression, string alias) {
        Expression = expression;
        Alias = alias;
      }

      public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitAlias(this);
    }
}
