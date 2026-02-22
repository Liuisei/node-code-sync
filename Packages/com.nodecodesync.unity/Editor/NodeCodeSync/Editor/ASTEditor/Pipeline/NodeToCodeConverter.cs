using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

// Path: NodeCodeSync/Editor/ASTEditor/Pipeline/NodeToCodeConverter.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    /// <summary>
    /// The core transformation engine that converts the Visual Graph (AstNodes and Edges) 
    /// back into C# source code. It employs a high-performance recursive traversal 
    /// with edge indexing for efficient code generation.
    /// </summary>
    public static class NodeToCodeConverter
    {
        private static readonly List<AstNode> s_emptyChildren = new List<AstNode>(0);

        const string _modifier = "[NodeToCodeConverter]";
        private static readonly NCSDebug _debug = new NCSDebug(_modifier);
        private static readonly NCSTimer _timer = new NCSTimer(_modifier);

        // =========================================================
        // Public API
        // =========================================================

        /// <summary>
        /// Converts a set of root nodes into a formatted C# source string.
        /// </summary>
        /// <param name="roots">The top-level nodes (e.g., Namespace or Class declarations).</param>
        /// <param name="graphView">The current GraphView to resolve node connections.</param>
        /// <param name="formatWithRoslyn">If true, uses Roslyn to prettify the output (heavy operation).</param>
        public static string NodeMetasToCSharp(AstNode[] roots, AstGraphView graphView, bool formatWithRoslyn = true)
        {
            if (roots == null || roots.Length == 0) return string.Empty;

            _timer.Start();

            // Phase 1: Edge Lookup Indexing
            // We index all edges once into a dictionary to allow O(1) child resolution during traversal.
            var edgeLookup = BuildEdgeLookup(graphView);
            _timer.Lap($"EdgeLookup (edges indexed={edgeLookup.Count})");

            // Phase 2: Recursive Text Generation
            // Uses a pre-allocated StringBuilder to minimize GC pressure during reconstruction.
            var sb = new StringBuilder(16 * 1024);

            _debug.Log("=== Generation Start ===");

            foreach (var rootNode in roots)
                AppendNode(sb, rootNode, edgeLookup, 0);

            _debug.Log($"=== Generation End (chars={sb.Length}) ===");
            UnityEngine.Debug.Log(_debug.PrintLog());

            _timer.Lap($"BuildText (chars={sb.Length})");

            // Phase 3: Post-Processing & Formatting
            if (!formatWithRoslyn)
            {
                UnityEngine.Debug.Log(_timer.Stop("TOTAL (raw)"));
                return sb.ToString();
            }

            var tree = CSharpSyntaxTree.ParseText(sb.ToString());
            var formatted = tree.GetCompilationUnitRoot().NormalizeWhitespace().ToFullString();

            UnityEngine.Debug.Log(_timer.Stop("TOTAL (formatted, includes RoslynFormat)"));
            return formatted;
        }

        /// <summary>
        /// Parses raw C# code into a Roslyn CompilationUnitSyntax.
        /// </summary>
        public static CompilationUnitSyntax CSharpToAST(string sourceCode)
        {
            _timer.Start();
            var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
            UnityEngine.Debug.Log(_timer.Stop("Roslyn Parse"));
            return root;
        }

        // =========================================================
        // Internal Edge Resolution Logic
        // =========================================================

        /// <summary>
        /// Unique key representing a port on a specific node for edge indexing.
        /// </summary>
        struct EdgeKey : IEquatable<EdgeKey>
        {
            public readonly AstNode Parent;
            public readonly string PortName;

            public EdgeKey(AstNode parent, string portName)
            {
                Parent = parent;
                PortName = portName;
            }

            public bool Equals(EdgeKey other) => ReferenceEquals(Parent, other.Parent) && PortName == other.PortName;
            public override bool Equals(object obj) => obj is EdgeKey other && Equals(other);
            public override int GetHashCode()
            {
                unchecked
                {
                    return ((Parent != null ? Parent.GetHashCode() : 0) * 397) ^ (PortName != null ? PortName.GetHashCode() : 0);
                }
            }
        }

        private static Dictionary<EdgeKey, List<AstNode>> BuildEdgeLookup(AstGraphView graphView)
        {
            var map = new Dictionary<EdgeKey, List<AstNode>>(256);
            foreach (var edge in graphView.edges)
            {
                if (edge?.output?.node is not AstNode parent || edge?.input?.node is not AstNode child) continue;

                var portName = edge.output.portName;
                if (string.IsNullOrEmpty(portName)) continue;

                var key = new EdgeKey(parent, portName);
                if (!map.TryGetValue(key, out var list))
                {
                    list = new List<AstNode>(2);
                    map[key] = list;
                }
                list.Add(child);
            }
            return map;
        }

        private static List<AstNode> GetChildNodesFast(AstNode parent, string fieldName, Dictionary<EdgeKey, List<AstNode>> edgeLookup)
        {
            return edgeLookup.TryGetValue(new EdgeKey(parent, fieldName), out var list) ? list : s_emptyChildren;
        }

        // =========================================================
        // Recursive Reconstruction Methods
        // =========================================================
        private static void AppendNode(StringBuilder sb, AstNode node, Dictionary<EdgeKey, List<AstNode>> edgeLookup, int depth)
        {
            if (!node.RuntimeMeta.HasValue) return;

            _debug.Log($"{new string(' ', depth * 2)}Node: {node.RuntimeMeta.Value.Name}");

            AppendFields(sb, node.RuntimeMeta.Value.Fields, node, edgeLookup, depth);
        }

        private static void AppendFields(StringBuilder sb, FieldUnit[] fields, AstNode node, Dictionary<EdgeKey, List<AstNode>> edgeLookup, int depth)
        {
            if (fields == null) return;
            foreach (var field in fields)
                AppendFieldUnit(sb, field, node, edgeLookup, depth);
        }

        private static void AppendFieldUnit(StringBuilder sb, FieldUnit unit, AstNode node, Dictionary<EdgeKey, List<AstNode>> edgeLookup, int depth)
        {
            switch (unit.Type)
            {
                case FieldUnitType.Single:
                    AppendSingleField(sb, unit.Data, node, edgeLookup, depth);
                    break;

                case FieldUnitType.Choice:
                    if (unit.Children != null && unit.Children.Length > 0)
                    {
                        var idx = Mathf.Clamp(unit.ChoiceIndex, 0, unit.Children.Length - 1);
                        _debug.Log($"{new string(' ', depth * 2)}Choice Selected: {idx}");
                        AppendFieldUnit(sb, unit.Children[idx], node, edgeLookup, depth);
                    }
                    break;

                case FieldUnitType.Sequence:
                    if (unit.Children != null)
                        foreach (var child in unit.Children)
                            AppendFieldUnit(sb, child, node, edgeLookup, depth);
                    break;
            }
        }

        private static void AppendSingleField(StringBuilder sb, FieldMetadata data, AstNode node, Dictionary<EdgeKey, List<AstNode>> edgeLookup, int depth)
        {
            if (string.IsNullOrEmpty(data.FieldType)) return;

            var kind = FieldTypeClassifier.Classify(data.FieldType);

            switch (kind)
            {
                case FieldTypeKind.Token:
                case FieldTypeKind.TokenList:
                    if (!string.IsNullOrEmpty(data.Value))
                    {
                        // Standardizing token separation
                        string val = kind == FieldTypeKind.TokenList ? data.Value.Replace(",", " ") : data.Value;
                        sb.Append(val).Append(' ');

                        _debug.Log($"{new string(' ', depth * 2)}Token: {data.Name}={val}");
                    }
                    break;

                case FieldTypeKind.SingleNode:
                case FieldTypeKind.NodeList:
                case FieldTypeKind.SeparatedNodeList:
                    {
                        var needsSep = FieldTypeClassifier.NeedsSeparator(kind);
                        var children = GetChildNodesFast(node, data.Name, edgeLookup);

                        _debug.Log($"{new string(' ', depth * 2)}Link: {data.Name} -> {children.Count} children");

                        for (int i = 0; i < children.Count; i++)
                        {
                            AppendNode(sb, children[i], edgeLookup, depth + 1);
                            if (needsSep && i < children.Count - 1) sb.Append(", ");
                        }
                    }
                    break;
            }
        }
    }
}