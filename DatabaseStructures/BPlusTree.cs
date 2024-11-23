using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DatabaseStructures {
    public partial class BPlusTree<TKey, TValue> where TKey : IComparable<TKey> {
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
    }
}
