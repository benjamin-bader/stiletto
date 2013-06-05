/*
 * Copyright © 2013 Ben Bader
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 * http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Linq;

namespace Stiletto.Fody
{
    public class Trie
    {
        private int root;
        private SupportedCharacters supportedCharacters;
        private ushort[,] trie;

        public Trie(IEnumerable<string> input)
        {
            if (input == null)
                throw new ArgumentNullException("input");

            Initialize(input.ToList());
        }

        public bool Contains(string input)
        {
            ushort node;
            return Find(input, out node);
        }

        private ushort NextChar(ushort node, char c)
        {
            return trie[node, supportedCharacters.IndexOf(c)];
        }

        private bool IsEndOfWord(ushort node)
        {
            return trie[node, supportedCharacters.Count] == 1;
        }

        private bool Find(string input, out ushort node)
        {
            node = (ushort) root;
            int i;

            for (i = 0; i < input.Length; ++i)
            {
                if (!supportedCharacters.IsValid(input[i]))
                {
                    return false;
                }

                var next = NextChar(node, input[i]);

                if (next == 0)
                    break;

                node = next;
            }

            return i == input.Length && IsEndOfWord(node);
        }

        private void Initialize(IList<string> input)
        {
            // Identify the set of characters contained in the input
            supportedCharacters =
                new SupportedCharacters(input.Cast<IEnumerable<char>>().SelectMany(cs => cs));

            // Create a node-based trie
            var rootNode = new TrieNode(supportedCharacters);
            foreach (var str in input)
            {
                rootNode.Add(str);
            }

            // Shrink the trie down to an acyclic word graph by sharing common suffixes.
            var canonicalNodes = CreateCanonicalNodeDictionary(rootNode);

            // Make sure that our array representation can contain the number of nodes
            if (canonicalNodes.Count >= ushort.MaxValue)
                throw new InvalidOperationException("Too many nodes - UInt16 may be too small.");

            // Initialize the array representation of the node structure
            trie = new ushort[canonicalNodes.Count + 1,supportedCharacters.Count + 1];

            // Establish a mapping between canonical nodes an array indices.
            var numToNode = new TrieNode[canonicalNodes.Keys.Count + 1];
            canonicalNodes.Keys.CopyTo(numToNode, 1); // Leave the first row (number 0) null.

            var nodeToNum = new Dictionary<TrieNode, ushort>(new NodeToNumComparer());
            for (var i = 1; i < numToNode.Length; ++i)
                nodeToNum.Add(numToNode[i], (ushort) i);

            // Populate the array, and let the garbage collecter handle the object refs.
            Fill(rootNode, nodeToNum);

            root = nodeToNum[rootNode];
        }

        /// <summary>
        /// Given a <paramref name="root"/> in a trie, condenses the trie into
        /// an acyclic graph by sharing common suffixes among nodes.
        /// </summary>
        /// <param name="root">
        /// The root node of the trie to be compressed.
        /// </param>
        /// <returns>
        /// Returns a dictionary mapping all <see cref="TrieNode"/> objects
        /// reachable from <paramref name="root"/> to their canonical instance.
        /// </returns>
        private static Dictionary<TrieNode, TrieNode> CreateCanonicalNodeDictionary(TrieNode root)
        {
            var canonicalNodes = new Dictionary<TrieNode, TrieNode>(new TrieNodeCanonicalEqualityComparer());

            for (var i = 1; i <= root.Height; ++i)
                Canonicalize(root, i, canonicalNodes);

            canonicalNodes.Add(root, root);

            return canonicalNodes;
        }

        private static void Canonicalize(TrieNode node, int height, Dictionary<TrieNode, TrieNode> canonicalNodes)
        {
            if (ReferenceEquals(node, null))
                return;

            if (node.Height > height)
            {
                foreach (var childNode in node.Children)
                {
                    Canonicalize(childNode, height, canonicalNodes);
                }
            }
            else if (node.Height == height)
            {
                for (var i = 0; i < node.Children.Count; ++i)
                {
                    var n = node.Children[i];

                    if (ReferenceEquals(n, null))
                        continue;

                    TrieNode canon;
                    if (canonicalNodes.TryGetValue(n, out canon))
                    {
                        n.Children[i] = canon;
                    }
                    else
                    {
                        canonicalNodes.Add(n, n);
                    }
                }
            }
        }

        private void Fill(TrieNode node, Dictionary<TrieNode, ushort> nodeToNum)
        {
            if (ReferenceEquals(node, null))
                return;

            foreach (var childNode in node.Children)
            {
                Fill(childNode, nodeToNum);
            }

            ushort num = nodeToNum[node];

            for (var i = 0; i < node.Children.Count; ++i)
            {
                trie[num, i] = ReferenceEquals(node.Children[i], null)
                                   ? (ushort) 0
                                   : nodeToNum[node.Children[i]];
            }

            if (node.IsEndOfInput)
                trie[num, node.Children.Count] = 1;
        }

        #region Nested type: NodeToNumComparer

        private class NodeToNumComparer : IEqualityComparer<TrieNode>
        {
            #region IEqualityComparer<TrieNode> Members

            public bool Equals(TrieNode x, TrieNode y)
            {
                return ReferenceEquals(x, y);
            }

            public int GetHashCode(TrieNode obj)
            {
                return obj.GetHashCode(); //.FastHash;
            }

            #endregion
        }

        #endregion

        #region Nested type: TrieNodeCanonicalEqualityComparer

        /// <summary>
        /// A semantic-equality comparer for <see cref="TrieNode"/>s.
        /// </summary>
        /// <remarks>
        /// Semantic equality between nodes is defined as
        /// <list type="bullet">
        /// <item>Matching heights</item>
        /// <item>Matching end-of-input flags</item>
        /// <item>Reference equality of all child nodes</item>
        /// </list>
        /// Please observer that this relation assumes that the trie has
        /// in fact been compressed into an acyclic word-graph.
        /// </remarks>
        private class TrieNodeCanonicalEqualityComparer : IEqualityComparer<TrieNode>
        {
            #region IEqualityComparer<TrieNode> Members

            public bool Equals(TrieNode x, TrieNode y)
            {
                return x.Height == y.Height
                       && x.IsEndOfInput == y.IsEndOfInput
                       && x.Children
                              .Zip(y.Children, Tuple.Create)
                              .All(tup => ReferenceEquals(tup.Item1, tup.Item2));
            }

            public int GetHashCode(TrieNode obj)
            {
                // We're ignoring the FastHash because we don't have a stable
                // and working implementation yet.
                return obj.GetHashCode();
            }

            #endregion
        }

        #endregion

        private class SupportedCharacters
        {
            private readonly char[] chars;
            private readonly Dictionary<char, int> indices;

            public SupportedCharacters(IEnumerable<char> characters)
            {
                if (characters == null)
                    throw new ArgumentNullException("characters");

                indices = new Dictionary<char, int>();
                chars = characters.Distinct().ToArray();

                for (var i = 0; i < chars.Length; ++i)
                {
                    if (!indices.ContainsKey(chars[i]))
                    {
                        indices.Add(chars[i], i);
                    }
                }
            }

            public int Count
            {
                get { return chars.Length; }
            }

            public bool IsValid(char c)
            {
                return indices.ContainsKey(c);
            }

            public int IndexOf(char c)
            {
                return indices[c];
            }
        }

        private class TrieNode
        {
            private const int MaxCharsForFastHash = 26;

            private readonly SupportedCharacters supportedCharacters;
            private readonly TrieNode[] children;

            private bool isEndOfInput;
            private int height = -1;
            private int hash;

            public IList<TrieNode> Children
            {
                get { return children; }
            }

            public bool IsEndOfInput
            {
                get { return isEndOfInput; }
            }

            public TrieNode this[char c]
            {
                get { return children[IndexOf(c)]; }
                set { children[IndexOf(c)] = value; }
            }

            public bool IsFastHashSupported
            {
                get { return supportedCharacters.Count <= MaxCharsForFastHash; }
            }

            public int FastHash
            {
                get { return hash; }
            }

            public int Height
            {
                get
                {
                    if (height == -1)
                    {
                        var max = 0;
                        foreach (var h in children.Select(n => n == null ? 0 : n.Height))
                        {
                            if (h > max)
                            {
                                max = h;
                            }
                        }

                        height = max + 1;

                        // Calculate the fast hash while we're at it
                        uint newHash = 0;

                        for (var i = 0; i < children.Length; ++i)
                        {
                            if (!ReferenceEquals(children[i], null))
                            {
                                newHash += 1;
                            }

                            newHash <<= 1;
                        }

                        newHash <<= 6;
                        newHash += (uint)height;

                        hash = (int)newHash;
                    }

                    return height;
                }
            }

            public TrieNode(SupportedCharacters supportedCharacters)
            {
                if (supportedCharacters == null)
                    throw new ArgumentNullException("supportedCharacters");

                this.supportedCharacters = supportedCharacters;

                children = new TrieNode[supportedCharacters.Count];
            }

            public void Add(string input)
            {
                if (input == null)
                    throw new ArgumentNullException("input");

                if (!input.All(IsValidChar))
                {
                    throw new ArgumentOutOfRangeException("input", "Input contains unsupported characters.");
                }

                Add(input, 0);
            }

            public override int GetHashCode()
            {
//                if (IsFastHashSupported)
//                    return FastHash;

                return base.GetHashCode();
            }

            private void Add(string input, int level)
            {
                if (level == input.Length)
                {
                    isEndOfInput = true;
                }
                else
                {
                    var next = this[input[level]];

                    if (next == null)
                    {
                        next = new TrieNode(supportedCharacters);
                        this[input[level]] = next;
                    }

                    next.Add(input, level + 1);
                }
            }

            private bool IsValidChar(char c)
            {
                return supportedCharacters.IsValid(c);
            }

            private int IndexOf(char c)
            {
                return supportedCharacters.IndexOf(c);
            }
        }
    }
}
