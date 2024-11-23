namespace DatabaseStructures {
    public partial class BPlusTree<TKey, TValue> where TKey : IComparable<TKey> {
        protected abstract class Node {
            protected internal List<TKey> Keys;
            protected internal Node Parent;
            protected internal bool IsLeaf;
            protected internal int MinKeys;

            protected Node() {
                Keys = new List<TKey>();
                Parent = null;
                IsLeaf = false;
            }

            public abstract void InsertAt(int index, TKey key);
            public abstract Node Split();
            public abstract bool CanLendKey();
            public abstract void MergeWith(Node node);
            public abstract TKey GetFirstKey();
        }
    }
}
