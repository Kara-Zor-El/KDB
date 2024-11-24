using System.Text;
using System.Text.Json;

namespace DatabaseStructures {
    public partial class BPlusTree<TKey, TValue> where TKey : IComparable<TKey> {
        public void Insert(TKey key, TValue value) {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            LeafNode leaf = FindLeafNode(key);
            leaf.InsertEntry(key, value);

            if (leaf.Keys.Count >= Order) {
                SplitNode(leaf);
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

        private void SerializeNode(Node node, BinaryWriter writer) {
            writer.Write(node.IsLeaf);
            writer.Write(node.Keys.Count);

            // Write keys
            foreach (TKey key in node.Keys) {
                SerializeKey(key, writer);
            }

            if (node.IsLeaf) {
                LeafNode leaf = (LeafNode)node;
                writer.Write(leaf.Entries.Count);

                foreach (var entry in leaf.Entries) {
                    SerializeKey(entry.Key, writer);
                    SerializeValue(entry.Value, writer);
                }
            } else {
                InternalNode internalNode = (InternalNode)node;
                foreach (Node child in internalNode.Children) {
                    SerializeNode(child, writer);
                }
            }
        }

        private void SerializeKey(TKey key, BinaryWriter writer) {
            if (key is string strKey) {
                writer.Write(strKey);
            } else if (key is int intKey) {
                writer.Write(intKey);
            } else if (key is decimal decKey) {
                writer.Write(decKey);
            } else if (key is DateTime dtKey) {
                writer.Write(dtKey.ToBinary());
            } else {
                throw new NotSupportedException($"Serialization not implemented for key type {typeof(TKey)}");
            }
        }

        private void SerializeValue(TValue value, BinaryWriter writer) {
            // For Dictionary<string, object> we need special handling
            if (value is Dictionary<string, object> dict) {
                // Convert dictionary to JSON string
                string jsonString = JsonSerializer.Serialize(dict);
                writer.Write(jsonString);
            } else if (value is string strValue) {
                writer.Write(strValue);
            } else if (value is int intValue) {
                writer.Write(intValue);
            } else if (value is decimal decValue) {
                writer.Write(decValue);
            } else if (value is DateTime dtValue) {
                writer.Write(dtValue.ToBinary());
            } else {
                throw new NotSupportedException($"Serialization not implemented for value type {typeof(TValue)}");
            }
        }

        private TKey DeserializeKey(BinaryReader reader) {
            if (typeof(TKey) == typeof(string)) {
                return (TKey)(object)reader.ReadString();
            } else if (typeof(TKey) == typeof(int)) {
                return (TKey)(object)reader.ReadInt32();
            } else if (typeof(TKey) == typeof(decimal)) {
                return (TKey)(object)reader.ReadDecimal();
            } else if (typeof(TKey) == typeof(DateTime)) {
                return (TKey)(object)DateTime.FromBinary(reader.ReadInt64());
            }
            throw new NotSupportedException($"Deserialization not implemented for key type {typeof(TKey)}");
        }

        private TValue DeserializeValue(BinaryReader reader) {
            if (typeof(TValue) == typeof(Dictionary<string, object>)) {
                string jsonString = reader.ReadString();
                return (TValue)(object)JsonSerializer.Deserialize<Dictionary<string, object>>(jsonString);
            } else if (typeof(TValue) == typeof(string)) {
                return (TValue)(object)reader.ReadString();
            } else if (typeof(TValue) == typeof(int)) {
                return (TValue)(object)reader.ReadInt32();
            } else if (typeof(TValue) == typeof(decimal)) {
                return (TValue)(object)reader.ReadDecimal();
            } else if (typeof(TValue) == typeof(DateTime)) {
                return (TValue)(object)DateTime.FromBinary(reader.ReadInt64());
            }
            throw new NotSupportedException($"Deserialization not implemented for value type {typeof(TValue)}");
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
                    TKey key = DeserializeKey(reader);
                    leaf.Keys.Add(key);
                }

                // Read entries
                int entryCount = reader.ReadInt32();
                for (int i = 0; i < entryCount; i++) {
                    TKey key = DeserializeKey(reader);
                    TValue value = DeserializeValue(reader);
                    leaf.Entries[key] = value;
                }
            } else {
                InternalNode internalNode = new InternalNode();
                node = internalNode;

                // Read keys
                for (int i = 0; i < keyCount; i++) {
                    TKey key = DeserializeKey(reader);
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
