namespace DatabaseStructures {
    public partial class BPlusTree<TKey, TValue> where TKey : IComparable<TKey> {
        private class LeafNode : Node {
            protected internal Dictionary<TKey, TValue> Entries;
            protected internal LeafNode NextLeaf;

            public LeafNode() {
                Entries = new Dictionary<TKey, TValue>();
                NextLeaf = null;
                IsLeaf = true;
            }

            public override void InsertAt(int index, TKey key) {
                Keys.Insert(index, key);
            }

            public void InsertEntry(TKey key, TValue value) {
                Entries[key] = value;
                if (!Keys.Contains(key)) {
                    Keys.Add(key);
                    Keys.Sort();
                }
            }

            public override Node Split() {
                int midIndex = Keys.Count / 2;
                LeafNode newNode = new LeafNode();

                // Move keys and entries to new node
                for (int i = midIndex; i < Keys.Count; i++) {
                    TKey key = Keys[i];
                    newNode.InsertEntry(key, Entries[key]);
                    Entries.Remove(key);
                }
                Keys.RemoveRange(midIndex, Keys.Count - midIndex);

                // Update leaf node links
                newNode.NextLeaf = this.NextLeaf;
                this.NextLeaf = newNode;

                return newNode;
            }

            public override bool CanLendKey() {
                return Keys.Count > MinKeys;
            }

            public override void MergeWith(Node node) {
                if (node is LeafNode other) {
                    foreach (var entry in other.Entries) {
                        InsertEntry(entry.Key, entry.Value);
                    }
                    NextLeaf = other.NextLeaf;
                }
            }

            public override TKey GetFirstKey() {
                return Keys[0];
            }
        }
    }
}
