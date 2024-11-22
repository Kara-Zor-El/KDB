using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DatabaseStructures {
    public class BPlusTree<TKey, TValue> where TKey : IComparable<TKey> {
        private Node Root;
        private LeafNode FirstLeaf;
        private readonly int Order;
        private readonly int MinKeys;
        private readonly bool PersistToDisk;
        private readonly string DataFilePath;

        public BPlusTree(int order, bool persistToDisk = true, string dataFilePath = "bplustree.dat") {
            if (order < 3) {
                throw new ArgumentException("Order must be at least 3", nameof(order));
            }
            Order = order;
            MinKeys = (order + 1) / 2 - 1;
            PersistToDisk = persistToDisk;
            DataFilePath = dataFilePath;
            Root = new LeafNode();
            FirstLeaf = (LeafNode)Root;
        }

        private abstract class Node {
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

        public void Insert(TKey key, TValue value) {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            LeafNode leaf = FindLeafNode(key);
            leaf.InsertEntry(key, value);

            if (leaf.Keys.Count >= Order) {
                SplitNode(leaf);
            }

            if (PersistToDisk) {
                PersistToDiskWriter();
            }
        }

        public TValue Get(TKey key) {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            LeafNode leaf = FindLeafNode(key);
            if (leaf.Entries.TryGetValue(key, out TValue value)) {
                return value;
            }
            throw new KeyNotFoundException($"Key {key} not found in B+ tree");
        }

        public void Remove(TKey key) {
            LeafNode leaf = FindLeafNode(key);
            if (!leaf.Entries.ContainsKey(key)) {
                throw new KeyNotFoundException($"Key {key} not found in B+ tree");
            }

            leaf.Entries.Remove(key);
            leaf.Keys.Remove(key);

            if (leaf != Root && leaf.Keys.Count < MinKeys) {
                HandleUnderflow(leaf);
            }

            if (Root.Keys.Count == 0 && !Root.IsLeaf) {
                Root = ((InternalNode)Root).Children[0];
                Root.Parent = null;
            }

            if (PersistToDisk) {
                PersistToDiskWriter();
            }
        }

        public IEnumerable<KeyValuePair<TKey, TValue>> Range(TKey start, TKey end) {
            LeafNode current = FindLeafNode(start);
            while (current != null) {
                foreach (TKey key in current.Keys) {
                    if (key.CompareTo(start) >= 0 && key.CompareTo(end) <= 0) {
                        yield return new KeyValuePair<TKey, TValue>(key, current.Entries[key]);
                    }
                    if (key.CompareTo(end) > 0) {
                        yield break;
                    }
                }
                current = current.NextLeaf;
            }
        }

        private LeafNode FindLeafNode(TKey key) {
            Node current = Root;
            while (!current.IsLeaf) {
                InternalNode internalNode = (InternalNode)current;
                int index = FindInsertIndex(internalNode.Keys, key);
                // Ensure index is within bounds
                if (index >= internalNode.Children.Count)
                    index = internalNode.Children.Count - 1;
                current = internalNode.Children[index];
            }
            return (LeafNode)current;
        }

        private void SplitNode(Node node) {
            Node newNode = node.Split();
            TKey promoteKey = newNode.GetFirstKey();

            if (node == Root) {
                InternalNode newRoot = new InternalNode();
                newRoot.Keys.Add(promoteKey);
                newRoot.Children.Add(node);
                newRoot.Children.Add(newNode);
                Root = newRoot;
                node.Parent = newRoot;
                newNode.Parent = newRoot;
            } else {
                InternalNode parent = (InternalNode)node.Parent;
                int insertIndex = FindInsertIndex(parent.Keys, promoteKey);
                parent.Keys.Insert(insertIndex, promoteKey);
                parent.Children.Insert(insertIndex + 1, newNode);
                newNode.Parent = parent;

                if (parent.Keys.Count >= Order) {
                    SplitNode(parent);
                }
            }
        }

        private void HandleUnderflow(Node node) {
            InternalNode parent = (InternalNode)node.Parent;
            int nodeIndex = parent.Children.IndexOf(node);

            // Try borrowing from left sibling
            if (nodeIndex > 0) {
                Node leftSibling = parent.Children[nodeIndex - 1];
                if (leftSibling.CanLendKey()) {
                    BorrowFromLeft(node, leftSibling, parent, nodeIndex);
                    return;
                }
            }

            // Try borrowing from right sibling
            if (nodeIndex < parent.Children.Count - 1) {
                Node rightSibling = parent.Children[nodeIndex + 1];
                if (rightSibling.CanLendKey()) {
                    BorrowFromRight(node, rightSibling, parent, nodeIndex);
                    return;
                }
            }

            // Merge with a sibling
            if (nodeIndex > 0) {
                MergeNodes(parent.Children[nodeIndex - 1], node, parent, nodeIndex - 1);
            } else {
                MergeNodes(node, parent.Children[nodeIndex + 1], parent, nodeIndex);
            }
        }

        private void BorrowFromLeft(Node node, Node leftSibling, InternalNode parent, int nodeIndex) {
            if (node.IsLeaf) {
                LeafNode leaf = (LeafNode)node;
                LeafNode leftLeaf = (LeafNode)leftSibling;
                TKey lastKey = leftLeaf.Keys.Last();
                TValue lastValue = leftLeaf.Entries[lastKey];

                leftLeaf.Keys.RemoveAt(leftLeaf.Keys.Count - 1);
                leftLeaf.Entries.Remove(lastKey);

                leaf.InsertEntry(lastKey, lastValue);
                parent.Keys[nodeIndex - 1] = leaf.Keys[0];
            } else {
                InternalNode internalNode = (InternalNode)node;
                InternalNode leftInternal = (InternalNode)leftSibling;

                internalNode.Keys.Insert(0, parent.Keys[nodeIndex - 1]);
                parent.Keys[nodeIndex - 1] = leftInternal.Keys.Last();
                leftInternal.Keys.RemoveAt(leftInternal.Keys.Count - 1);

                internalNode.Children.Insert(0, leftInternal.Children.Last());
                internalNode.Children[0].Parent = internalNode;
                leftInternal.Children.RemoveAt(leftInternal.Children.Count - 1);
            }
        }

        private void BorrowFromRight(Node node, Node rightSibling, InternalNode parent, int nodeIndex) {
            if (node.IsLeaf) {
                LeafNode leaf = (LeafNode)node;
                LeafNode rightLeaf = (LeafNode)rightSibling;
                TKey firstKey = rightLeaf.Keys.First();
                TValue firstValue = rightLeaf.Entries[firstKey];

                rightLeaf.Keys.RemoveAt(0);
                rightLeaf.Entries.Remove(firstKey);

                leaf.InsertEntry(firstKey, firstValue);
                parent.Keys[nodeIndex] = rightLeaf.Keys[0];
            } else {
                InternalNode internalNode = (InternalNode)node;
                InternalNode rightInternal = (InternalNode)rightSibling;

                internalNode.Keys.Add(parent.Keys[nodeIndex]);
                parent.Keys[nodeIndex] = rightInternal.Keys.First();
                rightInternal.Keys.RemoveAt(0);

                internalNode.Children.Add(rightInternal.Children.First());
                internalNode.Children.Last().Parent = internalNode;
                rightInternal.Children.RemoveAt(0);
            }
        }

        private void MergeNodes(Node left, Node right, InternalNode parent, int leftIndex) {
            left.MergeWith(right);
            parent.Keys.RemoveAt(leftIndex);
            parent.Children.RemoveAt(leftIndex + 1);

            if (parent == Root && parent.Keys.Count == 0) {
                Root = left;
                Root.Parent = null;
            } else if (parent != Root && parent.Keys.Count < MinKeys) {
                HandleUnderflow(parent);
            }
        }

        private int FindInsertIndex(List<TKey> keys, TKey key) {
            if (keys.Count == 0)
                return 0;

            int index = 0;
            while (index < keys.Count && key.CompareTo(keys[index]) >= 0) {
                index++;
            }
            return index;
        }

        private void PersistToDiskWriter() {
            using (FileStream fs = new FileStream(DataFilePath, FileMode.Create))
            using (BinaryWriter writer = new BinaryWriter(fs)) {
                // Write tree metadata
                writer.Write(Order);
                writer.Write(MinKeys);

                // Serialize the tree structure
                SerializeNode(Root, writer);
            }
        }

        private void SerializeNode(Node node, BinaryWriter writer) {
            writer.Write(node.IsLeaf);
            writer.Write(node.Keys.Count);

            // Write keys
            foreach (TKey key in node.Keys) {
                // Implement custom serialization for TKey
                writer.Write(key.ToString());
            }

            if (node.IsLeaf) {
                LeafNode leaf = (LeafNode)node;
                writer.Write(leaf.Entries.Count);
                foreach (var entry in leaf.Entries) {
                    // Implement custom serialization for TKey and TValue
                    writer.Write(entry.Key.ToString());
                    writer.Write(entry.Value.ToString());
                }
            } else {
                InternalNode internalNode = (InternalNode)node;
                foreach (Node child in internalNode.Children) {
                    SerializeNode(child, writer);
                }
            }
        }

        private void LoadFromDisk() {
            using (FileStream fs = new FileStream(DataFilePath, FileMode.Open))
            using (BinaryReader reader = new BinaryReader(fs)) {
                // Read tree metadata
                int order = reader.ReadInt32();
                int minKeys = reader.ReadInt32();

                // Deserialize the tree structure
                Root = DeserializeNode(reader, null);

                // Rebuild leaf node links
                RebuildLeafLinks();
            }
        }

        private Node DeserializeNode(BinaryReader reader, Node parent) {
            bool isLeaf = reader.ReadBoolean();
            int keyCount = reader.ReadInt32();

            Node node;
            if (isLeaf) {
                LeafNode leaf = new LeafNode();
                node = leaf;

                // Read keys
                for (int i = 0; i < keyCount; i++) {
                    // Implement custom deserialization for TKey
                    string keyStr = reader.ReadString();
                    TKey key = DeserializeKey(keyStr);
                    leaf.Keys.Add(key);
                }

                // Read entries
                int entryCount = reader.ReadInt32();
                for (int i = 0; i < entryCount; i++) {
                    string keyStr = reader.ReadString();
                    string valueStr = reader.ReadString();
                    TKey key = DeserializeKey(keyStr);
                    TValue value = DeserializeValue(valueStr);
                    leaf.Entries[key] = value;
                }
            } else {
                InternalNode internalNode = new InternalNode();
                node = internalNode;

                // Read keys
                for (int i = 0; i < keyCount; i++) {
                    string keyStr = reader.ReadString();
                    TKey key = DeserializeKey(keyStr);
                    internalNode.Keys.Add(key);
                }

                // Read children
                for (int i = 0; i <= keyCount; i++) {
                    Node child = DeserializeNode(reader, internalNode);
                    internalNode.Children.Add(child);
                }
            }

            node.Parent = parent;
            return node;
        }

        private void RebuildLeafLinks() {
            FirstLeaf = FindLeftmostLeaf(Root);
            LeafNode current = FirstLeaf;

            while (current != null) {
                LeafNode next = FindNextLeaf(current);
                current.NextLeaf = next;
                current = next;
            }
        }

        private LeafNode FindLeftmostLeaf(Node node) {
            while (!node.IsLeaf) {
                node = ((InternalNode)node).Children[0];
            }
            return (LeafNode)node;
        }

        private LeafNode FindNextLeaf(LeafNode current) {
            if (current.Parent == null) {
                return null;
            }

            InternalNode parent = (InternalNode)current.Parent;
            int currentIndex = parent.Children.IndexOf(current);

            if (currentIndex < parent.Children.Count - 1) {
                return FindLeftmostLeaf(parent.Children[currentIndex + 1]);
            }

            // Traverse up until we find a parent with a next sibling
            Node node = current;
            while (node.Parent != null) {
                parent = (InternalNode)node.Parent;
                currentIndex = parent.Children.IndexOf(node);

                if (currentIndex < parent.Children.Count - 1) {
                    return FindLeftmostLeaf(parent.Children[currentIndex + 1]);
                }

                node = parent;
            }

            return null;
        }

        // Override these methods based on your TKey and TValue types
        protected virtual TKey DeserializeKey(string keyStr) {
            // Implementation depends on TKey type
            if (typeof(TKey) == typeof(int)) {
                return (TKey)(object)int.Parse(keyStr);
            }
            if (typeof(TKey) == typeof(string)) {
                return (TKey)(object)keyStr;
            }
            throw new NotImplementedException($"Deserialization not implemented for key type {typeof(TKey)}");
        }

        protected virtual TValue DeserializeValue(string valueStr) {
            // Implementation depends on TValue type
            if (typeof(TValue) == typeof(int)) {
                return (TValue)(object)int.Parse(valueStr);
            }
            if (typeof(TValue) == typeof(string)) {
                return (TValue)(object)valueStr;
            }
            throw new NotImplementedException($"Deserialization not implemented for value type {typeof(TValue)}");
        }

        public bool Validate() {
            // Check root
            if (Root == null) {
                return false;
            }

            // Check node properties recursively
            return ValidateNode(Root) && ValidateLeafLinks();
        }

        private bool ValidateNode(Node node) {
            // Check key count
            if (node != Root && node.Keys.Count < MinKeys) {
                return false;
            }

            // Check key order
            for (int i = 1; i < node.Keys.Count; i++) {
                if (node.Keys[i - 1].CompareTo(node.Keys[i]) >= 0) {
                    return false;
                }
            }

            if (!node.IsLeaf) {
                InternalNode internalNode = (InternalNode)node;

                // Check child count
                if (internalNode.Children.Count != internalNode.Keys.Count + 1) {
                    return false;
                }

                // Validate children recursively
                foreach (Node child in internalNode.Children) {
                    if (child.Parent != internalNode || !ValidateNode(child)) {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool ValidateLeafLinks() {
            LeafNode current = FirstLeaf;
            TKey lastKey = default;
            bool isFirst = true;

            while (current != null) {
                if (!current.IsLeaf) {
                    return false;
                }

                if (!isFirst) {
                    if (lastKey.CompareTo(current.Keys[0]) >= 0) {
                        return false;
                    }
                }

                if (current.Keys.Count > 0) {
                    lastKey = current.Keys.Last();
                    isFirst = false;
                }

                current = current.NextLeaf;
            }

            return true;
        }

        public string PrintTree() {
            List<string> lines = new List<string>();
            PrintNode(Root, "", true, lines);
            return string.Join(Environment.NewLine, lines);
        }

        private void PrintNode(Node node, string prefix, bool isTail, List<string> lines) {
            lines.Add($"{prefix}{(isTail ? "└── " : "├── ")}{string.Join(", ", node.Keys)}");

            if (!node.IsLeaf) {
                InternalNode internalNode = (InternalNode)node;
                for (int i = 0; i < internalNode.Children.Count - 1; i++) {
                    PrintNode(internalNode.Children[i], prefix + (isTail ? "    " : "│   "), false, lines);
                }
                if (internalNode.Children.Count > 0) {
                    PrintNode(internalNode.Children[internalNode.Children.Count - 1],
                                             prefix + (isTail ? "    " : "│   "), true, lines);
                }
            }
        }

    }
}
