namespace DatabaseStructures {
    public partial class BPlusTree<TKey, TValue> where TKey : IComparable<TKey> {
        private class InternalNode : Node {
            protected internal List<Node> Children;

            public InternalNode() {
                Children = new List<Node>();
                IsLeaf = false;
            }

            public override void InsertAt(int index, TKey key) {
                Keys.Insert(index, key);
            }

            public void InsertChildAt(int index, Node child) {
                Children.Insert(index, child);
                child.Parent = this;
            }

            public override Node Split() {
                int midIndex = Keys.Count / 2;
                InternalNode newNode = new InternalNode();

                // Move keys to new node
                newNode.Keys.AddRange(Keys.GetRange(midIndex + 1, Keys.Count - midIndex - 1));
                Keys.RemoveRange(midIndex + 1, Keys.Count - midIndex - 1);

                // Move children to new node
                newNode.Children.AddRange(Children.GetRange(midIndex + 1, Children.Count - midIndex - 1));
                Children.RemoveRange(midIndex + 1, Children.Count - midIndex - 1);

                // Update children's parent references
                foreach (Node child in newNode.Children) {
                    child.Parent = newNode;
                }

                return newNode;
            }

            public override bool CanLendKey() {
                return Keys.Count > MinKeys;
            }

            public override void MergeWith(Node node) {
                if (node is InternalNode other) {
                    Keys.AddRange(other.Keys);
                    Children.AddRange(other.Children);

                    foreach (Node child in other.Children) {
                        child.Parent = this;
                    }
                }
            }

            public override TKey GetFirstKey() {
                return Keys[0];
            }
        }
    }
}
