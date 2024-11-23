using SQLInterpreter;

namespace SQLInterpreter {
    public class BinaryOpNode : ASTNode {
        public TokenType Operator { get; }
        public ASTNode Left { get; }
        public ASTNode Right { get; }

        public BinaryOpNode(TokenType op, ASTNode left, ASTNode right) {
            Operator = op;
            Left = left;
            Right = right;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitBinaryOp(this);
    }

    public class LiteralNode : ASTNode {
        public object Value { get; }
        public TokenType Type { get; }

        public LiteralNode(object value, TokenType type) {
            Value = value;
            Type = type;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitLiteral(this);
    }

    public class IdentifierNode : ASTNode {
        public string Name { get; }

        public IdentifierNode(string name) {
            Name = name;
        }

        public override T Accept<T>(IVisitor<T> visitor) => visitor.VisitIdentifier(this);
    }
}
