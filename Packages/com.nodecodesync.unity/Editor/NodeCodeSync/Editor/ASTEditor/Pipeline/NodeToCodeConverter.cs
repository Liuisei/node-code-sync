using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnityEngine;

// Path: Assets/NodeCodeSync/Editor/ASTEditor/Pipeline/NodeToCodeConverter.cs
namespace NodeCodeSync.Editor.ASTEditor
{
    public static class NodeToCodeConverter
    {
        // =========================================================
        // Public API
        // =========================================================

        /// <summary>
        /// ルートノード群 → C#
        /// - Edge は一度だけインデックス化
        /// - 文字列組み立てのみ（Roslyn整形は任意）
        /// </summary>
        /// <param name="roots">ルートAstNode群</param>
        /// <param name="graphView">GraphView</param>
        /// <param name="formatWithRoslyn">
        /// true: ParseText + NormalizeWhitespace（見た目綺麗、重い）
        /// false: 生テキスト（速い、スペースが少し雑でもOKなら推奨）
        /// </param>
        /// <param name="enableDebug">ログ</param>
        /// <param name="enableProfiling">Stopwatchログ</param>
        public static string NodeMetasToCSharp(AstNode[] roots, AstGraphView graphView, bool
      formatWithRoslyn = false, bool enableDebug = false, bool enableProfiling = false)//ここ後で設定ファイルに移す
        {
            if (roots == null || roots.Length == 0) return string.Empty;

            var swTotal = enableProfiling ? Stopwatch.StartNew() : null;

            // 1) Edge lookup（1回だけ）
            var swLookup = enableProfiling ? Stopwatch.StartNew() : null;
            var edgeLookup = BuildEdgeLookup(graphView);
            if (enableProfiling)
            {
                swLookup!.Stop();
                UnityEngine.Debug.Log($"[NodeToCode] EdgeLookup: {swLookup.Elapsed.TotalMilliseconds:F3}ms  (edges indexed={edgeLookup.Count})");
            }

            // 2) SB で最速生成
            var swBuild = enableProfiling ? Stopwatch.StartNew() : null;

            var sb = new StringBuilder(16 * 1024);
            StringBuilder? dbg = enableDebug ? new StringBuilder(8 * 1024) : null;
            if (enableDebug) dbg!.AppendLine("[NodeToCode] === Start ===");

            for (int i = 0; i < roots.Length; i++)
                AppendNode(sb, roots[i], edgeLookup, dbg, 0, enableDebug);

            if (enableDebug)
            {
                dbg!.AppendLine("[NodeToCode] === End ===");
                UnityEngine.Debug.Log(dbg.ToString());
            }

            if (enableProfiling)
            {
                swBuild!.Stop();
                UnityEngine.Debug.Log($"[NodeToCode] BuildText: {swBuild.Elapsed.TotalMilliseconds:F3}ms  (chars={sb.Length})");
            }

            // 3) 整形（必要なときだけON推奨！）
            if (!formatWithRoslyn)
            {
                if (enableProfiling)
                {
                    swTotal!.Stop();
                    UnityEngine.Debug.Log($"[NodeToCode] TOTAL (no format): {swTotal.Elapsed.TotalMilliseconds:F3}ms");
                }
                return sb.ToString();
            }

            var swFormat = enableProfiling ? Stopwatch.StartNew() : null;
            var tree = CSharpSyntaxTree.ParseText(sb.ToString());
            var formatted = tree.GetCompilationUnitRoot().NormalizeWhitespace().ToFullString();
            if (enableProfiling)
            {
                swFormat!.Stop();
                swTotal!.Stop();
                UnityEngine.Debug.Log($"[NodeToCode] RoslynFormat(Parse+Normalize): {swFormat.Elapsed.TotalMilliseconds:F3}ms");
                UnityEngine.Debug.Log($"[NodeToCode] TOTAL (with format): {swTotal.Elapsed.TotalMilliseconds:F3}ms");
            }
            return formatted;
        }

        /// <summary>
        /// C# → AST（計測用）
        /// </summary>
        public static CompilationUnitSyntax CSharpToAST(string sourceCode, bool enableProfiling = false)
        {
            if (!enableProfiling)
            {
                SyntaxTree syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);
                return syntaxTree.GetCompilationUnitRoot();
            }

            var sw = Stopwatch.StartNew();
            SyntaxTree tree = CSharpSyntaxTree.ParseText(sourceCode);
            var root = tree.GetCompilationUnitRoot();
            sw.Stop();
            UnityEngine.Debug.Log($"[Parse] {sw.Elapsed.TotalMilliseconds:F3}ms");
            return root;
        }

        /// <summary>
        /// AST → C#（Roslyn整形）
        /// </summary>
        public static string ASTToCSharp(CompilationUnitSyntax root)
        {
            return root.NormalizeWhitespace().ToFullString();
        }

        // =========================================================
        // Edge lookup (parent, portName) -> children
        // =========================================================

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
                    // Parent: reference hash, PortName: string hash
                    return (Parent != null ? Parent.GetHashCode() : 0) * 397 ^ (PortName != null ? PortName.GetHashCode() : 0);
                }
            }
        }

        static readonly List<AstNode> s_emptyChildren = new List<AstNode>(0);

        static Dictionary<EdgeKey, List<AstNode>> BuildEdgeLookup(AstGraphView graphView)
        {
            var map = new Dictionary<EdgeKey, List<AstNode>>(256);
            foreach (var e in graphView.edges)
            {
                if (e?.output?.node is not AstNode parent) continue;
                if (e?.input?.node is not AstNode child) continue;

                var portName = e.output.portName;
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

        static List<AstNode> GetChildNodesFast(AstNode parent, string fieldName, Dictionary<EdgeKey, List<AstNode>> edgeLookup)
        {
            return edgeLookup.TryGetValue(new EdgeKey(parent, fieldName), out var list)
              ? list
              : s_emptyChildren;
        }

        //
        // Append
        // =========================================================

        static void AppendNode(StringBuilder sb, AstNode node, Dictionary<EdgeKey, List<AstNode>> edgeLookup, StringBuilder? dbg, int depth, bool enableDebug)
        {
            if (!node.RuntimeMeta.HasValue) return;

            if (enableDebug)
            {
                dbg!.Append(' ', depth * 2).Append("Node: ").AppendLine(node.RuntimeMeta.Value.Name);
            }

            AppendFields(sb, node.RuntimeMeta.Value.Fields, node, edgeLookup, dbg, depth, enableDebug);
        }

        static void AppendFields(StringBuilder sb, FieldUnit[] fields, AstNode node, Dictionary<EdgeKey, List<AstNode>> edgeLookup, StringBuilder? dbg, int depth, bool enableDebug)
        {
            if (fields == null) return;

            for (int i = 0; i < fields.Length; i++)
                AppendFieldUnit(sb, fields[i], node, edgeLookup, dbg, depth, enableDebug);
        }

        static void AppendFieldUnit(StringBuilder sb, FieldUnit unit, AstNode node, Dictionary<EdgeKey, List<AstNode>> edgeLookup, StringBuilder? dbg, int depth, bool enableDebug)
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
                        if (enableDebug)
                            dbg!.Append(' ', depth * 2).Append("Choice: idx=").Append(idx).Append('/').AppendLine(unit.Children.Length.ToString());
                        AppendFieldUnit(sb, unit.Children[idx], node, edgeLookup, dbg, depth, enableDebug);
                    }
                    break;

                case FieldUnitType.Sequence:
                    if (unit.Children != null)
                        for (int i = 0; i < unit.Children.Length; i++)
                            AppendFieldUnit(sb, unit.Children[i], node, edgeLookup, dbg, depth, enableDebug);
                    break;
            }
        }

        static void AppendSingleField(StringBuilder sb, FieldMetadata data, AstNode node, Dictionary<EdgeKey, List<AstNode>> edgeLookup, StringBuilder? dbg, int depth, bool enableDebug)
        {
            if (string.IsNullOrEmpty(data.FieldType)) return;

            var kind = FieldTypeClassifier.Classify(data.FieldType);

            if (enableDebug)
                dbg!.Append(' ', depth * 2).Append("Field: ").Append(data.Name).Append(" (").Append(data.FieldType).Append(") => ").AppendLine(kind.ToString());

            switch (kind)
            {
                case FieldTypeKind.Token:
                    if (!string.IsNullOrEmpty(data.Value))
                    {
                        sb.Append(data.Value).Append(' ');
                        if (enableDebug)
                            dbg!.Append(' ', depth * 2).Append("Token: ").Append(data.Name).Append("=\"").Append(data.Value).AppendLine("\"");
                    }
                    break;

                case FieldTypeKind.TokenList:
                    if (!string.IsNullOrEmpty(data.Value))
                    {
                        // "public, static" → "public static"
                        sb.Append(data.Value.Replace(",", " ")).Append(' ');
                        if (enableDebug)
                            dbg!.Append(' ', depth * 2).Append("TokenList: ").Append(data.Name).Append("=\"").Append(data.Value).AppendLine("\"");
                    }
                    break;

                case FieldTypeKind.SingleNode:
                case FieldTypeKind.NodeList:
                case FieldTypeKind.SeparatedNodeList:
                    {
                        var needsSep = FieldTypeClassifier.NeedsSeparator(kind);
                        var children = GetChildNodesFast(node, data.Name, edgeLookup);

                        if (enableDebug)
                            dbg!.Append(' ', depth * 2).Append("Port: ").Append(data.Name).Append(" -> ").Append(children.Count).AppendLine(" children");

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
