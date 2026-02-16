using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        // =========================================================
        // Public API
        // =========================================================

        /// <summary>
        /// Converts a set of root nodes into a formatted C# source string.
        /// </summary>
        /// <param name="roots">The top-level nodes (e.g., Namespace or Class declarations).</param>
        /// <param name="graphView">The current GraphView to resolve node connections.</param>
        /// <param name="formatWithRoslyn">If true, uses Roslyn to prettify the output (heavy operation).</param>
        /// <param name="enableDebug">If true, outputs a structural trace to the Unity Console.</param>
        /// <param name="enableProfiling">If true, logs detailed timing metrics for each phase.</param>
        public static string NodeMetasToCSharp(
            AstNode[] roots,
            AstGraphView graphView,
            bool formatWithRoslyn = false,
            bool enableDebug = false,
            bool enableProfiling = false)
        {
            if (roots == null || roots.Length == 0) return string.Empty;

            var swTotal = enableProfiling ? Stopwatch.StartNew() : null;

            // Phase 1: Edge Lookup Indexing
            // We index all edges once into a dictionary to allow O(1) child resolution during traversal.
            var swLookup = enableProfiling ? Stopwatch.StartNew() : null;
            var edgeLookup = BuildEdgeLookup(graphView);
            if (enableProfiling)
            {
                swLookup!.Stop();
                UnityEngine.Debug.Log($"[NodeToCode] EdgeLookup: {swLookup.Elapsed.TotalMilliseconds:F3}ms (edges indexed={edgeLookup.Count})");
            }

            // Phase 2: Recursive Text Generation
            // Uses a pre-allocated StringBuilder to minimize GC pressure during reconstruction.
            var swBuild = enableProfiling ? Stopwatch.StartNew() : null;
            var sb = new StringBuilder(16 * 1024);
            StringBuilder? dbg = enableDebug ? new StringBuilder(8 * 1024) : null;

            if (enableDebug) dbg!.AppendLine("[NodeToCode] === Generation Start ===");

            foreach (var rootNode in roots)
                AppendNode(sb, rootNode, edgeLookup, dbg, 0, enableDebug);

            if (enableDebug)
            {
                dbg!.AppendLine("[NodeToCode] === Generation End ===");
                UnityEngine.Debug.Log(dbg.ToString());
            }

            if (enableProfiling)
            {
                swBuild!.Stop();
                UnityEngine.Debug.Log($"[NodeToCode] BuildText: {swBuild.Elapsed.TotalMilliseconds:F3}ms (chars={sb.Length})");
            }

            // Phase 3: Post-Processing & Formatting
            if (!formatWithRoslyn)
            {
                swTotal?.Stop();
                if (enableProfiling) UnityEngine.Debug.Log($"[NodeToCode] TOTAL (raw): {swTotal!.Elapsed.TotalMilliseconds:F3}ms");
                return sb.ToString();
            }

            var swFormat = enableProfiling ? Stopwatch.StartNew() : null;
            var tree = CSharpSyntaxTree.ParseText(sb.ToString());
            var formatted = tree.GetCompilationUnitRoot().NormalizeWhitespace().ToFullString();

            if (enableProfiling)
            {
                swFormat!.Stop();
                swTotal!.Stop();
                UnityEngine.Debug.Log($"[NodeToCode] RoslynFormat: {swFormat.Elapsed.TotalMilliseconds:F3}ms");
                UnityEngine.Debug.Log($"[NodeToCode] TOTAL (formatted): {swTotal.Elapsed.TotalMilliseconds:F3}ms");
            }
            return formatted;
        }

        /// <summary>
        /// Parses raw C# code into a Roslyn CompilationUnitSyntax.
        /// </summary>
        public static CompilationUnitSyntax CSharpToAST(string sourceCode, bool enableProfiling = false)
        {
            if (!enableProfiling)
            {
                return CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
            }

            var sw = Stopwatch.StartNew();
            var root = CSharpSyntaxTree.ParseText(sourceCode).GetCompilationUnitRoot();
            sw.Stop();
            UnityEngine.Debug.Log($"[Roslyn Parse] {sw.Elapsed.TotalMilliseconds:F3}ms");
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

        private static readonly List<AstNode> s_emptyChildren = new List<AstNode>(0);

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

        private static void AppendNode(StringBuilder sb, AstNode node, Dictionary<EdgeKey, List<AstNode>> edgeLookup, StringBuilder? dbg, int depth, bool enableDebug)
        {
            if (!node.RuntimeMeta.HasValue) return;

            if (enableDebug) dbg!.Append(' ', depth * 2).Append("Node: ").AppendLine(node.RuntimeMeta.Value.Name);

            AppendFields(sb, node.RuntimeMeta.Value.Fields, node, edgeLookup, dbg, depth, enableDebug);
        }

        private static void AppendFields(StringBuilder sb, FieldUnit[] fields, AstNode node, Dictionary<EdgeKey, List<AstNode>> edgeLookup, StringBuilder? dbg, int depth, bool enableDebug)
        {
            if (fields == null) return;
            foreach (var field in fields)
                AppendFieldUnit(sb, field, node, edgeLookup, dbg, depth, enableDebug);
        }

        private static void AppendFieldUnit(StringBuilder sb, FieldUnit unit, AstNode node, Dictionary<EdgeKey, List<AstNode>> edgeLookup, StringBuilder? dbg, int depth, bool enableDebug)
        {
            switch (unit.Type)
            {
                case FieldUnitType.Single:
                    AppendSingleField(sb, unit.Data, node, edgeLookup, dbg, depth, enableDebug);
                    break;

                case FieldUnitType.Choice:
                    if (unit.Children != null && unit.Children.Length > 0)
                    {
                        var idx = Mathf.Clamp(unit.ChoiceIndex, 0, unit.Children.Length - 1);
                        if (enableDebug) dbg!.Append(' ', depth * 2).Append("Choice Selected: ").Append(idx).AppendLine();
                        AppendFieldUnit(sb, unit.Children[idx], node, edgeLookup, dbg, depth, enableDebug);
                    }
                    break;

                case FieldUnitType.Sequence:
                    if (unit.Children != null)
                        foreach (var child in unit.Children)
                            AppendFieldUnit(sb, child, node, edgeLookup, dbg, depth, enableDebug);
                    break;
            }
        }

        private static void AppendSingleField(StringBuilder sb, FieldMetadata data, AstNode node, Dictionary<EdgeKey, List<AstNode>> edgeLookup, StringBuilder? dbg, int depth, bool enableDebug)
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

                        if (enableDebug) dbg!.Append(' ', depth * 2).Append("Token: ").Append(data.Name).Append("=").AppendLine(val);
                    }
                    break;

                case FieldTypeKind.SingleNode:
                case FieldTypeKind.NodeList:
                case FieldTypeKind.SeparatedNodeList:
                    {
                        var needsSep = FieldTypeClassifier.NeedsSeparator(kind);
                        var children = GetChildNodesFast(node, data.Name, edgeLookup);

                        if (enableDebug) dbg!.Append(' ', depth * 2).Append("Link: ").Append(data.Name).Append(" -> ").Append(children.Count).AppendLine(" children");

                        for (int i = 0; i < children.Count; i++)
                        {
                            AppendNode(sb, children[i], edgeLookup, dbg, depth + 1, enableDebug);
                            if (needsSep && i < children.Count - 1) sb.Append(", ");
                        }
                    }
                    break;
            }
        }
    }
}